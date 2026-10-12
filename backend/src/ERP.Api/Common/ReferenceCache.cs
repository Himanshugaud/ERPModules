using System.Collections.Concurrent;

namespace ERP.Api.Common;

/// <summary>
/// Short-lived per-organization cache for reference data that rarely changes (statuses, categories, catalog…).
/// Each group has a version so a write can drop everything cached for it in one call.
/// </summary>
public sealed class ReferenceCache
{
    private readonly ConcurrentDictionary<string, (DateTime Expires, object Value)> _entries = new();
    private readonly ConcurrentDictionary<string, int> _versions = new();

    public async Task<T> GetAsync<T>(Guid organizationId, string group, string key, TimeSpan ttl, Func<Task<T>> load)
    {
        var cacheKey = $"{organizationId}:{group}:{_versions.GetValueOrDefault($"{organizationId}:{group}")}:{key}";
        if (_entries.TryGetValue(cacheKey, out var hit) && hit.Expires > DateTime.UtcNow) return (T)hit.Value;

        var value = await load();
        _entries[cacheKey] = (DateTime.UtcNow.Add(ttl), value!);
        return value;
    }

    public void Invalidate(Guid organizationId, params string[] groups)
    {
        foreach (var g in groups) _versions.AddOrUpdate($"{organizationId}:{g}", 1, (_, v) => v + 1);
        // Superseded versions are never read again; drop anything expired so the map can't grow unbounded.
        var now = DateTime.UtcNow;
        foreach (var kv in _entries) if (kv.Value.Expires <= now) _entries.TryRemove(kv.Key, out _);
    }
}

public static class CacheGroups
{
    public const string Lookups = "lookups";
    public const string Items = "items";
    public const string Warehouses = "warehouses";
}
