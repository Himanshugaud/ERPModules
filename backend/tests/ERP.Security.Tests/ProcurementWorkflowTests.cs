using ERP.Application.Abstractions;
using ERP.Application.Inventory;
using ERP.Application.Projects;
using ERP.Domain.Entities;
using ERP.Shared.Exceptions;
using Moq;
using Xunit;

namespace ERP.Security.Tests;

public sealed class ProcurementWorkflowTests
{
    [Fact]
    public async Task NewDraftHasNoSupplierProjectOrderDateOrPrices()
    {
        var fixture = new Fixture();
        var result = await fixture.Service.CreateAsync(new CreatePurchaseOrderRequest
        {
            Lines = new() { new() { ItemId = fixture.ItemId, Qty = 2, UnitPrice = 500, TaxRatePercent = 18 } }
        });
        Assert.Equal("DRAFT", result.Status);
        Assert.Null(result.SupplierId);
        Assert.Null(result.ProjectId);
        Assert.Null(result.OrderDate);
        Assert.Equal(0, result.TotalAmount);
        Assert.Equal(0, Assert.Single(result.Lines).UnitPrice);
    }

    [Fact]
    public void DraftValidatorsAllowSupplierAndPriceToBeDeferred()
    {
        var item = Guid.NewGuid();
        Assert.True(new CreatePurchaseOrderRequestValidator().Validate(new CreatePurchaseOrderRequest
        {
            Lines = new() { new() { ItemId = item, Qty = 2 } }
        }).IsValid);
        Assert.True(new ConvertToPurchaseOrderRequestValidator().Validate(new ConvertToPurchaseOrderRequest
        {
            Lines = new() { new() { MaterialRequirementLineId = item, Qty = 2 } }
        }).IsValid);
    }

    [Fact]
    public async Task DraftEditPreservesLinkedLineIdentity()
    {
        var fixture = new Fixture();
        var line = Assert.Single(fixture.Order.Lines);
        fixture.LinkRequirement(line);
        var result = await fixture.Service.UpdateAsync(fixture.Order.Id, new UpdatePurchaseOrderRequest
        {
            Lines = new() { new() { Id = line.Id, ItemId = line.ItemId, Qty = 1, UnitPrice = 999 } }
        });
        Assert.Equal(line.Id, Assert.Single(result.Lines).Id);
        Assert.Equal(line.MaterialRequirementLineId, result.Lines[0].MaterialRequirementLineId);
        Assert.Equal(line.Id, Assert.Single(fixture.Requirement.Lines).PurchaseOrderLineId);
        Assert.Equal(0, result.Lines[0].UnitPrice);
    }

    [Fact]
    public async Task RemovingDraftLineReopensItsMaterialRequirement()
    {
        var fixture = new Fixture();
        fixture.LinkRequirement(Assert.Single(fixture.Order.Lines));
        await fixture.Service.UpdateAsync(fixture.Order.Id, new UpdatePurchaseOrderRequest
        {
            Lines = new() { new() { ItemId = fixture.ItemId, Qty = 1 } }
        });
        Assert.Null(Assert.Single(fixture.Requirement.Lines).PurchaseOrderLineId);
        Assert.Equal("APPROVED", fixture.Requirement.Status);
    }

    [Fact]
    public async Task OrderingRequiresSupplierAndPricesAndComputesTotals()
    {
        var fixture = new Fixture();
        fixture.Order.Status = "APPROVED";
        var result = await fixture.Service.MarkOrderedAsync(fixture.Order.Id, fixture.OrderRequest());
        Assert.Equal("ORDERED", result.Status);
        Assert.Equal(fixture.SupplierId, result.SupplierId);
        Assert.Equal(new DateOnly(2026, 10, 30), result.OrderDate);
        Assert.Equal(200, result.SubTotal);
        Assert.Equal(36, result.TaxAmount);
        Assert.Equal(236, result.TotalAmount);
    }

