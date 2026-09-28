using FluentValidation;

namespace ERP.Application.Identity;

public sealed class CreateUserRequest
{
    public string Email { get; set; } = default!;
    public string Username { get; set; } = default!;
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
    public string? Username { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? DisplayName { get; set; }
    public string? Phone { get; set; }
    public string? JobTitle { get; set; }
    public Guid? DepartmentId { get; set; }
    public string? Status { get; set; }
}

public sealed class SetUserPasswordRequest
{
    public string Password { get; set; } = default!;
}

public sealed class ChangeOwnPasswordRequest
{
    public string? CurrentPassword { get; set; }
    public string NewPassword { get; set; } = default!;
}

public sealed class UserResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = default!;
    public string? Username { get; set; }
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
        RuleFor(x => x.Username).NotEmpty().MinimumLength(3).MaximumLength(50)
            .Matches("^[a-zA-Z0-9._-]+$").WithMessage("Username may only contain letters, numbers, dots, underscores and hyphens.");
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
        RuleFor(x => x.Username).MinimumLength(3).MaximumLength(50)
            .Matches("^[a-zA-Z0-9._-]+$").WithMessage("Username may only contain letters, numbers, dots, underscores and hyphens.")
            .When(x => !string.IsNullOrEmpty(x.Username));
        RuleFor(x => x.Status).Must(s => s is null || AllowedStatuses.Contains(s))
            .WithMessage("Status must be one of ACTIVE, INACTIVE, SUSPENDED.");
        RuleFor(x => x.DisplayName).MaximumLength(200);
    }
}

public sealed class SetUserPasswordRequestValidator : AbstractValidator<SetUserPasswordRequest>
{
    public SetUserPasswordRequestValidator()
    {
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(10)
            .MaximumLength(128)
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain a lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain a number.");
    }
}

public sealed class ChangeOwnPasswordRequestValidator : AbstractValidator<ChangeOwnPasswordRequest>
{
    public ChangeOwnPasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).MaximumLength(128);
        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(10)
            .MaximumLength(128)
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain a lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain a number.");
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
