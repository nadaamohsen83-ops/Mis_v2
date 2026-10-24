namespace MIS.Application.DTOs.Legal;

public sealed record LegalDashboardDto(
    int OpenCases,
    int IntakeCases,
    int HearingsThisWeek,
    int ReturnedThisMonth,
    decimal OutstandingTotal,
    bool CanManage);

public sealed record LegalOrganizationDto(Guid Id, string Code, string Name);

public sealed record LegalCaseListItemDto(
    Guid CollectionCaseId,
    Guid? FileId,
    string CaseNumber,
    string CustomerName,
    string OrganizationName,
    string PortfolioName,
    decimal OutstandingBalance,
    int DaysPastDue,
    string CollectionStatus,
    string LegalStage,
    DateOnly? NextHearingOn,
    DateTimeOffset UpdatedAt);

public sealed record LegalCasePageDto(IReadOnlyList<LegalCaseListItemDto> Items, int Page, int PageSize, int TotalCount, int TotalPages);

public sealed record LegalCaseActionDto(
    Guid Id,
    string ActionType,
    string? Result,
    string Notes,
    DateOnly? HappenedOn,
    string CreatedByName,
    DateTimeOffset CreatedAt);

public sealed record LegalCaseDetailsDto(
    Guid CollectionCaseId,
    Guid FileId,
    string CaseNumber,
    string AccountReference,
    string CustomerName,
    string? NationalId,
    string? PrimaryPhone,
    string OrganizationName,
    string PortfolioName,
    decimal OriginalAmount,
    decimal OutstandingBalance,
    int DaysPastDue,
    string CollectionStatus,
    string LegalStage,
    string? CourtName,
    string? CourtCaseNumber,
    string? LawyerName,
    DateOnly? NextHearingOn,
    string? Notes,
    DateTimeOffset ReceivedAt,
    string ReceivedByName,
    DateTimeOffset UpdatedAt,
    bool CanManage,
    IReadOnlyList<LegalCaseActionDto> Actions);

public sealed record SaveLegalFileRequest(
    string? CourtName,
    string? CourtCaseNumber,
    string? LawyerName,
    DateOnly? NextHearingOn,
    string? Notes);

public sealed record RecordLegalActionRequest(
    string ActionType,
    string Notes,
    string? Result,
    DateOnly? HappenedOn);

public sealed record LegalImportSourceRow(
    int RowNumber,
    string? CustomerCode,
    string? CustomerName,
    string? NationalId,
    string? MobileNumber,
    string? CollectionCaseNumber,
    string? AccountNumber,
    string? Organization,
    string? Portfolio,
    string? LegalCaseNumber,
    string? LegalStage,
    string? CourtName,
    string? NextSessionDate,
    string? LawyerName,
    string? Notes);

public sealed record LegalImportPreviewRowDto(
    LegalImportSourceRow Source,
    string Status,
    string? MatchedCustomerName,
    string? MatchedCaseNumber,
    IReadOnlyCollection<string> Errors);

public sealed record LegalImportPreviewDto(
    string FileName,
    int TotalRows,
    int ValidRows,
    int InvalidRows,
    int ExistingCustomersMatched,
    int ExistingCasesMatched,
    int DuplicateRows,
    int NewLegalCases,
    IReadOnlyCollection<LegalImportPreviewRowDto> Rows);

public sealed record LegalImportRequest(string FileName, IReadOnlyCollection<LegalImportSourceRow> Rows);

public sealed record LegalImportRowResultDto(int RowNumber, string Status, IReadOnlyCollection<string> Errors);

public sealed record LegalImportResultDto(
    int TotalRows,
    int SuccessfulRows,
    int FailedRows,
    IReadOnlyCollection<LegalImportRowResultDto> Rows);

public sealed record LegalImportHistoryDto(
    Guid Id,
    string FileName,
    int TotalRows,
    int SuccessfulRows,
    int FailedRows,
    string UploadedBy,
    DateTimeOffset UploadedAt,
    IReadOnlyCollection<LegalImportRowResultDto> Errors);