    [Theory]
    [InlineData("supplier")]
    [InlineData("price")]
    [InlineData("missing-line")]
    [InlineData("foreign-line")]
    [InlineData("stale")]
    public async Task InvalidOrderDetailsDoNotChangeApprovedOrder(string failure)
    {
        var fixture = new Fixture();
        fixture.Order.Status = "APPROVED";
        var request = fixture.OrderRequest();
        if (failure == "supplier") request.SupplierId = Guid.NewGuid();
        if (failure == "price") request.Lines[0].UnitPrice = 0;
        if (failure == "missing-line") request.Lines.Clear();
        if (failure == "foreign-line") request.Lines[0].LineId = Guid.NewGuid();
        if (failure == "stale") request.RowVersion = "stale";
        await Assert.ThrowsAnyAsync<AppException>(() => fixture.Service.MarkOrderedAsync(fixture.Order.Id, request));
        Assert.Equal("APPROVED", fixture.Order.Status);
        Assert.Null(fixture.Order.SupplierId);
        Assert.Equal(0, fixture.Order.TotalAmount);
    }

    [Fact]
    public async Task MaterialListCreatesOnlyUnpricedIndependentDraft()
    {
        var fixture = new Fixture();
        var line = Assert.Single(fixture.Order.Lines);
        fixture.LinkRequirement(line);
        fixture.Requirement.Lines[0].PurchaseOrderLineId = null;
        fixture.Requirement.Status = "APPROVED";
        var service = new MaterialRequirementService(fixture.Requirements.Object, fixture.Orders.Object,
            Mock.Of<IWarehouseRepository>(), Mock.Of<ISupplierRepository>(), fixture.Items.Object,
            Mock.Of<IProjectStatusAdvancer>(), fixture.Tenant.Object, fixture.Unit.Object,
            Mock.Of<IAuditWriter>(), Mock.Of<IOutboxWriter>(), fixture.Clock.Object);
        var result = await service.ConvertToPurchaseOrderAsync(fixture.Requirement.Id, new ConvertToPurchaseOrderRequest
        {
            Lines = new() { new() { MaterialRequirementLineId = fixture.Requirement.Lines[0].Id, Qty = 1, UnitPrice = 99 } }
        });
        Assert.Equal("DRAFT", result.Status);
        Assert.Null(result.SupplierId);
        Assert.Null(result.ProjectId);
        Assert.Equal(fixture.Requirement.Id, result.MaterialRequirementId);
        Assert.Equal(0, result.TotalAmount);
    }

