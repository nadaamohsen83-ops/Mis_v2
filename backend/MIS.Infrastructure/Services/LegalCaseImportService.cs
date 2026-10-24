using System.Data;
using System.Globalization;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using MIS.Application.Common;
using MIS.Application.DTOs.Legal;
using MIS.Application.Interfaces;
using MIS.Domain.Constants;
using MIS.Domain.Entities;
using MIS.Infrastructure.Persistence;

namespace MIS.Infrastructure.Services;

public sealed class LegalCaseImportService(ApplicationDbContext db, ICurrentUserContext user) : ILegalCaseImportService
{
    private const int MaximumRows = 5_000;
    private const string LegalImportAuditAction = "LegalCasesBulkImported";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record EvaluatedRow(
        LegalImportSourceRow Source,
        string Status,
        CollectionCustomer? Customer,
        CollectionCase? Case,
        DateOnly? NextSessionDate,
        IReadOnlyCollection<string> Errors);

    private sealed record ImportAuditDetails(
        string FileName,
        int TotalRows,
        int SuccessfulRows,
        int FailedRows,
        IReadOnlyCollection<LegalImportRowResultDto> Errors);

    public byte[] BuildTemplate()
    {
        using var workbook = new XLWorkbook();
        var sheet = HrImportWorkbookBuilder.AddSheet(workbook, "Legal Cases",
            "Customer Code", "Customer Name", "National ID", "Mobile Number", "Collection Case Number",
            "Account Number", "Entity / Bank", "Portfolio", "Legal Case Number", "Legal Stage", "Court",
            "Next Session Date", "Lawyer / Responsible Person", "Notes");
        sheet.Range("L2:L5001").Style.DateFormat.Format = "yyyy-mm-dd";
        sheet.Column(2).Width = 30;
        sheet.Column(5).Width = 24;
        sheet.Column(7).Width = 28;
        sheet.Column(9).Width = 24;
        sheet.Column(13).Width = 30;
        sheet.Column(14).Width = 36;
        HrImportWorkbookBuilder.AddInstructions(workbook,
            ("How to use", "طريقة الاستخدام"),
            ("Import rows must refer to existing customers and existing collection cases. This import does not create customers or collection cases.",
                "يجب أن تشير الصفوف إلى عملاء وحالات تحصيل موجودة. لا ينشئ هذا الاستيراد عملاء أو حالات تحصيل."),
            ("Match customers using Customer Code or National ID. Match the collection case using Collection Case Number or Account Number.",
                "تتم مطابقة العميل بكود العميل أو الرقم القومي، وحالة التحصيل برقم حالة التحصيل أو رقم الحساب."),
            ("Entity / Bank and Portfolio must identify the same organization and portfolio as the matched case.",
                "يجب أن تطابق الجهة / البنك والمحفظة بيانات حالة التحصيل المطابقة."),
            ("Legal Stage: INTAKE, NOTICE, COURT, or HEARING. Judgment, settlement, and return are recorded through the legal action workflow.",
                "المرحلة القانونية: INTAKE أو NOTICE أو COURT أو HEARING. يُسجل الحكم والتسوية والإرجاع من خلال إجراءات الملف القانوني."),
            ("Legal Case Number maps to the legal file's court case number. The debt balance is read from the existing collection case.",
                "رقم القضية القانونية هو رقم القضية بالمحكمة في الملف القانوني. يُقرأ الرصيد من حالة التحصيل الموجودة."),
            ("Use yyyy-mm-dd for Next Session Date. A legal case already on file is treated as a duplicate.",
                "استخدم yyyy-mm-dd لتاريخ الجلسة القادمة. تُعامل القضية القانونية المسجلة مسبقًا كتكرار."));
        return HrImportWorkbookBuilder.Save(workbook);
    }

