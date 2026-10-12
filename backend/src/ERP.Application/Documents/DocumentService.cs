using ERP.Application.Abstractions;
using ERP.Domain.Constants;
using ERP.Domain.Entities;
using ERP.Shared.Exceptions;

namespace ERP.Application.Documents;

public sealed class DocumentResponse
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Category { get; set; } = default!;
    public string Folder { get; set; } = default!;
    public string FileName { get; set; } = default!;
    public string? ContentType { get; set; }
    public long FileSize { get; set; }
    public string? Description { get; set; }
    public Guid? UploadedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed record DocumentFile(Stream Content, string FileName, string ContentType);

public interface IDocumentService
{
    Task<IReadOnlyList<DocumentResponse>> ListForProjectAsync(Guid projectId, string? category, CancellationToken ct = default);
    Task<DocumentResponse> UploadAsync(Guid projectId, string category, string fileName, string? contentType, long length,
        Stream content, string? description, CancellationToken ct = default);
    Task<DocumentFile> DownloadAsync(Guid id, CancellationToken ct = default);
    Task DeleteAsync(Guid id, bool canDeleteAny, bool canManagePlan, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentResponse>> ListForDraftAsync(Guid draftId, CancellationToken ct = default);
    Task<DocumentResponse> UploadToDraftAsync(Guid draftId, string fileName, string? contentType, long length,
        Stream content, string? description, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentResponse>> AdoptDraftAsync(Guid projectId, Guid draftId, CancellationToken ct = default);
}

/// <summary>
/// Stores project attachments in blob storage as
/// {Organization}/Project Resources/{Project Code - Name}/{Category folder}/{id}_{file}
/// and tracks them in shared.Documents (EntityType=PROJECT).
/// </summary>
public sealed class DocumentService : IDocumentService
{
    public const long MaxFileBytes = 25L * 1024 * 1024;
    private const string ProjectEntity = "PROJECT";
    // Requirement drafts have no project yet; their files wait here (keyed by the draft id) until conversion.
    private const string DraftEntity = "REQUIREMENT_DRAFT";
    private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".bat", ".cmd", ".com", ".msi", ".scr", ".ps1", ".vbs", ".js", ".jar", ".sh", ".apk"
    };

    private readonly IDocumentRepository _documents;
    private readonly IProjectRepository _projects;
    private readonly IOrganizationRepository _orgs;
    private readonly IBlobStorage _blobs;
    private readonly ITenantContext _tenant;
    private readonly IUnitOfWork _uow;
    private readonly IAuditWriter _audit;
    private readonly IClock _clock;
    private readonly IProjectPlanRepository _plans;

    public DocumentService(IDocumentRepository documents, IProjectRepository projects, IOrganizationRepository orgs,
        IBlobStorage blobs, ITenantContext tenant, IUnitOfWork uow, IAuditWriter audit, IClock clock, IProjectPlanRepository plans)
    {
        _plans = plans;
        _documents = documents;
        _projects = projects;
        _orgs = orgs;
        _blobs = blobs;
        _tenant = tenant;
        _uow = uow;
        _audit = audit;
        _clock = clock;
    }

    public async Task<IReadOnlyList<DocumentResponse>> ListForProjectAsync(Guid projectId, string? category, CancellationToken ct = default)
    {
        await RequireProjectAsync(projectId, ct);
        var items = await _documents.ListForEntityAsync(_tenant.OrganizationId, ProjectEntity, projectId, category, ct);
        return items.Select(ToResponse).ToList();
    }

    public async Task<DocumentResponse> UploadAsync(Guid projectId, string category, string fileName, string? contentType,
        long length, Stream content, string? description, CancellationToken ct = default)
    {
        if (!DocumentCategories.IsValid(category))
            throw new ValidationException(new Dictionary<string, string[]> { ["category"] = new[] { "Unknown attachment category." } });
        var safeName = ValidateFile(fileName, length);

        var orgId = _tenant.OrganizationId;
        var project = await RequireProjectAsync(projectId, ct);
        var org = await _orgs.GetAsync(orgId, ct) ?? throw NotFoundException.For("Organization", orgId);

        var id = Guid.NewGuid();
        var folder = DocumentCategories.Folders[category];
        var path = $"{Segment(org.Name)}/Project Resources/{Segment($"{project.Code} - {project.Name}")}/{folder}/{id:N}_{safeName}";
        return await StoreAsync(id, ProjectEntity, projectId, category, safeName, path, contentType, length, content, description, ct);
    }

    public async Task<IReadOnlyList<DocumentResponse>> ListForDraftAsync(Guid draftId, CancellationToken ct = default)
    {
        var items = await _documents.ListForEntityAsync(_tenant.OrganizationId, DraftEntity, draftId, null, ct);
        return items.Select(ToResponse).ToList();
    }

    public async Task<DocumentResponse> UploadToDraftAsync(Guid draftId, string fileName, string? contentType, long length,
        Stream content, string? description, CancellationToken ct = default)
    {
        var safeName = ValidateFile(fileName, length);
        var org = await _orgs.GetAsync(_tenant.OrganizationId, ct) ?? throw NotFoundException.For("Organization", _tenant.OrganizationId);
        var id = Guid.NewGuid();
        var path = $"{Segment(org.Name)}/Requirement Drafts/{draftId:N}/{id:N}_{safeName}";
        return await StoreAsync(id, DraftEntity, draftId, DocumentCategories.Requirement, safeName, path, contentType, length, content, description, ct);
    }

    public async Task<IReadOnlyList<DocumentResponse>> AdoptDraftAsync(Guid projectId, Guid draftId, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        var project = await RequireProjectAsync(projectId, ct);
        var org = await _orgs.GetAsync(orgId, ct) ?? throw NotFoundException.For("Organization", orgId);
        var drafts = await _documents.ListForEntityAsync(orgId, DraftEntity, draftId, null, ct, track: true);
        var folder = DocumentCategories.Folders[DocumentCategories.Requirement];

        var moved = new List<(Document Doc, string OldPath)>();
        foreach (var doc in drafts)
        {
            var oldPath = doc.BlobPath!;
            var newPath = $"{Segment(org.Name)}/Project Resources/{Segment($"{project.Code} - {project.Name}")}/{folder}/{doc.Id:N}_{doc.FileName}";
            var blob = await _blobs.OpenReadAsync(oldPath, ct);
            if (blob is null) continue;
            await using (blob.Content)
                await _blobs.UploadAsync(newPath, blob.Content, doc.ContentType, ct);

            doc.EntityType = ProjectEntity;
            doc.EntityId = projectId;
            doc.Category = DocumentCategories.Requirement;
            doc.BlobPath = newPath;
            doc.UpdatedAt = _clock.UtcNow;
            moved.Add((doc, oldPath));
            _audit.Add(EntityTypes.Document, doc.Id, AuditActions.Update, null, new { Action = "AdoptedFromDraft", ProjectId = projectId });
        }

        await _uow.SaveChangesAsync(ct);
        foreach (var m in moved) await _blobs.DeleteAsync(m.OldPath, CancellationToken.None);
        return moved.Select(m => ToResponse(m.Doc)).ToList();
    }

    private async Task<DocumentResponse> StoreAsync(Guid id, string entityType, Guid entityId, string category, string safeName, string path,
        string? contentType, long length, Stream content, string? description, CancellationToken ct)
    {
        await _blobs.UploadAsync(path, content, contentType, ct);

        var doc = new Document
        {
            Id = id,
            OrganizationId = _tenant.OrganizationId,
            EntityType = entityType,
            EntityId = entityId,
            Category = category,
            FileName = safeName,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            FileSize = length,
            BlobContainer = _blobs.Container,
            BlobPath = path,
            Description = description?.Trim(),
            UploadedBy = _tenant.UserId,
            CreatedAt = _clock.UtcNow
        };

        try
        {
            await _documents.AddAsync(doc, ct);
            _audit.Add(EntityTypes.Document, doc.Id, AuditActions.Create, null, new { doc.FileName, doc.Category, EntityType = entityType, EntityId = entityId });
            await _uow.SaveChangesAsync(ct);
        }
        catch
        {
            await _blobs.DeleteAsync(path, CancellationToken.None);
            throw;
        }

        return ToResponse(doc);
    }

    private static string ValidateFile(string fileName, long length)
    {
        if (length <= 0)
            throw new ValidationException(new Dictionary<string, string[]> { ["file"] = new[] { "The file is empty." } });
        if (length > MaxFileBytes)
            throw new ValidationException(new Dictionary<string, string[]> { ["file"] = new[] { $"Files must be {MaxFileBytes / 1024 / 1024} MB or smaller." } });

        var safeName = SanitizeFileName(fileName);
        if (BlockedExtensions.Contains(Path.GetExtension(safeName)))
            throw new ValidationException(new Dictionary<string, string[]> { ["file"] = new[] { "This file type is not allowed." } });
        return safeName;
    }

    public async Task<DocumentFile> DownloadAsync(Guid id, CancellationToken ct = default)
    {
        var doc = await _documents.GetAsync(_tenant.OrganizationId, id, track: false, ct) ?? throw NotFoundException.For("Document", id);
        var blob = await _blobs.OpenReadAsync(doc.BlobPath!, ct) ?? throw NotFoundException.For("Document", id);
        return new DocumentFile(blob.Content, doc.FileName, doc.ContentType ?? blob.ContentType ?? "application/octet-stream");
    }

    public async Task DeleteAsync(Guid id, bool canDeleteAny, bool canManagePlan, CancellationToken ct = default)
    {
        var doc = await _documents.GetAsync(_tenant.OrganizationId, id, track: true, ct) ?? throw NotFoundException.For("Document", id);
        if (doc.EntityType == DraftEntity)
        {
            if (!canDeleteAny && doc.UploadedBy != _tenant.UserId)
                throw new ForbiddenException("Only the uploader or a document manager can delete this attachment.");
        }
        else if (doc.EntityType == ProjectEntity && doc.Category == DocumentCategories.Planning)
        {
            // Planning files may be removed while the plan is still a draft; once published they are a permanent record.
            var plan = await _plans.GetPlanAsync(_tenant.OrganizationId, doc.EntityId, track: false, ct);
            if (plan?.Status == PlanStatuses.Published)
                throw new ForbiddenException("Planning attachments are locked once the plan is published.");
            if (!canManagePlan && !canDeleteAny && doc.UploadedBy != _tenant.UserId)
                throw new ForbiddenException("Only the uploader or a planner can delete this attachment.");
        }
        else
            throw new ForbiddenException("Project attachments cannot be deleted.");

        // Soft delete: the blob is retained for audit/recovery.
        doc.Status = "DELETED";
        doc.UpdatedAt = _clock.UtcNow;
        _audit.Add(EntityTypes.Document, doc.Id, AuditActions.Delete, new { doc.FileName }, null);
        await _uow.SaveChangesAsync(ct);
    }

    private async Task<Project> RequireProjectAsync(Guid projectId, CancellationToken ct) =>
        await _projects.GetByIdAsync(_tenant.OrganizationId, projectId, track: false, ct) ?? throw NotFoundException.For("Project", projectId);

    private static DocumentResponse ToResponse(Document d) => new()
    {
        Id = d.Id,
        ProjectId = d.EntityId,
        Category = d.Category ?? DocumentCategories.Other,
        Folder = DocumentCategories.Folders.TryGetValue(d.Category ?? "", out var f) ? f : DocumentCategories.Folders[DocumentCategories.Other],
        FileName = d.FileName,
        ContentType = d.ContentType,
        FileSize = d.FileSize ?? 0,
        Description = d.Description,
        UploadedBy = d.UploadedBy,
        CreatedAt = d.CreatedAt
    };

    private static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName((fileName ?? "").Replace('\\', '/'));
        var cleaned = new string(name.Where(c => !char.IsControl(c) && !Path.GetInvalidFileNameChars().Contains(c)).ToArray()).Trim();
        if (cleaned.Length > 150) cleaned = cleaned[^150..];
        return string.IsNullOrWhiteSpace(cleaned) ? "file" : cleaned;
    }

    // Blob path segments must not contain separators or other path-significant characters.
    private static string Segment(string value)
    {
        var cleaned = new string(value.Where(c => !char.IsControl(c) && !"/\\:*?\"<>|#%".Contains(c)).ToArray()).Trim().Trim('.');
        if (cleaned.Length > 80) cleaned = cleaned[..80].Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "Untitled" : cleaned;
    }
}
