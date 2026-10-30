using ERP.Domain.Entities;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.Security.Tests;

public sealed class PersistenceModelTests
{
    [Theory]
    [InlineData(typeof(GoodsReceipt))]
    [InlineData(typeof(StockTransfer))]
    public void InventoryTransitionChecksOriginalStatus(Type entityType)
    {
        using var database = new ErpDbContext(new DbContextOptionsBuilder<ErpDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=NeverOpened;Integrated Security=true").Options);

        Assert.True(database.Model.FindEntityType(entityType)!.FindProperty("Status")!.IsConcurrencyToken);
    }
}