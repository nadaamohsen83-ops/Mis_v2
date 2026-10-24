using MIS.Application.DTOs.Legal;

namespace MIS.Application.Interfaces;

public interface ILegalCaseImportService
{
    byte[] BuildTemplate();
    Task<LegalImportPreviewDto> PreviewAsync(string fileName, long length, Stream content, CancellationToken token);
    Task<LegalImportResultDto> ImportAsync(LegalImportRequest request, CancellationToken token);
    Task<IReadOnlyCollection<LegalImportHistoryDto>> GetHistoryAsync(CancellationToken token);
}
