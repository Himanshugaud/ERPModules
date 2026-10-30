using ERP.Application.Abstractions;
using ERP.Application.Documents;
using ERP.Application.Identity;
using ERP.Application.Inventory;
using ERP.Domain.Entities;
using ERP.Shared.Exceptions;
using Moq;
using Xunit;

namespace ERP.Security.Tests;

public sealed class TenantIsolationTests
{
    [Theory]
    [InlineData(false, "project")]
    [InlineData(true, "project")]
    [InlineData(false, "warehouse")]
    [InlineData(true, "warehouse")]
    [InlineData(false, "uom")]
    [InlineData(true, "uom")]
    [InlineData(false, "local")]
    [InlineData(true, "local")]
    public async Task PurchaseOrderReferencesMustBelongToVerifiedOrganization(bool update, string reference)
    {
        var organizationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var uomId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var order = new PurchaseOrder { Id = Guid.NewGuid(), OrganizationId = organizationId, Status = "DRAFT", Notes = "Unchanged" };
        var orders = new Mock<IPurchaseOrderRepository>();
        orders.Setup(repository => repository.GetAsync(organizationId, order.Id, true, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var suppliers = new Mock<ISupplierRepository>();
        suppliers.Setup(repository => repository.ExistsAsync(organizationId, supplierId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var items = new Mock<IItemRepository>();
        items.Setup(repository => repository.ExistsAsync(organizationId, itemId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var warehouses = new Mock<IWarehouseRepository>();
        warehouses.Setup(repository => repository.ExistsAsync(organizationId, warehouseId, It.IsAny<CancellationToken>())).ReturnsAsync(reference != "warehouse");
        var projects = new Mock<IProjectRepository>();
        projects.Setup(repository => repository.GetByIdAsync(organizationId, projectId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference == "project" ? null : new Project { Id = projectId, OrganizationId = organizationId });
        var uoms = new Mock<IUnitOfMeasureRepository>();
        uoms.Setup(repository => repository.ExistsAsync(organizationId, uomId, It.IsAny<CancellationToken>())).ReturnsAsync(reference != "uom");
        var tenant = new Mock<ITenantContext>();
        tenant.SetupGet(context => context.OrganizationId).Returns(organizationId);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(unit => unit.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task> action, CancellationToken cancellation) => action(cancellation));
        var service = new PurchaseOrderService(orders.Object, suppliers.Object, warehouses.Object, items.Object, tenant.Object,
            unitOfWork.Object, Mock.Of<IAuditWriter>(), Mock.Of<IOutboxWriter>(), Mock.Of<IClock>(), projects.Object, uoms.Object,
            Mock.Of<IMaterialRequirementRepository>());
        var lines = new List<PurchaseOrderLineRequest> { new() { ItemId = itemId, UomId = uomId, Qty = 1, UnitPrice = 1 } };
        Func<Task<PurchaseOrderResponse>> action = update
            ? () => service.UpdateAsync(order.Id, new UpdatePurchaseOrderRequest
            {
                SupplierId = supplierId, ProjectId = projectId, WarehouseId = warehouseId, Lines = lines, Notes = "Changed"
            })
            : () => service.CreateAsync(new CreatePurchaseOrderRequest
            {
                SupplierId = supplierId, ProjectId = projectId, WarehouseId = warehouseId, Lines = lines, Notes = "Changed"
            });

        if (reference == "local")
        {
            var result = await action();
            Assert.Null(result.ProjectId);
            Assert.Equal(warehouseId, result.WarehouseId);
            Assert.Equal(uomId, Assert.Single(result.Lines).UomId);
        }
        else
        {
            await Assert.ThrowsAsync<ConflictException>(action);
            Assert.Equal("Unchanged", order.Notes);
            unitOfWork.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmployeeCannotReferenceForeignDepartment(bool update)
    {
        var organizationId = Guid.NewGuid();
        var user = new User { Id = Guid.NewGuid(), OrganizationId = organizationId };
        var users = new Mock<IUserRepository>();
        users.Setup(repository => repository.GetAsync(organizationId, user.Id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        var tenant = new Mock<ITenantContext>();
        tenant.SetupGet(context => context.OrganizationId).Returns(organizationId);
        var service = new UserService(users.Object, Mock.Of<IRoleRepository>(), tenant.Object,
            Mock.Of<IUnitOfWork>(), Mock.Of<IAuditWriter>(), Mock.Of<IClock>(), Mock.Of<IDepartmentRepository>());
        var foreignDepartmentId = Guid.NewGuid();

        if (update)
            await Assert.ThrowsAsync<ConflictException>(() => service.UpdateAsync(user.Id,
                new UpdateUserRequest { DepartmentId = foreignDepartmentId }));
        else
            await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(new CreateUserRequest
            {
                Email = "synthetic@example.test", Username = "synthetic", DepartmentId = foreignDepartmentId
            }));
        Assert.Null(user.DepartmentId);
    }

    [Fact]
    public async Task ForeignDocumentLookupNeverOpensBlob()
    {
        var organizationId = Guid.NewGuid();
        var foreignDocumentId = Guid.NewGuid();
        var tenant = new Mock<ITenantContext>();
        tenant.SetupGet(context => context.OrganizationId).Returns(organizationId);
        var documents = new Mock<IDocumentRepository>(MockBehavior.Strict);
        documents.Setup(repository => repository.GetAsync(organizationId, foreignDocumentId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Document?)null);
        var blobs = new Mock<IBlobStorage>(MockBehavior.Strict);
        var service = new DocumentService(documents.Object, Mock.Of<IProjectRepository>(), Mock.Of<IOrganizationRepository>(),
            blobs.Object, tenant.Object, Mock.Of<IUnitOfWork>(), Mock.Of<IAuditWriter>(), Mock.Of<IClock>(), Mock.Of<IProjectPlanRepository>());

        await Assert.ThrowsAsync<NotFoundException>(() => service.DownloadAsync(foreignDocumentId));
        blobs.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ForeignRoleCannotBeAssignedToLocalEmployee()
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var foreignRoleId = Guid.NewGuid();
        var users = new Mock<IUserRepository>();
        users.Setup(repository => repository.ExistsAsync(organizationId, userId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var tenant = new Mock<ITenantContext>();
        tenant.SetupGet(context => context.OrganizationId).Returns(organizationId);
        var service = new UserService(users.Object, Mock.Of<IRoleRepository>(), tenant.Object,
            Mock.Of<IUnitOfWork>(), Mock.Of<IAuditWriter>(), Mock.Of<IClock>(), Mock.Of<IDepartmentRepository>());

        await Assert.ThrowsAsync<ConflictException>(() => service.AssignRoleAsync(userId, foreignRoleId));
        users.Verify(repository => repository.AssignRole(It.IsAny<UserRole>()), Times.Never);
    }
}