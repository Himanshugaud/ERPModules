using FluentValidation;

namespace ERP.Application.Identity;

public sealed class LoginRequest
{
    public string OrganizationCode { get; set; } = default!;
    public string Email { get; set; } = default!;
}

public sealed class MeResponse
{
    public Guid UserId { get; set; }
    public Guid OrganizationId { get; set; }
    public string? OrganizationName { get; set; }
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Permissions { get; set; } = Array.Empty<string>();
}

public sealed class LoginResponse
{
    public string AccessToken { get; set; } = default!;
    public string TokenType { get; set; } = "Bearer";
    public DateTime ExpiresAt { get; set; }
    public MeResponse User { get; set; } = new();
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.OrganizationCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
    }
}
