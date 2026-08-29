using FluentValidation;

namespace ERP.Application.Clients;

public sealed class CreateClientRequest
{
    public string Name { get; set; } = default!;
    public string Code { get; set; } = default!;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Status { get; set; }
}

public sealed class UpdateClientRequest
{
    public string Name { get; set; } = default!;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Status { get; set; }
}

public sealed class ClientResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Code { get; set; } = default!;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string Status { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class CreateClientRequestValidator : AbstractValidator<CreateClientRequest>
{
    private static readonly string[] AllowedStatuses = { "ACTIVE", "INACTIVE" };
    public CreateClientRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email)).MaximumLength(255);
        RuleFor(x => x.Phone).MaximumLength(50);
        RuleFor(x => x.Status).Must(s => s is null || AllowedStatuses.Contains(s))
            .WithMessage("Status must be ACTIVE or INACTIVE.");
    }
}

public sealed class UpdateClientRequestValidator : AbstractValidator<UpdateClientRequest>
{
    private static readonly string[] AllowedStatuses = { "ACTIVE", "INACTIVE" };
    public UpdateClientRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email)).MaximumLength(255);
        RuleFor(x => x.Phone).MaximumLength(50);
        RuleFor(x => x.Status).Must(s => s is null || AllowedStatuses.Contains(s))
            .WithMessage("Status must be ACTIVE or INACTIVE.");
    }
}

public interface IClientService
{
    Task<IReadOnlyList<ClientResponse>> ListAsync(CancellationToken ct = default);
    Task<ClientResponse> GetAsync(Guid clientId, CancellationToken ct = default);
    Task<ClientResponse> CreateAsync(CreateClientRequest request, CancellationToken ct = default);
    Task<ClientResponse> UpdateAsync(Guid clientId, UpdateClientRequest request, CancellationToken ct = default);
}
