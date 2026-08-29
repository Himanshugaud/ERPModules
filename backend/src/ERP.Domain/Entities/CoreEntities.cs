namespace ERP.Domain.Entities;

public class Organization
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? LegalName { get; set; }
    public string? Description { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? CountryCode { get; set; }
    public string? Timezone { get; set; }
    public string? CurrencyCode { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
}

public class User
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string? ExternalIdentityId { get; set; }
    public string Email { get; set; } = default!;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? DisplayName { get; set; }
    public string? Phone { get; set; }
    public string? JobTitle { get; set; }
    public Guid? DepartmentId { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public string? Timezone { get; set; }
    public string? ProfileImageUrl { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
}

public class Client
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = default!;
    public string Code { get; set; } = default!;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
