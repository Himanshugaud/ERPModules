using ERP.Domain.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Services;
using ERP.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.Security.Tests;

public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly string _database = $"ERP_Audit_Test_{Guid.NewGuid():N}";
    private bool _created;
    public ErpDbContext Open() => new(new DbContextOptionsBuilder<ErpDbContext>()
        .UseSqlServer($"Server=(localdb)\\MSSQLLocalDB;Database={_database};Integrated Security=true;TrustServerCertificate=true;Connect Timeout=15")
        .Options);

    public async Task InitializeAsync()
    {
        await using var database = Open();
        await database.Database.EnsureCreatedAsync();
        _created = true;
    }

    public async Task DisposeAsync()
    {
        if (!_created) return;
        await using var database = Open();
        await database.Database.EnsureDeletedAsync();
    }
}

public sealed class SqlServerConcurrencyTests : IClassFixture<SqlServerFixture>
{
    private readonly SqlServerFixture _fixture;
    public SqlServerConcurrencyTests(SqlServerFixture fixture) => _fixture = fixture;

    [Fact]
    [Trait("Category", "SqlServer")]
    public async Task ConcurrentReceiptApprovalAndRejectionCannotBothCommit()
    {
        var receipt = new GoodsReceipt
        {
            Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid(), GrnNumber = Guid.NewGuid().ToString(),
            WarehouseId = Guid.NewGuid(), Status = "PENDING_APPROVAL", CreatedAt = DateTime.UtcNow
        };
        await using var setup = _fixture.Open();
        setup.GoodsReceipts.Add(receipt);
        await setup.SaveChangesAsync();
        await using var first = _fixture.Open();
        await using var second = _fixture.Open();
        var approval = await first.GoodsReceipts.SingleAsync(document => document.Id == receipt.Id);
        var rejection = await second.GoodsReceipts.SingleAsync(document => document.Id == receipt.Id);
        approval.Status = "POSTED";
        rejection.Status = "REJECTED";

        var outcomes = await Task.WhenAll(TryCommit(first), TryCommit(second));

        Assert.Single(outcomes, committed => committed);
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public async Task DuplicateTransferDispatchCannotBothCommit()
    {
        var transfer = new StockTransfer
        {
            Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid(), TransferNumber = Guid.NewGuid().ToString(),
            ToWarehouseId = Guid.NewGuid(), Status = "APPROVED", CreatedAt = DateTime.UtcNow
        };
        await using var setup = _fixture.Open();
        setup.StockTransfers.Add(transfer);
        await setup.SaveChangesAsync();
        await using var first = _fixture.Open();
        await using var second = _fixture.Open();
        var firstDispatch = await first.StockTransfers.SingleAsync(document => document.Id == transfer.Id);
        var secondDispatch = await second.StockTransfers.SingleAsync(document => document.Id == transfer.Id);
        firstDispatch.Status = "DISPATCHED";
        secondDispatch.Status = "DISPATCHED";

        var outcomes = await Task.WhenAll(TryCommit(first), TryCommit(second));

        Assert.Single(outcomes, committed => committed);
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public async Task StockRowVersionRejectsLostUpdateAndRollsBackLosingLedgerWrite()
    {
        var level = new StockLevel
        {
            Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid(), ItemId = Guid.NewGuid(),
            WarehouseId = Guid.NewGuid(), QtyOnHand = 10, AvgUnitCost = 5
        };
        await using var setup = _fixture.Open();
        setup.StockLevels.Add(level);
        await setup.SaveChangesAsync();
        await using var first = _fixture.Open();
        await using var second = _fixture.Open();
        var firstLevel = await first.StockLevels.SingleAsync(stock => stock.Id == level.Id);
        var secondLevel = await second.StockLevels.SingleAsync(stock => stock.Id == level.Id);
        foreach (var pair in new[] { (Database: first, Level: firstLevel), (Database: second, Level: secondLevel) })
        {
            pair.Level.QtyOnHand -= 7;
            pair.Database.StockMovements.Add(new StockMovement
            {
                Id = Guid.NewGuid(), OrganizationId = level.OrganizationId, ItemId = level.ItemId,
                WarehouseId = level.WarehouseId, MovementType = "MATERIAL_ISSUE", Direction = "OUT",
                Qty = 7, UnitCost = 5, TotalCost = 35, OccurredAt = DateTime.UtcNow
            });
        }

        var outcomes = await Task.WhenAll(TryCommit(first), TryCommit(second));

        Assert.Single(outcomes, committed => committed);
        await using var verify = _fixture.Open();
        Assert.Equal(3, (await verify.StockLevels.SingleAsync(stock => stock.Id == level.Id)).QtyOnHand);
        Assert.Equal(1, await verify.StockMovements.CountAsync(movement => movement.ItemId == level.ItemId));
    }

    private static async Task<bool> TryCommit(ErpDbContext database)
    {
        var unitOfWork = new UnitOfWork(database);
        try
        {
            await unitOfWork.ExecuteInTransactionAsync(async cancellation =>
            {
                await unitOfWork.SaveChangesAsync(cancellation);
            });
            return true;
        }
        catch (ConcurrencyException)
        {
            return false;
        }
    }
}