using MIS.Application.DTOs.Hr;

namespace MIS.Application.Interfaces;

public interface IExcuseImportService
{
    Task<HrImportFileTemplate> BuildTemplateAsync(CancellationToken cancellationToken);
    Task<ExcuseImportUpload> UploadAsync(HrUploadFile file, CancellationToken cancellationToken);
    Task<ExcuseImportPreview> PreviewAsync(Guid id, ExcuseImportMapping mapping, CancellationToken cancellationToken);
    Task<ExcuseImportResult> ConfirmAsync(Guid id, Guid previewId, CancellationToken cancellationToken, IReadOnlyCollection<int>? excludedRows = null);
}
