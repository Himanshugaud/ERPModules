using ERP.Application.Abstractions;
using ERP.Application.Inventory;
using ERP.Domain.Entities;
using ERP.Shared.Exceptions;
using Moq;
using Xunit;

namespace ERP.Security.Tests;

public sealed class GoodsReceiptTests
{
    [Fact]
    public async Task EnteringExistingPoNumberAsManualReceiptCannotBypassApproval()
    {
        var itemId = Guid.NewGuid();
        var order = Order(itemId, 10);
        var receipt = Receipt(order, itemId, 1);
        var ledger = new Mock<IStockLedger>(MockBehavior.Strict);
        await Assert.ThrowsAsync<ConflictException>(() => Service(receipt, order, ledger.Object).CreateAsync(new CreateGoodsReceiptRequest
        {
            WarehouseId = receipt.WarehouseId, PoReference = order.PoNumber,
            Lines = new() { new() { ItemId = itemId, Qty = 1, UnitCost = 1 } }
        }));
        ledger.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("ORDERED", true)]
    [InlineData("APPROVED", false)]
    public async Task CreatingPoReceiptRequiresOrderedStatusAndNeverPostsStock(string status, bool accepted)
    {
        var itemId = Guid.NewGuid();
        var order = Order(itemId, 10);
        order.Status = status;
        order.Lines[0].UnitPrice = 1;
        var receipt = Receipt(order, itemId, 1);
        var ledger = new Mock<IStockLedger>(MockBehavior.Strict);
        var request = new CreateGoodsReceiptRequest
        {
            WarehouseId = receipt.WarehouseId, PurchaseOrderId = order.Id, PoReference = order.PoNumber,
            Lines = new() { new() { ItemId = itemId, Qty = 1, UnitCost = 1 } }
        };
        if (accepted)
        {
            var result = await Service(receipt, order, ledger.Object).CreateAsync(request);
            Assert.Equal("PENDING_APPROVAL", result.Status);
        }
        else await Assert.ThrowsAsync<ConflictException>(() => Service(receipt, order, ledger.Object).CreateAsync(request));
        Assert.Equal(0, order.Lines[0].QtyReceived);
        ledger.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ApprovedButUnorderedPurchaseOrderCannotBeReceived()
    {
        var itemId = Guid.NewGuid();
        var order = Order(itemId, 10);
        order.Status = "APPROVED";
        var receipt = Receipt(order, itemId, 1);
        await Assert.ThrowsAsync<ConflictException>(() => Service(receipt, order).ApproveAsync(receipt.Id));
        Assert.Equal("PENDING_APPROVAL", receipt.Status);
        Assert.Equal(0, order.Lines[0].QtyReceived);
    }

    [Fact]
    public async Task ApprovingReceiptPostsStockExactlyOnce()
    {
        var itemId = Guid.NewGuid();
        var order = Order(itemId, 10);
        var receipt = Receipt(order, itemId, 10);
        var ledger = new Mock<IStockLedger>();
        var service = Service(receipt, order, ledger.Object);
        await service.ApproveAsync(receipt.Id);
        await Assert.ThrowsAsync<ConflictException>(() => service.ApproveAsync(receipt.Id));
        Assert.Equal("POSTED", receipt.Status);
        Assert.Equal("RECEIVED", order.Status);
        ledger.Verify(stock => stock.ReceiveAsync(itemId, receipt.WarehouseId, 10, 1, It.IsAny<string>(), It.IsAny<string>(), receipt.Id,
            It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RejectingReceiptDoesNotPostStock()
    {
        var itemId = Guid.NewGuid();
        var order = Order(itemId, 10);
        var receipt = Receipt(order, itemId, 10);
        var ledger = new Mock<IStockLedger>(MockBehavior.Strict);
        await Service(receipt, order, ledger.Object).RejectAsync(receipt.Id);
        Assert.Equal("REJECTED", receipt.Status);
        Assert.Equal(0, order.Lines[0].QtyReceived);
        ledger.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DuplicateItemLinesCannotExceedTotalOpenPurchaseQuantity()
    {
        var itemId = Guid.NewGuid();
        var purchaseOrder = Order(itemId, 10);
        var receipt = Receipt(purchaseOrder, itemId, 6, 6);
        var service = Service(receipt, purchaseOrder);

        await Assert.ThrowsAsync<ConflictException>(() => service.ApproveAsync(receipt.Id));
        Assert.Equal("PENDING_APPROVAL", receipt.Status);
        Assert.All(purchaseOrder.Lines, line => Assert.Equal(0, line.QtyReceived));
    }

    [Fact]
    public async Task ReceiptAllocatesAcrossPurchaseLinesWithoutOverfillingAnyLine()
    {
        var itemId = Guid.NewGuid();
        var purchaseOrder = Order(itemId, 3, 7);
        var receipt = Receipt(purchaseOrder, itemId, 8);

        await Service(receipt, purchaseOrder).ApproveAsync(receipt.Id);

        Assert.Equal(8, purchaseOrder.Lines.Sum(line => line.QtyReceived));
        Assert.All(purchaseOrder.Lines, line => Assert.InRange(line.QtyReceived, 0, line.Qty));
        Assert.Equal("PARTIALLY_RECEIVED", purchaseOrder.Status);
    }

    private static PurchaseOrder Order(Guid itemId, params decimal[] quantities) => new()
    {
        Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid(), Status = "ORDERED", PoNumber = "PO-TEST",
        Lines = quantities.Select(quantity => new PurchaseOrderLine
        {
            Id = Guid.NewGuid(), ItemId = itemId, Qty = quantity
        }).ToList()
    };

    private static GoodsReceipt Receipt(PurchaseOrder order, Guid itemId, params decimal[] quantities) => new()
    {
        Id = Guid.NewGuid(), OrganizationId = order.OrganizationId, PurchaseOrderId = order.Id,
        Status = "PENDING_APPROVAL", WarehouseId = Guid.NewGuid(),
        Lines = quantities.Select(quantity => new GoodsReceiptLine
        {
            Id = Guid.NewGuid(), ItemId = itemId, Qty = quantity, UnitCost = 1
        }).ToList()
    };

    private static GoodsReceiptService Service(GoodsReceipt receipt, PurchaseOrder order, IStockLedger? ledger = null)
    {
        var receipts = new Mock<IGoodsReceiptRepository>();
        receipts.Setup(repository => repository.GetTrackedAsync(order.OrganizationId, receipt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(receipt);
        var orders = new Mock<IPurchaseOrderRepository>();
        orders.Setup(repository => repository.GetAsync(order.OrganizationId, order.Id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        orders.Setup(repository => repository.NumberExistsAsync(order.OrganizationId, order.PoNumber, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var warehouses = new Mock<IWarehouseRepository>();
        warehouses.Setup(repository => repository.ExistsAsync(order.OrganizationId, receipt.WarehouseId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var items = new Mock<IItemRepository>();
        items.Setup(repository => repository.ExistsAsync(order.OrganizationId, It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var tenant = new Mock<ITenantContext>();
        tenant.SetupGet(context => context.OrganizationId).Returns(order.OrganizationId);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(unit => unit.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task> action, CancellationToken cancellation) => action(cancellation));
        return new GoodsReceiptService(receipts.Object, warehouses.Object, Mock.Of<ISupplierRepository>(),
            items.Object, Mock.Of<IBatchRepository>(), orders.Object, ledger ?? Mock.Of<IStockLedger>(),
            tenant.Object, unitOfWork.Object, Mock.Of<IAuditWriter>(), Mock.Of<IOutboxWriter>(), Mock.Of<IClock>());
    }
}