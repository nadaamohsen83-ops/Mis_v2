using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MIS.API.Authorization;
using MIS.Application.Common;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;

namespace MIS.API.Controllers;

[ApiController]
[Route("api/hr/excuses/imports")]
[Authorize(Policy = AuthorizationPolicies.HrDepartment)]
public sealed class HrExcuseImportsController(IExcuseImportService service) : ControllerBase
{
    [HttpGet("template")]
    public async Task<IActionResult> DownloadTemplate(CancellationToken cancellationToken)
    {
        var template = await service.BuildTemplateAsync(cancellationToken);
        return File(template.Content, template.ContentType, template.FileName);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ExcelImportLimits.RequestBytes)]
    public async Task<ActionResult<ExcuseImportUpload>> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null) throw new HrValidationException("Select an excuse import file.");
        await using var stream = file.OpenReadStream();
        return Ok(await service.UploadAsync(new HrUploadFile(file.FileName, file.ContentType, file.Length, stream), cancellationToken));
    }

    [HttpPost("{id:guid}/preview")]
    public async Task<ActionResult<ExcuseImportPreview>> Preview(Guid id, ExcuseImportMapping mapping, CancellationToken cancellationToken) =>
        Ok(await service.PreviewAsync(id, mapping, cancellationToken));

    [HttpPost("{id:guid}/confirm")]
    public async Task<ActionResult<ExcuseImportResult>> Confirm(Guid id, ConfirmExcuseImportRequest request, CancellationToken cancellationToken) =>
        Ok(await service.ConfirmAsync(id, request.PreviewId, cancellationToken, request.ExcludedRows));
}
