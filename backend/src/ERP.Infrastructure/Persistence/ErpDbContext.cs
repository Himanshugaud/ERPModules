using ERP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using TaskStatus = ERP.Domain.Entities.TaskStatus;

namespace ERP.Infrastructure.Persistence;

public sealed class ErpDbContext : DbContext
{
    public ErpDbContext(DbContextOptions<ErpDbContext> options) : base(options) { }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<ProjectStatus> ProjectStatuses => Set<ProjectStatus>();
    public DbSet<ProjectPriority> ProjectPriorities => Set<ProjectPriority>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<TaskStatus> TaskStatuses => Set<TaskStatus>();
    public DbSet<TaskPriority> TaskPriorities => Set<TaskPriority>();
    public DbSet<TaskWatcher> TaskWatchers => Set<TaskWatcher>();
    public DbSet<Milestone> Milestones => Set<Milestone>();
    public DbSet<Sprint> Sprints => Set<Sprint>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ErpDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
