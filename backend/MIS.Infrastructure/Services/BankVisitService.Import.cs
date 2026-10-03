using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using MIS.Application.DTOs.Collections;
using MIS.Application.DTOs.Hr;
using MIS.Domain.Entities;
using MIS.Domain.Hr;

namespace MIS.Infrastructure.Services;

public sealed partial class BankVisitService
{
    private const string ImportEntity = "VisitImport";
    private const long ImportMaximumBytes = ExcelImportLimits.MaximumBytes;
    private static readonly JsonSerializerOptions ImportJson = new(JsonSerializerDefaults.Web);
    private sealed record VisitUploaded(Guid BankId, string FileName, string StorageKey, string Extension);
    private sealed record VisitPreviewSaved(Guid PreviewId, string StorageKey, int TotalRows);

    public Task<HrImportFileTemplate> BuildVisitImportTemplateAsync(Guid bankId, CancellationToken token)
    {
        EnsureCanCreate();
        token.ThrowIfCancellationRequested();
        return Task.FromResult(new HrImportFileTemplate(VisitImportWorkbookBuilder.Build(), "Visits_Import_Template.xlsx", VisitImportWorkbookBuilder.ExcelContentType));
    }

    public async Task<VisitImportUpload> UploadVisitImportAsync(Guid bankId, HrUploadFile file, CancellationToken token)
    {
        EnsureCanCreate();
        await RequireBankAsync(bankId, token);
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (file.Length <= 0 || file.Length > ImportMaximumBytes || extension is not (".csv" or ".xls" or ".xlsx"))
            throw new HrValidationException("Choose a CSV, XLS, or XLSX file no larger than 50 MB.");
        var stored = await storage.SaveAsync($"bank-visit-imports/{bankId:N}", file.FileName, file.ContentType, file.Content, ImportMaximumBytes, token);
        try
        {
            await using var stream = await storage.OpenReadAsync(stored.StorageKey, token);
            await HrAttendanceImportService.ValidateSignatureAsync(stream, extension, token);
            var sheets = await new AttendanceImportParser(calendar).InspectAsync(stream, extension, token);
            var id = Guid.NewGuid();
            await WriteImportAsync(id, "VisitImportUploaded", new VisitUploaded(bankId, stored.OriginalFileName, stored.StorageKey, extension), token);
            return new VisitImportUpload(id, stored.OriginalFileName, sheets.Select(sheet => new VisitImportSheet(sheet.SheetName, sheet.SuggestedHeaderRowNumber, sheet.DetectedColumns)).ToArray());
        }
        catch
        {
            await storage.DeleteAsync(stored.StorageKey, CancellationToken.None);
            throw;
        }
    }

