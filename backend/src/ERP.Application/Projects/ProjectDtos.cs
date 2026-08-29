namespace ERP.Application.Projects;

public sealed class CreateProjectRequest
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? ManagerId { get; set; }
    public Guid? StatusId { get; set; }
    public Guid? PriorityId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? PlannedEndDate { get; set; }
    public decimal? Budget { get; set; }
    public string? CurrencyCode { get; set; }
}

public sealed class UpdateProjectRequest
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? ManagerId { get; set; }
    public Guid? StatusId { get; set; }
    public Guid? PriorityId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? PlannedEndDate { get; set; }
    public DateOnly? ActualEndDate { get; set; }
    public decimal? CompletionPercentage { get; set; }
    public decimal? Budget { get; set; }
    public string? CurrencyCode { get; set; }
    // Optimistic concurrency token (base64 of rowversion).
    public string? RowVersion { get; set; }
}

public sealed class ProjectResponse
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? ManagerId { get; set; }
    public Guid? StatusId { get; set; }
    public Guid? PriorityId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? PlannedEndDate { get; set; }
    public DateOnly? ActualEndDate { get; set; }
    public decimal CompletionPercentage { get; set; }
    public decimal? Budget { get; set; }
    public string? CurrencyCode { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string RowVersion { get; set; } = default!;
}
