using ERP.Application.Abstractions;
using ERP.Application.Inventory;
using ERP.Application.Projects;
using ERP.Application.Tasks;
using ERP.Domain.Entities;
using ERP.Shared.Exceptions;
using Moq;
using Xunit;

namespace ERP.Security.Tests;

public sealed class StaleWriteTests
{
    [Theory]
    [InlineData("AAAAAAAAAAE=", false)]
    [InlineData("not-base64", false)]
    [InlineData("AAAAAAAAAAI=", true)]
    [InlineData(null, true)]
    public async Task ProjectChecksSuppliedTokenBeforeMutation(string? token, bool accepted)
    {
        var organizationId = Guid.NewGuid();
        var project = new Project { Id = Guid.NewGuid(), Name = "Current", RowVersion = new byte[] { 0, 0, 0, 0, 0, 0, 0, 2 } };
        var projects = new Mock<IProjectRepository>();
        projects.Setup(repository => repository.GetByIdAsync(organizationId, project.Id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        var unitOfWork = new Mock<IUnitOfWork>(MockBehavior.Strict);
        unitOfWork.Setup(unit => unit.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var tenant = new Mock<ITenantContext>();
        tenant.SetupGet(context => context.OrganizationId).Returns(organizationId);
        var service = new ProjectService(projects.Object, Mock.Of<IClientRepository>(), Mock.Of<IUserRepository>(),
            Mock.Of<IProjectStatusRepository>(), Mock.Of<IProjectPriorityRepository>(), tenant.Object, unitOfWork.Object,
            Mock.Of<IAuditWriter>(), Mock.Of<IOutboxWriter>(), Mock.Of<IClock>());

        var request = new UpdateProjectRequest { Name = "New edit", RowVersion = token };
        if (accepted)
            Assert.Equal("New edit", (await service.UpdateAsync(project.Id, request)).Name);
        else
        {
            await Assert.ThrowsAsync<ConcurrencyException>(() => service.UpdateAsync(project.Id, request));
            Assert.Equal("Current", project.Name);
            unitOfWork.VerifyNoOtherCalls();
        }
    }

    [Theory]
    [InlineData("AAAAAAAAAAE=", false)]
    [InlineData("not-base64", false)]
    [InlineData("AAAAAAAAAAI=", true)]
    [InlineData(null, true)]
    public async Task ItemChecksSuppliedTokenBeforeMutation(string? token, bool accepted)
    {
        var organizationId = Guid.NewGuid();
        var item = new Item { Id = Guid.NewGuid(), Name = "Current", RowVersion = new byte[] { 0, 0, 0, 0, 0, 0, 0, 2 } };
        var items = new Mock<IItemRepository>();
        items.Setup(repository => repository.GetByIdAsync(organizationId, item.Id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);
        var unitOfWork = new Mock<IUnitOfWork>(MockBehavior.Strict);
        unitOfWork.Setup(unit => unit.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var tenant = new Mock<ITenantContext>();
        tenant.SetupGet(context => context.OrganizationId).Returns(organizationId);
        var service = new ItemService(items.Object, Mock.Of<IItemCategoryRepository>(), Mock.Of<IUnitOfMeasureRepository>(),
            tenant.Object, unitOfWork.Object, Mock.Of<IAuditWriter>(), Mock.Of<IOutboxWriter>(), Mock.Of<IClock>());

        var request = new UpdateItemRequest { Name = "New edit", RowVersion = token };
        if (accepted)
            Assert.Equal("New edit", (await service.UpdateAsync(item.Id, request)).Name);
        else
        {
            await Assert.ThrowsAsync<ConcurrencyException>(() => service.UpdateAsync(item.Id, request));
            Assert.Equal("Current", item.Name);
            unitOfWork.VerifyNoOtherCalls();
        }
    }

    [Theory]
    [InlineData("AAAAAAAAAAE=", false)]
    [InlineData("not-base64", false)]
    [InlineData("AAAAAAAAAAI=", true)]
    [InlineData(null, true)]
    public async Task TaskChecksSuppliedTokenBeforeMutation(string? token, bool accepted)
    {
        var organizationId = Guid.NewGuid();
        var task = new TaskItem { Id = Guid.NewGuid(), ProjectId = Guid.NewGuid(), Title = "Current", RowVersion = new byte[] { 0, 0, 0, 0, 0, 0, 0, 2 } };
        var tasks = new Mock<ITaskRepository>();
        tasks.Setup(repository => repository.GetByIdAsync(organizationId, task.Id, true, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        var projects = new Mock<IProjectRepository>();
        projects.Setup(repository => repository.GetByIdAsync(organizationId, task.ProjectId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Project { Id = task.ProjectId });
        var tenant = new Mock<ITenantContext>();
        tenant.SetupGet(context => context.OrganizationId).Returns(organizationId);
        var service = new TaskService(tasks.Object, Mock.Of<ITaskStatusRepository>(), Mock.Of<ITaskPriorityRepository>(),
            Mock.Of<ITaskWatcherRepository>(), projects.Object, Mock.Of<IProjectMemberRepository>(), Mock.Of<IUserRepository>(),
            Mock.Of<IMilestoneRepository>(), Mock.Of<ISprintRepository>(), tenant.Object, Mock.Of<IUnitOfWork>(),
            Mock.Of<IAuditWriter>(), Mock.Of<IOutboxWriter>(), Mock.Of<IClock>());
        var request = new UpdateTaskRequest { Title = "New edit", RowVersion = token };

        if (accepted)
            Assert.Equal("New edit", (await service.UpdateAsync(task.Id, request)).Title);
        else
        {
            await Assert.ThrowsAsync<ConcurrencyException>(() => service.UpdateAsync(task.Id, request));
            Assert.Equal("Current", task.Title);
        }
    }

    [Theory]
    [InlineData("AAAAAAAAAAE=", false)]
    [InlineData("not-base64", false)]
    [InlineData("AAAAAAAAAAI=", true)]
    [InlineData(null, true)]
    public async Task PurchaseOrderChecksSuppliedTokenBeforeMutation(string? token, bool accepted)
    {
        var organizationId = Guid.NewGuid();
        var order = new PurchaseOrder { Id = Guid.NewGuid(), Status = "DRAFT", Notes = "Current", RowVersion = new byte[] { 0, 0, 0, 0, 0, 0, 0, 2 } };
        var orders = new Mock<IPurchaseOrderRepository>();
        orders.Setup(repository => repository.GetAsync(organizationId, order.Id, true, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var suppliers = new Mock<ISupplierRepository>();
        suppliers.Setup(repository => repository.ExistsAsync(organizationId, It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var tenant = new Mock<ITenantContext>();
        tenant.SetupGet(context => context.OrganizationId).Returns(organizationId);
        var service = new PurchaseOrderService(orders.Object, suppliers.Object, Mock.Of<IWarehouseRepository>(), Mock.Of<IItemRepository>(),
            tenant.Object, Mock.Of<IUnitOfWork>(), Mock.Of<IAuditWriter>(), Mock.Of<IOutboxWriter>(), Mock.Of<IClock>(),
            Mock.Of<IProjectRepository>(), Mock.Of<IUnitOfMeasureRepository>(), Mock.Of<IMaterialRequirementRepository>());
        var request = new UpdatePurchaseOrderRequest { Notes = "New edit", RowVersion = token };

        if (accepted)
            Assert.Equal("New edit", (await service.UpdateAsync(order.Id, request)).Notes);
        else
        {
            await Assert.ThrowsAsync<ConcurrencyException>(() => service.UpdateAsync(order.Id, request));
            Assert.Equal("Current", order.Notes);
        }
    }
}