    public async Task<LegalImportPreviewDto> PreviewAsync(string fileName, long length, Stream content, CancellationToken token)
    {
        RequireManage();
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (length is <= 0 or > ExcelImportLimits.MaximumBytes)
            throw new HrValidationException(ExcelImportLimits.SizeMessage);
        if (extension is not (".xlsx" or ".xls" or ".csv"))
            throw new HrValidationException("Only XLSX, XLS, and CSV legal case files are supported.");

        IReadOnlyCollection<ParsedCollectionRow> parsed;
        try
        {
            parsed = await CollectionImportParser.ParseAsync(content, extension, token);
        }
        catch (Exception exception) when (exception is InvalidDataException or FormatException or IOException or ArgumentException)
        {
            throw new HrValidationException("The selected legal cases file is invalid or unreadable.");
        }
        if (parsed.Count > MaximumRows)
            throw new HrValidationException($"Legal case imports cannot exceed {MaximumRows:N0} rows.");

        var sourceRows = parsed.Select(row => MapSource(row.RowNumber, row.Values)).ToArray();
        var evaluated = await EvaluateRowsAsync(sourceRows, token);
        var rows = evaluated.Select(ToPreviewRow).ToArray();
        var valid = rows.Count(x => x.Status == "VALID");
        return new LegalImportPreviewDto(
            SafeFileName(fileName),
            rows.Length,
            valid,
            rows.Length - valid,
            evaluated.Where(x => x.Customer is not null).Select(x => x.Customer!.Id).Distinct().Count(),
            evaluated.Where(x => x.Case is not null).Select(x => x.Case!.Id).Distinct().Count(),
            rows.Count(x => x.Status == "DUPLICATE"),
            valid,
            rows);
    }

    public async Task<LegalImportResultDto> ImportAsync(LegalImportRequest request, CancellationToken token)
    {
        RequireManage();
        if (request.Rows is null || request.Rows.Count == 0)
            throw new HrValidationException("The import request must contain previewed rows.");
        if (request.Rows.Count > MaximumRows)
            throw new HrValidationException($"Legal case imports cannot exceed {MaximumRows:N0} rows.");
        if (request.Rows.Select(x => x.RowNumber).Distinct().Count() != request.Rows.Count
            || request.Rows.Any(x => x.RowNumber < 2))
            throw new HrValidationException("Import row numbers must be unique and start at row 2 or later.");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var evaluated = await EvaluateRowsAsync(request.Rows, token);
        var now = DateTimeOffset.UtcNow;
        var imported = 0;
        foreach (var row in evaluated.Where(x => x.Status == "VALID"))
        {
            var item = row.Case!;
            var wasLegal = item.Status == CollectionsValues.CaseStatuses.Legal;
            if (!wasLegal)
            {
                item.UpdatePortfolioCase(CollectionsValues.CaseStatuses.Legal, item.NextFollowUpAt, now);
                db.CollectionActivities.Add(new CollectionActivity(item.Id, CollectionsValues.ActivityTypes.Legal, "REFERRED",
                    "Case referred to legal affairs through bulk legal file import.", "LEGAL", user.UserId, now, null));
            }

            var file = new LegalCaseFile(item.Id, user.UserId, now);
            file.SetStage(NormalizeStage(row.Source.LegalStage)!, now);
            file.UpdateFile(row.Source.CourtName, row.Source.LegalCaseNumber, row.Source.LawyerName,
                row.NextSessionDate, row.Source.Notes, now);
            db.LegalCaseFiles.Add(file);
            if (wasLegal)
                db.CollectionActivities.Add(new CollectionActivity(item.Id, CollectionsValues.ActivityTypes.Legal, "OPENED",
                    "Legal file opened through bulk legal file import.", "LEGAL", user.UserId, now, null));
            db.CollectionAuditLogs.Add(new CollectionAuditLog(user.UserId, "LegalCaseImported", nameof(LegalCaseFile),
                file.Id, item.Id, null,
                JsonSerializer.Serialize(new { FileName = SafeFileName(request.FileName), item.CaseNumber, file.Stage, file.CourtCaseNumber }),
                "LEGAL_IMPORT", now));
            imported++;
        }

        await SaveImportChangesAsync(token);
        var results = evaluated.Select(x =>
        {
            var succeeded = x.Status == "VALID";
            return new LegalImportRowResultDto(x.Source.RowNumber, succeeded ? "IMPORTED" : x.Status, x.Errors);
        }).ToArray();
        var failed = results.Length - imported;
        var auditId = Guid.NewGuid();
        var auditDetails = new ImportAuditDetails(
            SafeFileName(request.FileName),
            results.Length,
            imported,
            failed,
            results.Where(x => x.Status != "IMPORTED").ToArray());
        db.CollectionAuditLogs.Add(new CollectionAuditLog(user.UserId, LegalImportAuditAction, "LegalCaseImport",
            auditId, null, null, JsonSerializer.Serialize(auditDetails, JsonOptions), "LEGAL_IMPORT", now));
        await SaveImportChangesAsync(token);
        try
        {
            await transaction.CommitAsync(token);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            throw new HrConflictException("The case data changed during import. Preview the file again and retry.");
        }
        return new LegalImportResultDto(results.Length, imported, failed, results);
    }

