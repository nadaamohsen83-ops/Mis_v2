using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MIS.API.Authorization;
using MIS.Application.Common;
using MIS.Application.DTOs.Collections;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;

namespace MIS.API.Controllers;

[ApiController]
[Route("api/banks/{bankId:guid}/visits/imports")]
[Route("api/installment-companies/{bankId:guid}/visits/imports")]
[Authorize(Policy = AuthorizationPolicies.CollectionsAccess)]
public sealed class BankVisitImportsController(IBankVisitService service) : ControllerBase
{
    [HttpGet("template")]
    public async Task<IActionResult> DownloadTemplate(Guid bankId, CancellationToken token)
    {
        var template = await service.BuildVisitImportTemplateAsync(bankId, token);
        return File(template.Content, template.ContentType, template.FileName);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ExcelImportLimits.RequestBytes)]
    public async Task<ActionResult<VisitImportUpload>> Upload(Guid bankId, IFormFile file, CancellationToken token)
    {
        if (file is null) throw new HrValidationException("Select a visit import file.");
        await using var stream = file.OpenReadStream();
        return Ok(await service.UploadVisitImportAsync(bankId, new HrUploadFile(file.FileName, file.ContentType, file.Length, stream), token));
    }

    [HttpPost("{id:guid}/preview")]
    public async Task<ActionResult<VisitImportPreview>> Preview(Guid bankId, Guid id, VisitImportMapping mapping, CancellationToken token) =>
        Ok(await service.PreviewVisitImportAsync(bankId, id, mapping, token));

    [HttpPost("{id:guid}/confirm")]
    public async Task<ActionResult<VisitImportResult>> Confirm(Guid bankId, Guid id, ConfirmVisitImportRequest request, CancellationToken token) =>
        Ok(await service.ConfirmVisitImportAsync(bankId, id, request.PreviewId, token));
}
