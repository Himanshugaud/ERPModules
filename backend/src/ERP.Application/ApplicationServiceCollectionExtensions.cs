using ERP.Application.Clients;
using ERP.Application.Identity;
using ERP.Application.Inventory;
using ERP.Application.Projects;
using ERP.Application.Tasks;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<CreateProjectRequestValidator>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IProjectMemberService, ProjectMemberService>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IClientService, ClientService>();

        // Inventory
        services.AddScoped<IStockLedger, StockLedger>();
        services.AddScoped<IItemService, ItemService>();
        services.AddScoped<IWarehouseService, WarehouseService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IInventoryLookupService, InventoryLookupService>();
        services.AddScoped<IGoodsReceiptService, GoodsReceiptService>();
        services.AddScoped<IMaterialIssueService, MaterialIssueService>();
        services.AddScoped<IStockTransferService, StockTransferService>();
        services.AddScoped<IStockAdjustmentService, StockAdjustmentService>();
        services.AddScoped<IBomService, BomService>();
        services.AddScoped<IWorkOrderService, WorkOrderService>();
        return services;
    }
}
