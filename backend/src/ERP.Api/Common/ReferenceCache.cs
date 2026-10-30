using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace ERP.Api.Common;

/// <summary>
/// Short-lived per-organization cache for reference data that rarely changes (statuses, categories, catalog…).
/// Each group has a version so a write can drop everything cached for it in one call.
/// </summary>
public sealed class ReferenceCache : IDisposable
{
    private readonly MemoryCache _entries = new(new MemoryCacheOptions { SizeLimit = 2048 });
    private readonly ConcurrentDictionary<string, Lazy<Task<object?>>> _loads = new();
    private readonly ConcurrentDictionary<string, int> _versions = new();

    public async Task<T> GetAsync<T>(Guid organizationId, string group, string key, TimeSpan ttl, Func<Task<T>> load)
    {
        var cacheKey = $"{organizationId}:{group}:{_versions.GetValueOrDefault($"{organizationId}:{group}")}:{key}";
        if (_entries.TryGetValue(cacheKey, out object? hit)) return (T)hit!;

        var pending = _loads.GetOrAdd(cacheKey, _ => new Lazy<Task<object?>>(async () =>
        {
            if (_entries.TryGetValue(cacheKey, out object? cached)) return cached;
            var value = await load();
            _entries.Set(cacheKey, value, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl, Size = 1 });
            return value;
        }));
        try
        {
            return (T)(await pending.Value)!;
        }
        finally
        {
            _loads.TryRemove(new KeyValuePair<string, Lazy<Task<object?>>>(cacheKey, pending));
        }
    }

    public void Invalidate(Guid organizationId, params string[] groups)
    {
        foreach (var g in groups) _versions.AddOrUpdate($"{organizationId}:{g}", 1, (_, v) => v + 1);
    }

    public void Dispose() => _entries.Dispose();
}

public static class CacheGroups
{
    public const string Lookups = "lookups";
    public const string Items = "items";
    public const string Warehouses = "warehouses";
}