    [Theory]
    [InlineData(null, 10, false)]
    [InlineData("warehouse", 1, false)]
    [InlineData("warehouse", 10, true)]
    public async Task ReadyToShipRequiresAvailableWarehouseStockButNotVehicle(string? source, int available, bool accepted)
    {
        var fixture = new Fixture();
        var transfer = new StockTransfer
        {
            Id = Guid.NewGuid(), MaterialRequirementId = Guid.NewGuid(), Status = "REQUESTED",
            FromWarehouseId = source is null ? null : Guid.NewGuid(),
            Lines = new() { new() { ItemId = fixture.ItemId, Qty = 2 }, new() { ItemId = fixture.ItemId, Qty = 2 } }
        };
        var transfers = new Mock<IStockTransferRepository>();
        transfers.Setup(repository => repository.GetAsync(fixture.OrganizationId, transfer.Id, true, It.IsAny<CancellationToken>())).ReturnsAsync(transfer);
        var levels = new Mock<IStockLevelRepository>();
        levels.Setup(repository => repository.GetAsync(fixture.OrganizationId, fixture.ItemId, It.IsAny<Guid>(), null, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StockLevel { QtyOnHand = available });
        var service = new StockTransferService(transfers.Object, Mock.Of<IWarehouseRepository>(), fixture.Items.Object,
            Mock.Of<IStockLedger>(), Mock.Of<IProjectStatusAdvancer>(), fixture.Tenant.Object, fixture.Unit.Object,
            Mock.Of<IAuditWriter>(), Mock.Of<IOutboxWriter>(), fixture.Clock.Object, fixture.Requirements.Object,
            Mock.Of<IProjectRepository>(), levels.Object);
        if (accepted)
        {
            var result = await service.ApproveAsync(transfer.Id);
            Assert.Equal("APPROVED", result.Status);
            Assert.Null(transfer.VehicleNumber);
        }
        else
        {
            await Assert.ThrowsAsync<ConflictException>(() => service.ApproveAsync(transfer.Id));
            Assert.Equal("REQUESTED", transfer.Status);
        }
    }

    private sealed class Fixture
    {
        public Guid OrganizationId { get; } = Guid.NewGuid();
        public Guid ItemId { get; } = Guid.NewGuid();
        public Guid SupplierId { get; } = Guid.NewGuid();
        public PurchaseOrder Order { get; }
        public MaterialRequirement Requirement { get; } = new() { Id = Guid.NewGuid(), ProjectId = Guid.NewGuid(), Status = "CONVERTED" };
        public Mock<IPurchaseOrderRepository> Orders { get; } = new();
        public Mock<IMaterialRequirementRepository> Requirements { get; } = new();
        public Mock<IItemRepository> Items { get; } = new();
        public Mock<ITenantContext> Tenant { get; } = new();
        public Mock<IUnitOfWork> Unit { get; } = new();
        public Mock<IClock> Clock { get; } = new();
        public PurchaseOrderService Service { get; }

        public Fixture()
        {
            Order = new PurchaseOrder
            {
                Id = Guid.NewGuid(), OrganizationId = OrganizationId, Status = "DRAFT", RowVersion = new byte[] { 1 },
                Lines = new() { new() { Id = Guid.NewGuid(), ItemId = ItemId, Qty = 2 } }
            };
            Orders.Setup(repository => repository.GetAsync(OrganizationId, Order.Id, true, It.IsAny<CancellationToken>())).ReturnsAsync(Order);
            Requirements.Setup(repository => repository.GetAsync(OrganizationId, Requirement.Id, true, It.IsAny<CancellationToken>())).ReturnsAsync(Requirement);
            Items.Setup(repository => repository.ExistsAsync(OrganizationId, ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var suppliers = new Mock<ISupplierRepository>();
            suppliers.Setup(repository => repository.ExistsAsync(OrganizationId, SupplierId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            Tenant.SetupGet(context => context.OrganizationId).Returns(OrganizationId);
            Clock.SetupGet(clock => clock.UtcNow).Returns(new DateTime(2026, 10, 30, 0, 0, 0, DateTimeKind.Utc));
            Unit.Setup(unit => unit.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
                .Returns((Func<CancellationToken, Task> action, CancellationToken cancellation) => action(cancellation));
            Service = new PurchaseOrderService(Orders.Object, suppliers.Object, Mock.Of<IWarehouseRepository>(), Items.Object,
                Tenant.Object, Unit.Object, Mock.Of<IAuditWriter>(), Mock.Of<IOutboxWriter>(), Clock.Object,
                Mock.Of<IProjectRepository>(), Mock.Of<IUnitOfMeasureRepository>(), Requirements.Object);
        }

        public void LinkRequirement(PurchaseOrderLine line)
        {
            Order.MaterialRequirementId = Requirement.Id;
            line.MaterialRequirementLineId = Guid.NewGuid();
            Requirement.Lines.Add(new MaterialRequirementLine { Id = line.MaterialRequirementLineId.Value, ItemId = line.ItemId, Qty = 2, PurchaseOrderLineId = line.Id });
        }

        public MarkPurchaseOrderOrderedRequest OrderRequest() => new()
        {
            SupplierId = SupplierId, CurrencyCode = "INR", RowVersion = Convert.ToBase64String(Order.RowVersion),
            Lines = Order.Lines.Select(line => new OrderPurchaseOrderLineRequest { LineId = line.Id, UnitPrice = 100, TaxRatePercent = 18 }).ToList()
        };
    }
}