    public async Task<IReadOnlyCollection<LegalImportHistoryDto>> GetHistoryAsync(CancellationToken token)
    {
        RequireManage();
        var rows = await db.CollectionAuditLogs.AsNoTracking()
            .Where(x => x.Action == LegalImportAuditAction && x.EntityType == "LegalCaseImport")
            .OrderByDescending(x => x.OccurredAt)
            .Take(50)
            .Select(x => new { x.Id, x.AfterJson, x.OccurredAt, UploadedBy = x.User == null ? "" : x.User.FullName })
            .ToArrayAsync(token);
        return rows.Select(x =>
        {
            var details = x.AfterJson is null ? null : JsonSerializer.Deserialize<ImportAuditDetails>(x.AfterJson, JsonOptions);
            return new LegalImportHistoryDto(
                x.Id,
                details?.FileName ?? string.Empty,
                details?.TotalRows ?? 0,
                details?.SuccessfulRows ?? 0,
                details?.FailedRows ?? 0,
                x.UploadedBy,
                x.OccurredAt,
                details?.Errors ?? []);
        }).ToArray();
    }

    private async Task<IReadOnlyCollection<EvaluatedRow>> EvaluateRowsAsync(IReadOnlyCollection<LegalImportSourceRow> sourceRows, CancellationToken token)
    {
        var organizations = await db.CollectionClientOrganizations.AsNoTracking().ToArrayAsync(token);
        var portfolios = await db.CollectionPortfolios.AsNoTracking().ToArrayAsync(token);
        var customerCodes = sourceRows.SelectMany(x => new[] { x.CustomerCode?.Trim().ToUpperInvariant(), LookupValue(x.CustomerCode) })
            .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray()!;
        var nationalIds = sourceRows.SelectMany(x => new[] { x.NationalId?.Trim(), NormalizeDigits(x.NationalId) })
            .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray()!;
        var customers = await db.CollectionCustomers
            .Where(x => customerCodes.Contains(x.CustomerCode.Trim().ToUpper())
                || (x.NationalId != null && nationalIds.Contains(x.NationalId.Trim())))
            .ToArrayAsync(token);

        var caseNumbers = sourceRows.Select(x => x.CollectionCaseNumber?.Trim().ToUpperInvariant()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray()!;
        var accountNumbers = sourceRows.Select(x => x.AccountNumber?.Trim().ToUpperInvariant()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray()!;
        var cases = new List<CollectionCase>();
        if (caseNumbers.Length > 0 || accountNumbers.Length > 0)
        {
            cases.AddRange(await db.CollectionCases
                .Include(x => x.Customer)
                .Include(x => x.Portfolio).ThenInclude(x => x.Organization)
                .Where(x => caseNumbers.Contains(x.CaseNumber.ToUpper()) || accountNumbers.Contains(x.AccountReference.ToUpper()))
                .ToArrayAsync(token));
        }

        var preliminary = sourceRows.Select(source =>
        {
            var organization = FindOrganization(source.Organization, organizations);
            var portfolio = FindPortfolio(source.Portfolio, portfolios);
            var customer = FindCustomer(source, organization, customers);
            var errors = new List<string>();
            if (organization is null) errors.Add(string.IsNullOrWhiteSpace(source.Organization) ? "ENTITY_REQUIRED" : "ENTITY_NOT_FOUND");
            if (portfolio is null) errors.Add(string.IsNullOrWhiteSpace(source.Portfolio) ? "PORTFOLIO_REQUIRED" : "PORTFOLIO_NOT_FOUND");
            if (organization is not null && portfolio is not null && portfolio.OrganizationId != organization.Id)
                errors.Add("PORTFOLIO_ORG_MISMATCH");
            ValidateSource(source, errors);
            if (customer is null && string.IsNullOrWhiteSpace(source.CustomerCode) && string.IsNullOrWhiteSpace(source.NationalId)
                && string.IsNullOrWhiteSpace(source.CollectionCaseNumber) && string.IsNullOrWhiteSpace(source.AccountNumber))
                errors.Add("CUSTOMER_REQUIRED");
            return (Source: source, Organization: organization, Portfolio: portfolio, Customer: customer, Errors: errors);
        }).ToArray();

        var fallbackCustomerIds = preliminary.Where(x => x.Customer is not null
                && string.IsNullOrWhiteSpace(x.Source.CollectionCaseNumber) && string.IsNullOrWhiteSpace(x.Source.AccountNumber)
                && x.Portfolio is not null)
            .Select(x => x.Customer!.Id).Distinct().ToArray();
        var fallbackPortfolioIds = preliminary.Where(x => x.Portfolio is not null
                && string.IsNullOrWhiteSpace(x.Source.CollectionCaseNumber) && string.IsNullOrWhiteSpace(x.Source.AccountNumber))
            .Select(x => x.Portfolio!.Id).Distinct().ToArray();
        if (fallbackCustomerIds.Length > 0 && fallbackPortfolioIds.Length > 0)
        {
            cases.AddRange(await db.CollectionCases
                .Include(x => x.Customer)
                .Include(x => x.Portfolio).ThenInclude(x => x.Organization)
                .Where(x => fallbackCustomerIds.Contains(x.CustomerId) && fallbackPortfolioIds.Contains(x.PortfolioId))
                .ToArrayAsync(token));
        }

        var casesById = cases.DistinctBy(x => x.Id).ToDictionary(x => x.Id);
        var legalCaseIds = casesById.Keys.ToArray();
        var legalFiles = legalCaseIds.Length == 0
            ? []
            : await db.LegalCaseFiles.AsNoTracking().Where(x => legalCaseIds.Contains(x.CollectionCaseId))
                .Select(x => x.CollectionCaseId).ToArrayAsync(token);
        var existingLegalCaseIds = legalFiles.ToHashSet();
        var legalNumbers = sourceRows.Select(x => x.LegalCaseNumber?.Trim().ToUpperInvariant())
            .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray()!;
        var existingLegalNumbers = legalNumbers.Length == 0
            ? []
            : await db.LegalCaseFiles.AsNoTracking()
                .Where(x => x.CourtCaseNumber != null && legalNumbers.Contains(x.CourtCaseNumber.Trim().ToUpper()))
                .Select(x => new { x.CollectionCaseId, x.CourtCaseNumber })
                .ToArrayAsync(token);
        var result = new List<EvaluatedRow>(preliminary.Length);
        var seenCaseIds = new HashSet<Guid>();
        var seenLegalNumbers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in preliminary)
        {
            var source = candidate.Source;
            var customer = candidate.Customer;
            CollectionCase? item = null;
            var caseNumberMatches = string.IsNullOrWhiteSpace(source.CollectionCaseNumber)
                ? []
                : cases.Where(x => LookupValue(x.CaseNumber) == LookupValue(source.CollectionCaseNumber)).DistinctBy(x => x.Id).ToArray();
            var accountMatches = string.IsNullOrWhiteSpace(source.AccountNumber)
                ? []
                : cases.Where(x => LookupValue(x.AccountReference) == LookupValue(source.AccountNumber)).DistinctBy(x => x.Id).ToArray();
            if (candidate.Portfolio is not null)
            {
                var scopedAccounts = accountMatches.Where(x => x.PortfolioId == candidate.Portfolio.Id).ToArray();
                if (scopedAccounts.Length > 0) accountMatches = scopedAccounts;
            }

            if (!string.IsNullOrWhiteSpace(source.CollectionCaseNumber) && !string.IsNullOrWhiteSpace(source.AccountNumber))
            {
                if (caseNumberMatches.Length == 1 && accountMatches.Length == 1 && caseNumberMatches[0].Id == accountMatches[0].Id)
                    item = caseNumberMatches[0];
                else
                    candidate.Errors.Add("CASE_REFERENCE_MISMATCH");
            }
            else if (caseNumberMatches.Length == 1)
                item = caseNumberMatches[0];
            else if (accountMatches.Length == 1)
                item = accountMatches[0];
            else if (caseNumberMatches.Length > 1 || accountMatches.Length > 1)
                candidate.Errors.Add("CASE_MATCH_AMBIGUOUS");
            else if (string.IsNullOrWhiteSpace(source.CollectionCaseNumber) && string.IsNullOrWhiteSpace(source.AccountNumber) && customer is not null && candidate.Portfolio is not null)
            {
                var customerCases = cases.Where(x => x.CustomerId == customer.Id && x.PortfolioId == candidate.Portfolio.Id && !x.IsArchived).DistinctBy(x => x.Id).ToArray();
                if (customerCases.Length == 1) item = customerCases[0];
                else if (customerCases.Length > 1) candidate.Errors.Add("CASE_REFERENCE_REQUIRED");
            }

            if (item is null && !candidate.Errors.Contains("CASE_MATCH_AMBIGUOUS")
                && !candidate.Errors.Contains("CASE_REFERENCE_MISMATCH")
                && candidate.Portfolio is not null
                && (customer is not null || !string.IsNullOrWhiteSpace(source.CollectionCaseNumber)
                    || !string.IsNullOrWhiteSpace(source.AccountNumber)))
                candidate.Errors.Add("CASE_NOT_FOUND");
            if (item is not null)
            {
                if (candidate.Portfolio is not null && item.PortfolioId != candidate.Portfolio.Id)
                    candidate.Errors.Add("CASE_PORTFOLIO_MISMATCH");
                if (candidate.Organization is not null && item.Portfolio.OrganizationId != candidate.Organization.Id)
                    candidate.Errors.Add("CASE_ENTITY_MISMATCH");
                if (customer is null) customer = item.Customer;
                else if (item.CustomerId != customer.Id) candidate.Errors.Add("CASE_CUSTOMER_MISMATCH");
                if (customer.OrganizationId != item.Customer.OrganizationId)
                    candidate.Errors.Add("CUSTOMER_ENTITY_MISMATCH");
                if (!string.IsNullOrWhiteSpace(source.CustomerCode) && LookupValue(source.CustomerCode) != LookupValue(customer.CustomerCode))
                    candidate.Errors.Add("CUSTOMER_CODE_MISMATCH");
                if (!string.IsNullOrWhiteSpace(source.NationalId)
                    && NormalizeDigits(source.NationalId) != NormalizeDigits(customer.NationalId))
                    candidate.Errors.Add("NATIONAL_ID_MISMATCH");
                if (item.IsArchived) candidate.Errors.Add("CASE_ARCHIVED");
                if (item.Status is not (CollectionsValues.CaseStatuses.Active or CollectionsValues.CaseStatuses.OnHold or CollectionsValues.CaseStatuses.Legal))
                    candidate.Errors.Add("CASE_STATUS_NOT_ELIGIBLE");
                if (existingLegalCaseIds.Contains(item.Id)) candidate.Errors.Add("CASE_ALREADY_EXISTS");
                if (!string.IsNullOrWhiteSpace(source.LegalCaseNumber)
                    && existingLegalNumbers.Any(x => x.CollectionCaseId != item.Id
                        && LookupValue(x.CourtCaseNumber) == LookupValue(source.LegalCaseNumber)))
                    candidate.Errors.Add("DUPLICATE_CASE_NUMBER");
                if (candidate.Errors.Count == 0 || candidate.Errors.All(x => x == "CASE_ALREADY_EXISTS"))
                {
                    if (!seenCaseIds.Add(item.Id)) candidate.Errors.Add("DUPLICATE");
                    var legalNumber = LookupValue(source.LegalCaseNumber);
                    if (legalNumber.Length > 0 && !seenLegalNumbers.Add(legalNumber))
                        candidate.Errors.Add("DUPLICATE");
                }
            }
            if (customer is null && candidate.Organization is not null
                && (!string.IsNullOrWhiteSpace(source.CustomerCode) || !string.IsNullOrWhiteSpace(source.NationalId)))
                candidate.Errors.Add("CUSTOMER_NOT_FOUND");

            var status = candidate.Errors.Contains("DUPLICATE") ? "DUPLICATE"
                : candidate.Errors.Contains("DUPLICATE_CASE_NUMBER") ? "DUPLICATE"
                : candidate.Errors.Contains("CASE_ALREADY_EXISTS") ? "CASE_ALREADY_EXISTS"
                : candidate.Errors.Contains("CUSTOMER_NOT_FOUND") ? "CUSTOMER_NOT_FOUND"
                : candidate.Errors.Count > 0 ? "INVALID"
                : "VALID";
            result.Add(new EvaluatedRow(source, status, customer, item, ParseDate(source.NextSessionDate), candidate.Errors.Distinct().ToArray()));
        }
        return result;
    }

    private static LegalImportSourceRow MapSource(int rowNumber, IReadOnlyDictionary<string, string> values) => new(
        rowNumber,
        Value(values, "customercode", "customerid", "customernumber", "كودالعميل", "رقمالعميل"),
        Value(values, "customername", "name", "اسم العميل", "اسمالعميل"),
        Value(values, "nationalid", "nationalnumber", "رقمقومي", "الرقمالقومي"),
        Value(values, "mobilenumber", "mobile", "phone", "رقمالموبايل", "الموبايل"),
        Value(values, "collectioncasenumber", "collectioncase", "collectioncaseno", "رقمحالةالتحصيل", "رقمملفالتحصيل"),
        Value(values, "accountnumber", "accountreference", "account", "رقمالحساب"),
        Value(values, "entitybank", "entity", "bank", "organization", "الجهة", "البنك"),
        Value(values, "portfolio", "portfolioname", "المحفظة"),
        Value(values, "legalcasenumber", "courtcasenumber", "casenumber", "رقم القضية", "رقمالقضية"),
        Value(values, "legalstage", "stage", "المرحلةالقانونية", "المرحلة"),
        Value(values, "court", "courtname", "المحكمة"),
        Value(values, "nextsessiondate", "nexthearingdate", "nexthearingon", "تاريخالجلسةالقادمة", "تاريخالجلسة"),
        Value(values, "lawyerresponsibleperson", "lawyer", "responsibleperson", "المحامي", "المسؤول"),
        Value(values, "notes", "ملاحظات"));

    private static string? Value(IReadOnlyDictionary<string, string> values, params string[] aliases)
    {
        foreach (var alias in aliases)
        {
            var key = CollectionImportParser.NormalizeHeader(alias);
            if (values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)) return value.Trim();
        }
        return null;
    }