    public async Task<VisitImportPreview> PreviewVisitImportAsync(Guid bankId, Guid id, VisitImportMapping mapping, CancellationToken token)
    {
        EnsureCanCreate();
        await RequireBankAsync(bankId, token);
        var upload = await OwnedImportAsync(bankId, id, token);
        if (await db.CollectionAuditLogs.AnyAsync(log => log.EntityType == ImportEntity && log.EntityId == id && log.Action == "VisitImportCompleted", token))
            throw new HrConflictException("This visit import is already complete.");
        if (mapping.Columns is null || mapping.Columns.Count > VisitImportPlanner.Fields.Length || mapping.Columns.Keys.Except(VisitImportPlanner.Fields).Any())
            throw new HrValidationException("Unsupported visit field mapping.");
        foreach (var required in new[] { "CaseNumber", "Collector", "VisitDate", "VisitTime" })
        {
            if (!mapping.Columns.TryGetValue(required, out var column) || string.IsNullOrWhiteSpace(column))
                throw new HrValidationException("Map the case number, collector, visit date, and visit time columns.");
        }
        var selected = mapping.Columns.Values.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        if (selected.Distinct(StringComparer.Ordinal).Count() != selected.Length)
            throw new HrValidationException("Map each source column only once.");

        await using var stream = await storage.OpenReadAsync(upload.StorageKey, token);
        var table = await AttendanceImportParser.ReadTableAsync(stream, upload.Extension, mapping.SheetName, mapping.HeaderRow, mapping.FirstDataRow, token, sheetNames: mapping.SheetNames);
        var indexes = new Dictionary<string, int>();
        foreach (var pair in mapping.Columns.Where(pair => !string.IsNullOrWhiteSpace(pair.Value)))
        {
            var index = Array.FindIndex(table.Headers, column => column == pair.Value);
            if (index < 0) throw new HrValidationException("A mapped source column was not found.");
            indexes[pair.Key] = index;
        }

        var arabic = ApiTextLocalizer.IsArabic;
        var caseRows = await ScopedCases(bankId).AsNoTracking().Select(item => new VisitImportPlanner.CaseMatch(
            item.Id,
            item.CaseNumber,
            arabic ? item.Customer.FullNameArabic ?? item.Customer.FullNameEnglish! : item.Customer.FullNameEnglish ?? item.Customer.FullNameArabic!,
            arabic ? item.Customer.AddressArabic ?? item.Customer.AddressEnglish : item.Customer.AddressEnglish ?? item.Customer.AddressArabic,
            item.AssignedCollectorId,
            item.AssignedCollector == null ? null : item.AssignedCollector.FullName)).ToArrayAsync(token);
        var cases = caseRows.GroupBy(item => VisitImportPlanner.Key(item.CaseNumber)).Where(group => group.Key.Length > 0).ToDictionary(group => group.Key, group => group.ToArray());
        var collectors = Manager
            ? await AuthorizedCollectors(bankId).AsNoTracking().SelectIdentityCandidates().ToArrayAsync(token)
            : [];
        var self = await db.Users.AsNoTracking().Where(item => item.Id == user.UserId).SelectIdentityCandidates().SingleAsync(token);
        var actor = new VisitImportPlanner.Actor(Manager, user.UserId, self);
        var values = table.Rows.Select(cells => indexes.ToDictionary(pair => pair.Key, pair => pair.Value < cells.Length ? cells[pair.Value] : "")).ToArray();
        var mentioned = values.Select(row => VisitImportPlanner.Key(row.GetValueOrDefault("CaseNumber") ?? "")).Where(key => cases.ContainsKey(key)).SelectMany(key => cases[key].Select(item => item.Id)).Distinct().ToArray();
        var existing = (await ScopedVisits(bankId).AsNoTracking()
            .Where(visit => mentioned.Contains(visit.CaseId) && ActiveStatuses.Contains(visit.Status))
            .Select(visit => new { visit.CaseId, visit.ScheduledAt })
            .ToArrayAsync(token))
            .Select(visit => { var parts = VisitImportPlanner.CairoParts(visit.ScheduledAt); return (visit.CaseId, parts.Date, parts.Time); })
            .ToHashSet();
        var batch = new HashSet<(Guid, DateOnly, TimeOnly)>();
        var rows = values.Select((row, index) => VisitImportPlanner.Plan(index + mapping.FirstDataRow, row, cases, collectors, actor, existing, batch)).ToArray();
        var preview = new VisitImportPreview(id, Guid.NewGuid(), rows);
        await using var content = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(preview, ImportJson));
        var stored = await storage.SaveAsync($"bank-visit-import-previews/{bankId:N}", "preview.json", "application/json", content, ImportMaximumBytes, token);
        try { await WriteImportAsync(id, "VisitImportPreviewed", new VisitPreviewSaved(preview.PreviewId, stored.StorageKey, rows.Length), token); }
        catch { await storage.DeleteAsync(stored.StorageKey, CancellationToken.None); throw; }
        return preview with { Rows = preview.Rows.Select(row => row with { Errors = row.Errors.Select(message => ApiTextLocalizer.Localize(message)).ToArray() }).ToArray() };
    }

    public async Task<VisitImportResult> ConfirmVisitImportAsync(Guid bankId, Guid id, Guid previewId, CancellationToken token)
    {
        EnsureCanCreate();
        await RequireBankAsync(bankId, token);
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({BitConverter.ToInt64(id.ToByteArray(), 0)})", token);
        var completed = await db.CollectionAuditLogs.AsNoTracking().SingleOrDefaultAsync(log => log.EntityType == ImportEntity && log.EntityId == id && log.Action == "VisitImportCompleted" && log.UserId == user.UserId, token);
        if (completed is not null) return ReadImport<VisitImportResult>(completed.AfterJson);
        await OwnedImportAsync(bankId, id, token);
        var saved = (await db.CollectionAuditLogs.AsNoTracking()
            .Where(log => log.EntityType == ImportEntity && log.EntityId == id && log.Action == "VisitImportPreviewed" && log.UserId == user.UserId)
            .OrderByDescending(log => log.OccurredAt)
            .ToArrayAsync(token))
            .Select(log => ReadImport<VisitPreviewSaved>(log.AfterJson))
            .FirstOrDefault(item => item.PreviewId == previewId) ?? throw new HrValidationException("Build and review the visit preview before confirming.");
        await using var stream = await storage.OpenReadAsync(saved.StorageKey, token);
        var preview = await JsonSerializer.DeserializeAsync<VisitImportPreview>(stream, ImportJson, token) ?? throw new HrValidationException("The visit preview could not be read.");
        var ready = preview.Rows.Where(row => row.Status == "Ready" && row.Request is not null).ToArray();
        if (ready.Length == 0) throw new HrValidationException("There are no valid visit rows to import.");

        var caseIds = ready.Select(row => row.Request!.CaseId).Distinct().ToArray();
        var occupied = (await ScopedVisits(bankId).AsNoTracking()
            .Where(visit => caseIds.Contains(visit.CaseId) && ActiveStatuses.Contains(visit.Status))
            .Select(visit => new { visit.CaseId, visit.ScheduledAt })
            .ToArrayAsync(token))
            .Select(visit => { var parts = VisitImportPlanner.CairoParts(visit.ScheduledAt); return (visit.CaseId, parts.Date, parts.Time); })
            .ToHashSet();
        foreach (var row in ready)
        {
            var request = row.Request!;
            var parts = VisitImportPlanner.CairoParts(request.ScheduledAt);
            if (!occupied.Add((request.CaseId, parts.Date, parts.Time)))
                throw new HrConflictException("A visit is already scheduled for this case at the same date and time.");
            await CreateAsync(bankId, request, token);
        }

        var result = new VisitImportResult(ready.Length, preview.Rows.Count - ready.Length, 0);
        await WriteImportAsync(id, "VisitImportCompleted", result, token);
        await transaction.CommitAsync(token);
        return result;
    }

    private void EnsureCanCreate()
    {
        if (!Manager && !Collector) throw new HrForbiddenException("You do not have permission to create visits.");
    }

    private async Task<VisitUploaded> OwnedImportAsync(Guid bankId, Guid id, CancellationToken token)
    {
        var log = await db.CollectionAuditLogs.AsNoTracking().SingleOrDefaultAsync(item => item.EntityType == ImportEntity && item.EntityId == id && item.Action == "VisitImportUploaded" && item.UserId == user.UserId, token)
            ?? throw new HrNotFoundException("Visit import was not found.");
        var upload = ReadImport<VisitUploaded>(log.AfterJson);
        if (upload.BankId != bankId) throw new HrNotFoundException("Visit import was not found.");
        return upload;
    }

    private async Task WriteImportAsync(Guid id, string action, object value, CancellationToken token)
    {
        db.CollectionAuditLogs.Add(new CollectionAuditLog(user.UserId, action, ImportEntity, id, null, null, JsonSerializer.Serialize(value, ImportJson), "BANK_WORKSPACE", DateTimeOffset.UtcNow));
        await db.SaveChangesAsync(token);
    }

    private static T ReadImport<T>(string? value) => JsonSerializer.Deserialize<T>(value ?? "null", ImportJson) ?? throw new HrValidationException("Import metadata is unavailable.");
}
