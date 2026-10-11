using ERP.Api.Common;
using ERP.Api.Security;
using ERP.Application.Abstractions;
using ERP.Application.Documents;
using ERP.Domain.Constants;
using ERP.Shared.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace ERP.Api.Functions;

public sealed class DocumentsFunctions
{
    private readonly IDocumentService _service;
    private readonly IAuthorizationGuard _auth;
    private readonly ICurrentUser _user;

    public DocumentsFunctions(IDocumentService service, IAuthorizationGuard auth, ICurrentUser user)
    {
        _service = service;
        _auth = auth;
        _user = user;
    }

    // Each attachment category is uploaded by the team that owns that stage of the workflow.
    private static string[] UploadPermissions(string category) => category switch
    {
        DocumentCategories.Requirement => new[] { Permissions.ProjectCreate },
        DocumentCategories.Planning => new[] { Permissions.PlanManage },
        DocumentCategories.Procurement => new[] { Permissions.PurchaseOrderCreate, Permissions.PurchaseOrderUpdate, Permissions.GoodsReceiptCreate },
        DocumentCategories.Transport => new[] { Permissions.TransferDispatch, Permissions.TransferReceive, Permissions.TransferCreate },
        _ => Array.Empty<string>()
    };

    [Function("ListProjectAttachments")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/projects/{projectId:guid}/attachments")] HttpRequest req,
        Guid projectId, CancellationToken ct)
    {
        _auth.Require(Permissions.DocumentRead);
        return Http.Ok(await _service.ListForProjectAsync(projectId, Http.StringQuery(req, "category"), ct));
    }

    [Function("UploadProjectAttachment")]
    public async Task<IActionResult> Upload(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/projects/{projectId:guid}/attachments")] HttpRequest req,
        Guid projectId, CancellationToken ct)
    {
        _auth.Require(Permissions.DocumentCreate);

        if (!req.HasFormContentType)
            throw Invalid("file", "Send the file as multipart/form-data.");
        var form = await req.ReadFormAsync(ct);
        var file = form.Files.FirstOrDefault() ?? throw Invalid("file", "No file was uploaded.");
        var category = form["category"].ToString();
        if (string.IsNullOrWhiteSpace(category)) category = DocumentCategories.Other;
        category = category.ToUpperInvariant();

        var required = UploadPermissions(category);
        if (required.Length > 0 && !required.Any(_user.HasPermission))
            throw new ForbiddenException($"Missing required permission: {string.Join(" or ", required)}");

        await using var stream = file.OpenReadStream();
        var created = await _service.UploadAsync(projectId, category, file.FileName, file.ContentType, file.Length, stream,
            form["description"].ToString(), ct);
        return Http.Created(created);
    }

    [Function("ListRequirementDraftAttachments")]
    public async Task<IActionResult> ListDraft(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/requirement-drafts/{draftId:guid}/attachments")] HttpRequest req,
        Guid draftId, CancellationToken ct)
    {
        _auth.Require(Permissions.DocumentRead);
        return Http.Ok(await _service.ListForDraftAsync(draftId, ct));
    }

    [Function("UploadRequirementDraftAttachment")]
    public async Task<IActionResult> UploadDraft(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/requirement-drafts/{draftId:guid}/attachments")] HttpRequest req,
        Guid draftId, CancellationToken ct)
    {
        _auth.Require(Permissions.DocumentCreate);
        _auth.Require(Permissions.ProjectCreate);
        if (!req.HasFormContentType) throw Invalid("file", "Send the file as multipart/form-data.");
        var form = await req.ReadFormAsync(ct);
        var file = form.Files.FirstOrDefault() ?? throw Invalid("file", "No file was uploaded.");

        await using var stream = file.OpenReadStream();
        return Http.Created(await _service.UploadToDraftAsync(draftId, file.FileName, file.ContentType, file.Length, stream,
            form["description"].ToString(), ct));
    }

    [Function("AdoptRequirementDraftAttachments")]
    public async Task<IActionResult> AdoptDraft(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/projects/{projectId:guid}/attachments/adopt-draft/{draftId:guid}")] HttpRequest req,
        Guid projectId, Guid draftId, CancellationToken ct)
    {
        _auth.Require(Permissions.DocumentCreate);
        _auth.Require(Permissions.ProjectCreate);
        return Http.Ok(await _service.AdoptDraftAsync(projectId, draftId, ct));
    }

    [Function("DownloadAttachment")]
    public async Task<IActionResult> Download(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/attachments/{id:guid}/download")] HttpRequest req,
        Guid id, CancellationToken ct)
    {
        _auth.Require(Permissions.DocumentRead);
        var file = await _service.DownloadAsync(id, ct);
        // Always served as a download so uploaded HTML/SVG can never execute in the app's origin.
        req.HttpContext.Response.Headers["X-Content-Type-Options"] = "nosniff";
        return new FileStreamResult(file.Content, file.ContentType) { FileDownloadName = file.FileName };
    }

    [Function("DeleteAttachment")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/attachments/{id:guid}")] HttpRequest req,
        Guid id, CancellationToken ct)
    {
        _auth.RequireAuthenticated();
        await _service.DeleteAsync(id, _user.HasPermission(Permissions.DocumentDelete), ct);
        return Http.NoContent();
    }

    private static ValidationException Invalid(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = new[] { message } });
}
