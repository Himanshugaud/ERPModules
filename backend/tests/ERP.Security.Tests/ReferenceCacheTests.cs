using ERP.Api.Common;
using Xunit;

namespace ERP.Security.Tests;

public sealed class ReferenceCacheTests
{
    [Fact]
    public async Task ConcurrentMissesShareOneLoader()
    {
        using var cache = new ReferenceCache();
        var organizationId = Guid.NewGuid();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var loads = 0;
        var requests = Enumerable.Range(0, 16).Select(_ => cache.GetAsync(organizationId, "items", "lookup", TimeSpan.FromMinutes(1), async () =>
        {
            Interlocked.Increment(ref loads);
            await gate.Task;
            return "synthetic-data";
        })).ToArray();
        gate.SetResult();

        Assert.All(await Task.WhenAll(requests), value => Assert.Equal("synthetic-data", value));
        Assert.Equal(1, loads);
    }

    [Fact]
    public async Task DifferentOrganizationsNeverShareValues()
    {
        using var cache = new ReferenceCache();
        var first = await cache.GetAsync(Guid.NewGuid(), "items", "lookup", TimeSpan.FromMinutes(1), () => Task.FromResult("first"));
        var second = await cache.GetAsync(Guid.NewGuid(), "items", "lookup", TimeSpan.FromMinutes(1), () => Task.FromResult("second"));

        Assert.Equal("first", first);
        Assert.Equal("second", second);
    }

    [Fact]
    public async Task FailedLoadDoesNotPoisonCache()
    {
        using var cache = new ReferenceCache();
        var organizationId = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetAsync<string>(organizationId, "items", "lookup",
            TimeSpan.FromMinutes(1), () => Task.FromException<string>(new InvalidOperationException("synthetic failure"))));

        Assert.Equal("recovered", await cache.GetAsync(organizationId, "items", "lookup", TimeSpan.FromMinutes(1), () => Task.FromResult("recovered")));
    }

    [Fact]
    public async Task InvalidationWhileLoadingCannotRepopulateCurrentVersionWithStaleData()
    {
        using var cache = new ReferenceCache();
        var organizationId = Guid.NewGuid();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stale = cache.GetAsync(organizationId, "items", "lookup", TimeSpan.FromMinutes(1), async () =>
        {
            await gate.Task;
            return "stale";
        });
        cache.Invalidate(organizationId, "items");
        await cache.GetAsync(organizationId, "items", "lookup", TimeSpan.FromMinutes(1), () => Task.FromResult("current"));
        gate.SetResult();
        await stale;

        Assert.Equal("current", await cache.GetAsync(organizationId, "items", "lookup", TimeSpan.FromMinutes(1), () => Task.FromResult("unexpected")));
    }
}