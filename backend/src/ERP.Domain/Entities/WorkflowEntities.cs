namespace ERP.Domain.Entities;

/// <summary>An uploaded file (maps to shared.Documents). Project attachments use EntityType=PROJECT, EntityId=ProjectId.</summary>
public class Document
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string EntityType { get; set; } = default!;
    public Guid EntityId { get; set; }
    public string? Category { get; set; }
    public string FileName { get; set; } = default!;
    public string? ContentType { get; set; }
    public long? FileSize { get; set; }
    public string? BlobContainer { get; set; }
    public string? BlobPath { get; set; }
    public string? Description { get; set; }
    public int Version { get; set; } = 1;
    public string Status { get; set; } = "ACTIVE";
    public Guid? UploadedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class PlanEntry
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid ProjectId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public string EntryType { get; set; } = "ACTIVITY";
    public string Status { get; set; } = "PLANNED";
    public Guid? AssignedTo { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
}

public class ProjectPlan
{
    public Guid ProjectId { get; set; }
    public Guid OrganizationId { get; set; }
    public string Status { get; set; } = "DRAFT";
    public DateTime? PublishedAt { get; set; }
    public Guid? PublishedBy { get; set; }
    public string? Notes { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
