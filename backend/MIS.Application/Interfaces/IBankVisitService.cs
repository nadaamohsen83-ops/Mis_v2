using MIS.Application.DTOs.Collections;
using MIS.Application.DTOs.Hr;

namespace MIS.Application.Interfaces;

public interface IBankVisitService
{
    Task<BankVisitSummaryDto> SummaryAsync(Guid bankId, CancellationToken token);
    Task<BankVisitPageDto> GetAsync(Guid bankId, BankVisitQuery query, CancellationToken token);
    Task<BankVisitDetailsDto> GetDetailsAsync(Guid bankId, Guid visitId, CancellationToken token);
    Task<IReadOnlyCollection<BankVisitCaseLookupDto>> CasesAsync(Guid bankId, string? search, CancellationToken token);
    Task<IReadOnlyCollection<BankPortfolioCollectorDto>> CollectorsAsync(Guid bankId, CancellationToken token);
    Task<BankVisitDetailsDto> CreateAsync(Guid bankId, CreateBankVisitRequest request, CancellationToken token);
    Task<BankVisitDetailsDto> CompleteAsync(Guid bankId, Guid visitId, CompleteBankVisitRequest request, CancellationToken token);
    Task<BankVisitDetailsDto> RescheduleAsync(Guid bankId, Guid visitId, RescheduleBankVisitRequest request, CancellationToken token);
    Task<BankVisitDetailsDto> ReassignAsync(Guid bankId, Guid visitId, ReassignBankVisitRequest request, CancellationToken token);
    Task<BankVisitDetailsDto> CancelAsync(Guid bankId, Guid visitId, CancelBankVisitRequest request, CancellationToken token);
    Task<BankVisitDetailsDto> ChangeStatusAsync(Guid bankId, Guid visitId, ChangeBankVisitStatusRequest request, CancellationToken token);
    Task<HrImportFileTemplate> BuildVisitImportTemplateAsync(Guid bankId, CancellationToken token);
    Task<VisitImportUpload> UploadVisitImportAsync(Guid bankId, HrUploadFile file, CancellationToken token);
    Task<VisitImportPreview> PreviewVisitImportAsync(Guid bankId, Guid id, VisitImportMapping mapping, CancellationToken token);
    Task<VisitImportResult> ConfirmVisitImportAsync(Guid bankId, Guid id, Guid previewId, CancellationToken token);
}
