using System.Text.Json;
using ERP.Shared.Models;
using ERP.Shared.Pagination;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Common;

public static class Http
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<T> ReadAsync<T>(HttpRequest req, CancellationToken ct)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<T>(req.Body, JsonOptions, ct)
                   ?? throw new ERP.Shared.Exceptions.ValidationException(
                       new Dictionary<string, string[]> { ["body"] = new[] { "Request body is required." } });
        }
        catch (JsonException)
        {
            throw new ERP.Shared.Exceptions.ValidationException(
                new Dictionary<string, string[]> { ["body"] = new[] { "Invalid JSON payload." } });
        }
    }

    public static async Task<T> ReadValidatedAsync<T>(HttpRequest req, IValidator<T> validator, CancellationToken ct)
    {
        var model = await ReadAsync<T>(req, ct);
        var result = await validator.ValidateAsync(model, ct);
        if (!result.IsValid)
        {
            var errors = result.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            throw new ERP.Shared.Exceptions.ValidationException(errors);
        }
        return model;
    }

    public static IActionResult Ok<T>(T data) => new OkObjectResult(ApiResponse<T>.Ok(data));

    public static IActionResult Created<T>(T data) => new ObjectResult(ApiResponse<T>.Ok(data)) { StatusCode = 201 };

    public static IActionResult NoContent() => new NoContentResult();

    public static IActionResult Paged<T>(PagedResult<T> result)
    {
        var response = new PagedResponse<T>
        {
            Data = result.Items,
            Pagination = new PaginationMeta
            {
                Page = result.Page,
                PageSize = result.PageSize,
                TotalItems = result.TotalItems,
                TotalPages = result.TotalPages
            }
        };
        return new OkObjectResult(response);
    }

    public static int? IntQuery(HttpRequest req, string key) =>
        int.TryParse(req.Query[key], out var v) ? v : null;

    public static Guid? GuidQuery(HttpRequest req, string key) =>
        Guid.TryParse(req.Query[key], out var v) ? v : null;

    public static DateOnly? DateQuery(HttpRequest req, string key) =>
        DateOnly.TryParse(req.Query[key], out var v) ? v : null;

    public static string? StringQuery(HttpRequest req, string key) =>
        req.Query.TryGetValue(key, out var v) ? v.ToString() : null;
}