    private static void ValidateSource(LegalImportSourceRow source, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(source.Organization)) errors.Add("ENTITY_REQUIRED");
        if (string.IsNullOrWhiteSpace(source.Portfolio)) errors.Add("PORTFOLIO_REQUIRED");
        if (string.IsNullOrWhiteSpace(source.LegalStage)) errors.Add("LEGAL_STAGE_REQUIRED");
        else if (NormalizeStage(source.LegalStage) is not { } stage) errors.Add("LEGAL_STAGE_UNKNOWN");
        else if (stage is LegalValues.Stages.Judgment or LegalValues.Stages.Settlement or LegalValues.Stages.Returned)
            errors.Add("LEGAL_STAGE_TERMINAL");
        if (!string.IsNullOrWhiteSpace(source.NationalId))
        {
            var nationalId = NormalizeDigits(source.NationalId);
            if (nationalId.Length is < 6 or > 32
                || source.NationalId.Any(character => !char.IsDigit(character) && !char.IsWhiteSpace(character) && character != '-'))
                errors.Add("NATIONAL_ID_INVALID");
        }
        if (!string.IsNullOrWhiteSpace(source.MobileNumber))
        {
            var phoneLength = NormalizeDigits(source.MobileNumber).Length;
            if (phoneLength is < 7 or > 20
                || source.MobileNumber.Any(character => !char.IsDigit(character) && !char.IsWhiteSpace(character) && character is not ('+' or '-' or '(' or ')')))
                errors.Add("MOBILE_INVALID");
        }
        CheckMax(source.CustomerCode, 100, "CUSTOMER_CODE_TOO_LONG", errors);
        CheckMax(source.CustomerName, 200, "CUSTOMER_NAME_TOO_LONG", errors);
        CheckMax(source.CollectionCaseNumber, 80, "COLLECTION_CASE_NUMBER_TOO_LONG", errors);
        CheckMax(source.AccountNumber, 120, "ACCOUNT_NUMBER_TOO_LONG", errors);
        CheckMax(source.LegalCaseNumber, 80, "LEGAL_CASE_NUMBER_TOO_LONG", errors);
        CheckMax(source.CourtName, 160, "COURT_TOO_LONG", errors);
        CheckMax(source.LawyerName, 160, "LAWYER_TOO_LONG", errors);
        CheckMax(source.Notes, 2000, "NOTES_TOO_LONG", errors);
        if (!string.IsNullOrWhiteSpace(source.NextSessionDate) && ParseDate(source.NextSessionDate) is null)
            errors.Add("DATE_INVALID");
    }

    private static void CheckMax(string? value, int max, string code, ICollection<string> errors)
    {
        if (value?.Length > max) errors.Add(code);
    }

    private static ClientOrganization? FindOrganization(string? input, IReadOnlyCollection<ClientOrganization> organizations)
    {
        var key = LookupValue(input);
        if (key.Length == 0) return null;
        var matches = organizations.Where(x => LookupValue(x.Code) == key || LookupValue(x.NameArabic) == key || LookupValue(x.NameEnglish) == key).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static CollectionPortfolio? FindPortfolio(string? input, IReadOnlyCollection<CollectionPortfolio> portfolios)
    {
        var key = LookupValue(input);
        if (key.Length == 0) return null;
        var matches = portfolios.Where(x => LookupValue(x.Code) == key || LookupValue(x.NameArabic) == key || LookupValue(x.NameEnglish) == key).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static CollectionCustomer? FindCustomer(LegalImportSourceRow source, ClientOrganization? organization, IReadOnlyCollection<CollectionCustomer> customers)
    {
        if (organization is null) return null;
        var code = LookupValue(source.CustomerCode);
        var nationalId = NormalizeDigits(source.NationalId);
        if (code.Length == 0 && nationalId.Length == 0) return null;
        var matches = customers.Where(x => x.OrganizationId == organization.Id
                && ((code.Length > 0 && LookupValue(x.CustomerCode) == code)
                    || (nationalId.Length > 0 && NormalizeDigits(x.NationalId) == nationalId)))
            .DistinctBy(x => x.Id).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static LegalImportPreviewRowDto ToPreviewRow(EvaluatedRow row)
    {
        var ar = ApiTextLocalizer.IsArabic;
        var customerName = row.Customer is null ? null
            : ar ? row.Customer.FullNameArabic ?? row.Customer.FullNameEnglish ?? row.Customer.CustomerCode
            : row.Customer.FullNameEnglish ?? row.Customer.FullNameArabic ?? row.Customer.CustomerCode;
        return new LegalImportPreviewRowDto(row.Source, row.Status, customerName, row.Case?.CaseNumber, row.Errors);
    }

    private static string? NormalizeStage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim().ToUpperInvariant();
        if (LegalValues.Stages.IsValid(trimmed)) return trimmed;
        return trimmed switch
        {
            "استلام" or "استلام الملف" => LegalValues.Stages.Intake,
            "انذار" or "إنذار" => LegalValues.Stages.Notice,
            "محكمة" => LegalValues.Stages.Court,
            "جلسة" => LegalValues.Stages.Hearing,
            "حكم" => LegalValues.Stages.Judgment,
            "تسوية" => LegalValues.Stages.Settlement,
            "معاد للتحصيل" or "إعادة للتحصيل" => LegalValues.Stages.Returned,
            _ => null,
        };
    }

    private static DateOnly? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateOnly.TryParse(value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var date))
            return date;
        if (DateTime.TryParse(value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var dateTime))
            return DateOnly.FromDateTime(dateTime);
        return null;
    }

    private static string LookupValue(string? value) =>
        new((value ?? string.Empty).Trim().ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

    private static string NormalizeDigits(string? value) =>
        string.Concat((value ?? string.Empty).Where(char.IsDigit).Select(character =>
        {
            var numeric = CharUnicodeInfo.GetDecimalDigitValue(character);
            return numeric is >= 0 and <= 9 ? (char)('0' + numeric) : character;
        }));

    private static string SafeFileName(string? value)
    {
        var safe = (value ?? string.Empty).Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim();
        if (string.IsNullOrWhiteSpace(safe)) throw new HrValidationException("The uploaded file name is invalid.");
        return safe.Length > 260 ? safe[..260] : safe;
    }

    private async Task SaveImportChangesAsync(CancellationToken token)
    {
        try
        {
            await db.SaveChangesAsync(token);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new HrConflictException("A selected collection case or legal case number was imported by another request. Preview the file again.");
        }
    }

    private bool CanManage() =>
        user.Roles.Contains(SystemRoleNames.Admin, StringComparer.OrdinalIgnoreCase)
        || user.Roles.Contains(SystemRoleNames.LegalOfficer, StringComparer.OrdinalIgnoreCase)
        || user.Permissions.Contains(SystemPermissionCodes.LegalCaseManage, StringComparer.OrdinalIgnoreCase)
        || user.Permissions.Contains("*", StringComparer.OrdinalIgnoreCase);

    private void RequireManage()
    {
        if (!CanManage()) throw new HrForbiddenException("You do not have permission to import legal cases.");
    }
}
