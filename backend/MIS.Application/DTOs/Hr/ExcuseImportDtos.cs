using System.ComponentModel.DataAnnotations;

namespace MIS.Application.DTOs.Hr;

public sealed class ExcuseImportMapping
{
    [StringLength(128)] public string? SheetName { get; init; }
    [Range(1, 1000)] public int HeaderRow { get; init; } = 1;
    [Range(2, 2000)] public int FirstDataRow { get; init; } = 2;
    [StringLength(32)] public string? DateFormat { get; init; }
    public Dictionary<string, string> Columns { get; init; } = [];
    public List<string>? SheetNames { get; init; }
}

public sealed record ExcuseImportUpload(Guid Id, string FileName, IReadOnlyCollection<AttendanceImportSheetDto> Sheets);

public sealed record ExcuseImportRecord(
    Guid EmployeeId,
    string Type,
    DateOnly Date,
    bool FullDay,
    TimeOnly? FromTime,
    TimeOnly? ToTime,
    string Reason);

public sealed record ExcuseImportRow(
    int Row,
    string EmployeeNumber,
    string EmployeeName,
    string ExcuseType,
    string? Date,
    string? FromTime,
    string? ToTime,
    string Status,
    IReadOnlyCollection<string> Errors,
    ExcuseImportRecord? Record);

public sealed record ExcuseImportPreview(Guid Id, Guid PreviewId, IReadOnlyCollection<ExcuseImportRow> Rows);
public sealed record ExcuseImportResult(int Imported, int Skipped, int Failed);
public sealed record ConfirmExcuseImportRequest(Guid PreviewId, IReadOnlyCollection<int>? ExcludedRows = null);
