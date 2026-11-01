namespace MIS.Application.DTOs.Collections;

public sealed record VisitImportSheet(string? SheetName, int SuggestedHeaderRowNumber, IReadOnlyCollection<string> DetectedColumns);
public sealed record VisitImportUpload(Guid Id, string FileName, IReadOnlyCollection<VisitImportSheet> Sheets);
public sealed record VisitImportMapping(string? SheetName, int HeaderRow, int FirstDataRow, IReadOnlyDictionary<string, string> Columns, IReadOnlyCollection<string>? SheetNames);
public sealed record VisitImportRow(
    int Row,
    string CaseNumber,
    string? CustomerName,
    string? CollectorName,
    string? VisitDate,
    string? VisitTime,
    string? Address,
    string Status,
    IReadOnlyCollection<string> Errors,
    CreateBankVisitRequest? Request);
public sealed record VisitImportPreview(Guid Id, Guid PreviewId, IReadOnlyCollection<VisitImportRow> Rows);
public sealed record VisitImportResult(int Imported, int Skipped, int Failed);
public sealed record ConfirmVisitImportRequest(Guid PreviewId);
