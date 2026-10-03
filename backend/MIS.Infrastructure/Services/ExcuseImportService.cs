using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;
using MIS.Domain.Entities;
using MIS.Infrastructure.Persistence;

namespace MIS.Infrastructure.Services;

public sealed class ExcuseImportService(
    ApplicationDbContext db,
    IHrFileStorage storage,
    IHrAuditService audit,
    ICurrentUserContext user,
    IWorkingCalendarCalculator calendar,
    HrMissionSynchronizer sync) : IExcuseImportService
{
    private const string Entity = "ExcuseImport";
    private const long MaximumBytes = ExcelImportLimits.MaximumBytes;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private sealed record Uploaded(string FileName, string StorageKey, string Extension);
    private sealed record PreviewSaved(Guid PreviewId, string StorageKey, int TotalRows);

    private bool CanManage => user.Roles.Contains("Admin") || user.Permissions.Contains("*")
        || user.Roles.Contains("HrManager") || user.Permissions.Contains("hr.excuses.approve")
        || user.Roles.Contains("HrOfficer") || user.Permissions.Contains("hr.excuses.manage");

    private void WriteAccess()
    {
        if (!CanManage) throw new HrForbiddenException("Excuse management permission is required.");
    }

    public Task<HrImportFileTemplate> BuildTemplateAsync(CancellationToken cancellationToken)
    {
        WriteAccess();
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new HrImportFileTemplate(
            HrImportWorkbookBuilder.BuildExcuse(),
            "Excuses_Import_Template.xlsx",
            HrImportWorkbookBuilder.ExcelContentType));
    }

    public async Task<ExcuseImportUpload> UploadAsync(HrUploadFile file, CancellationToken cancellationToken)
    {
        WriteAccess();
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (file.Length <= 0 || file.Length > MaximumBytes || extension is not (".csv" or ".xls" or ".xlsx"))
            throw new HrValidationException("Choose a CSV, XLS, or XLSX file no larger than 50 MB.");
        var stored = await storage.SaveAsync("excuse-imports", file.FileName, file.ContentType, file.Content, MaximumBytes, cancellationToken);
        try
        {
            await using var stream = await storage.OpenReadAsync(stored.StorageKey, cancellationToken);
            await HrAttendanceImportService.ValidateSignatureAsync(stream, extension, cancellationToken);
            var sheets = await new AttendanceImportParser(calendar).InspectAsync(stream, extension, cancellationToken);
            var id = Guid.NewGuid();
            await Write(id, "ExcuseImportUploaded", new Uploaded(stored.OriginalFileName, stored.StorageKey, extension), cancellationToken);
            return new ExcuseImportUpload(id, stored.OriginalFileName, sheets);
        }
        catch
        {
            await storage.DeleteAsync(stored.StorageKey, CancellationToken.None);
            throw;
        }
    }

    public async Task<ExcuseImportPreview> PreviewAsync(Guid id, ExcuseImportMapping mapping, CancellationToken cancellationToken)
    {
        WriteAccess();
        var upload = Read<Uploaded>((await OwnedUpload(id, cancellationToken)).NewValue);
        if (await db.HrAuditLogs.AnyAsync(log => log.EntityType == Entity && log.EntityId == id && log.Action == "ExcuseImportCompleted", cancellationToken))
            throw new HrConflictException("This excuse import is already complete.");
        if (mapping.Columns is null || mapping.Columns.Count > ExcuseImportMapper.Fields.Length || mapping.Columns.Keys.Except(ExcuseImportMapper.Fields).Any())
            throw new HrValidationException("Unsupported excuse field mapping.");
        foreach (var required in new[] { "EmployeeNumber", "ExcuseType", "Date" })
        {
            if (!mapping.Columns.TryGetValue(required, out var column) || string.IsNullOrWhiteSpace(column))
                throw new HrValidationException("Map the employee number, excuse type, and date columns.");
        }
        var selected = mapping.Columns.Values.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        if (selected.Distinct(StringComparer.Ordinal).Count() != selected.Length)
            throw new HrValidationException("Map each source column only once.");

        await using var stream = await storage.OpenReadAsync(upload.StorageKey, cancellationToken);
        var table = await AttendanceImportParser.ReadTableAsync(stream, upload.Extension, mapping.SheetName, mapping.HeaderRow, mapping.FirstDataRow, cancellationToken, sheetNames: mapping.SheetNames);
        var indexes = new Dictionary<string, int>();
        foreach (var pair in mapping.Columns.Where(pair => !string.IsNullOrWhiteSpace(pair.Value)))
        {
            var index = Array.FindIndex(table.Headers, column => column == pair.Value);
            if (index < 0) throw new HrValidationException("A mapped source column was not found.");
            indexes[pair.Key] = index;
        }

        var employeeRows = await db.Employees.AsNoTracking()
            .Select(employee => new { employee.Id, employee.EmployeeNumber, employee.FullName, employee.FullNameArabic, employee.FullNameEnglish, employee.IsActive, employee.IsArchived, employee.HireDate, employee.TerminationDate })
            .ToArrayAsync(cancellationToken);
        var arabic = ApiTextLocalizer.IsArabic;
        var employees = ExcuseImportMapper.Index(employeeRows.Select(employee => new ExcuseImportMapper.EmployeeMatch(
            employee.Id,
            employee.EmployeeNumber,
            (arabic ? employee.FullNameArabic : employee.FullNameEnglish) ?? employee.FullName,
            employee.IsActive,
            employee.IsArchived,
            employee.HireDate,
            employee.TerminationDate)).ToArray());
        var existing = (await db.HrExcuseMissions.AsNoTracking()
            .Where(mission => mission.SourceType == "Manual" && mission.EmployeeId != null && (mission.Status == "PendingApproval" || mission.Status == "Approved"))
            .Select(mission => new { EmployeeId = mission.EmployeeId!.Value, mission.Date, mission.FromTime, mission.ToTime })
            .ToArrayAsync(cancellationToken))
            .Select(mission => (mission.EmployeeId, mission.Date, mission.FromTime, mission.ToTime))
            .ToHashSet();

        var batch = new HashSet<(Guid EmployeeId, DateOnly Date, TimeOnly? From, TimeOnly? To)>();
        var rows = new List<ExcuseImportRow>();
        foreach (var cells in table.Rows)
        {
            var values = indexes.ToDictionary(pair => pair.Key, pair => pair.Value < cells.Length ? cells[pair.Value] : "");
            rows.Add(ExcuseImportMapper.Map(rows.Count + mapping.FirstDataRow, values, mapping.DateFormat, employees, existing, batch));
        }

        var preview = new ExcuseImportPreview(id, Guid.NewGuid(), rows);
        await using var content = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(preview, Json));
        var stored = await storage.SaveAsync("excuse-import-previews", "preview.json", "application/json", content, MaximumBytes, cancellationToken);
        try { await Write(id, "ExcuseImportPreviewed", new PreviewSaved(preview.PreviewId, stored.StorageKey, rows.Count), cancellationToken); }
        catch { await storage.DeleteAsync(stored.StorageKey, CancellationToken.None); throw; }
        return preview with { Rows = preview.Rows.Select(row => row with { Errors = row.Errors.Select(message => ApiTextLocalizer.Localize(message)).ToArray() }).ToArray() };
    }

    public async Task<ExcuseImportResult> ConfirmAsync(Guid id, Guid previewId, CancellationToken cancellationToken, IReadOnlyCollection<int>? excludedRows = null)
    {
        WriteAccess();
        await OwnedUpload(id, cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({BitConverter.ToInt64(id.ToByteArray(), 0)})", cancellationToken);
        var completed = await db.HrAuditLogs.AsNoTracking().SingleOrDefaultAsync(log => log.EntityType == Entity && log.EntityId == id && log.Action == "ExcuseImportCompleted", cancellationToken);
        if (completed is not null) return Read<ExcuseImportResult>(completed.NewValue);

        var saved = (await db.HrAuditLogs.AsNoTracking()
            .Where(log => log.EntityType == Entity && log.EntityId == id && log.Action == "ExcuseImportPreviewed")
            .OrderByDescending(log => log.Timestamp)
            .ToArrayAsync(cancellationToken))
            .Select(log => Read<PreviewSaved>(log.NewValue))
            .FirstOrDefault();
        if (saved is null || saved.PreviewId != previewId)
            throw new HrValidationException("Build and review the excuse preview before confirming.");
        await using var stream = await storage.OpenReadAsync(saved.StorageKey, cancellationToken);
        var preview = await JsonSerializer.DeserializeAsync<ExcuseImportPreview>(stream, Json, cancellationToken)
            ?? throw new HrValidationException("The excuse preview could not be read.");

        var excluded = (excludedRows ?? []).ToHashSet();
        var ready = preview.Rows.Where(row => row.Status == "Ready" && row.Record is not null && !excluded.Contains(row.Row)).ToArray();
        if (ready.Length == 0) throw new HrValidationException("There are no valid excuse rows to import.");

        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({HrMissionSynchronizer.LockKey})", cancellationToken);
        var employeeIds = ready.Select(row => row.Record!.EmployeeId).Distinct().ToArray();
        var dates = ready.Select(row => row.Record!.Date).Distinct().ToArray();
        var employees = await db.Employees.AsNoTracking()
            .Where(employee => employeeIds.Contains(employee.Id))
            .Select(employee => new { employee.Id, employee.IsActive, employee.IsArchived, employee.HireDate, employee.TerminationDate })
            .ToDictionaryAsync(employee => employee.Id, cancellationToken);
        var existing = (await db.HrExcuseMissions.AsNoTracking()
            .Where(mission => mission.SourceType == "Manual" && mission.EmployeeId != null && employeeIds.Contains(mission.EmployeeId.Value) && dates.Contains(mission.Date) && (mission.Status == "PendingApproval" || mission.Status == "Approved"))
            .Select(mission => new { EmployeeId = mission.EmployeeId!.Value, mission.Date, mission.FromTime, mission.ToTime })
            .ToArrayAsync(cancellationToken))
            .Select(mission => (mission.EmployeeId, mission.Date, mission.FromTime, mission.ToTime))
            .ToHashSet();

        var imported = 0;
        var skipped = preview.Rows.Count - ready.Length;
        var failed = 0;
        var now = DateTimeOffset.UtcNow;
        foreach (var row in ready)
        {
            var record = row.Record!;
            if (!employees.TryGetValue(record.EmployeeId, out var employee) || !employee.IsActive || employee.IsArchived
                || (employee.HireDate.HasValue && record.Date < employee.HireDate.Value)
                || (employee.TerminationDate.HasValue && record.Date > employee.TerminationDate.Value))
            {
                failed++;
                continue;
            }
            var key = (record.EmployeeId, record.Date, record.FromTime, record.ToTime);
            if (!existing.Add(key))
            {
                skipped++;
                continue;
            }
            try
            {
                var mission = new HrExcuseMission(record.EmployeeId, record.Type, record.Date, record.FromTime, record.ToTime, record.Reason, record.Reason, user.UserId, now);
                mission.SetManualPeriod(record.FullDay, record.FromTime, record.ToTime, now);
                if (mission.Status != "PendingApproval") throw new InvalidOperationException("Imported excuses stay pending approval.");
                db.HrExcuseMissions.Add(mission);
                sync.Audit("ExcuseRequestCreated", nameof(HrExcuseMission), mission.Id, mission.EmployeeId, null, new
                {
                    mission.Type,
                    mission.Date,
                    mission.FullDay,
                    mission.FromTime,
                    mission.ToTime,
                    mission.Status,
                    mission.SourceType,
                    mission.Reason,
                    ImportId = id
                }, user.UserId);
                imported++;
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException)
            {
                existing.Remove(key);
                failed++;
            }
        }

        var result = new ExcuseImportResult(imported, skipped, failed);
        await Write(id, "ExcuseImportCompleted", result, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private async Task<HrAuditLog> OwnedUpload(Guid id, CancellationToken cancellationToken) =>
        await db.HrAuditLogs.AsNoTracking().SingleOrDefaultAsync(log => log.EntityType == Entity && log.EntityId == id
            && log.Action == "ExcuseImportUploaded" && log.UserId == user.UserId, cancellationToken)
        ?? throw new HrNotFoundException("Excuse import was not found.");

    private Task Write(Guid id, string action, object value, CancellationToken cancellationToken) =>
        audit.WriteAsync(new AuditWriteRequest(action, Entity, id.ToString(), null, null, value, action), cancellationToken);

    private static T Read<T>(string? value) => JsonSerializer.Deserialize<T>(value ?? "null", Json) ?? throw new HrValidationException("Import metadata is unavailable.");
}
