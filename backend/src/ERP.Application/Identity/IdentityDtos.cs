using FluentValidation;

namespace ERP.Application.Identity;

public sealed class CreateUserRequest
{
    public string Email { get; set; } = default!;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? DisplayName { get; set; }
    public string? Phone { get; set; }
    public string? JobTitle { get; set; }
    public Guid? DepartmentId { get; set; }
    public string? ExternalIdentityId { get; set; }
}

public sealed class UpdateUserRequest
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? DisplayName { get; set; }
    public string? Phone { get; set; }
    public string? JobTitle { get; set; }
    public Guid? DepartmentId { get; set; }
    public string? Status { get; set; }
}

public sealed class UserResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = default!;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? DisplayName { get; set; }
    public string? Phone { get; set; }
    public string? JobTitle { get; set; }
    public Guid? DepartmentId { get; set; }
    public string Status { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
}

public sealed class RoleResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }
    public bool IsActive { get; set; }
}

public sealed class PermissionResponse
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Module { get; set; }
    public string? Resource { get; set; }
    public string? Action { get; set; }
}

public sealed class CreateRoleRequest
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
}

public sealed class UpdateRoleRequest
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.FirstName).MaximumLength(100);
        RuleFor(x => x.LastName).MaximumLength(100);
        RuleFor(x => x.DisplayName).MaximumLength(200);
    }
}

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    private static readonly string[] AllowedStatuses = { "ACTIVE", "INACTIVE", "SUSPENDED" };
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.Status).Must(s => s is null || AllowedStatuses.Contains(s))
            .WithMessage("Status must be one of ACTIVE, INACTIVE, SUSPENDED.");
        RuleFor(x => x.DisplayName).MaximumLength(200);
    }
}

public sealed class CreateRoleRequestValidator : AbstractValidator<CreateRoleRequest>
{
    public CreateRoleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public sealed class UpdateRoleRequestValidator : AbstractValidator<UpdateRoleRequest>
{
    public UpdateRoleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}
