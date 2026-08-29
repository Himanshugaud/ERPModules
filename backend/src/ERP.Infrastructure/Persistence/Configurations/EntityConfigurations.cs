using ERP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> b)
    {
        b.ToTable("Organizations", "core");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(50);
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.CurrencyCode).HasColumnType("char(3)");
    }
}

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("Users", "core");
        b.HasKey(x => x.Id);
        b.Property(x => x.Email).HasMaxLength(255);
    }
}

public sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> b)
    {
        b.ToTable("Clients", "core");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Code).HasMaxLength(50);
    }
}

public sealed class ProjectStatusConfiguration : IEntityTypeConfiguration<ProjectStatus>
{
    public void Configure(EntityTypeBuilder<ProjectStatus> b)
    {
        b.ToTable("ProjectStatuses", "project");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(50);
        b.Property(x => x.Name).HasMaxLength(100);
    }
}

public sealed class ProjectPriorityConfiguration : IEntityTypeConfiguration<ProjectPriority>
{
    public void Configure(EntityTypeBuilder<ProjectPriority> b)
    {
        b.ToTable("ProjectPriorities", "project");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(50);
        b.Property(x => x.Name).HasMaxLength(100);
    }
}

public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> b)
    {
        b.ToTable("Projects", "project");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(50);
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.CompletionPercentage).HasColumnType("decimal(5,2)");
        b.Property(x => x.Budget).HasColumnType("decimal(19,4)");
        b.Property(x => x.CurrencyCode).HasColumnType("char(3)");
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public sealed class ProjectMemberConfiguration : IEntityTypeConfiguration<ProjectMember>
{
    public void Configure(EntityTypeBuilder<ProjectMember> b)
    {
        b.ToTable("ProjectMembers", "project");
        b.HasKey(x => new { x.ProjectId, x.UserId });
        b.Property(x => x.AllocationPercentage).HasColumnType("decimal(5,2)");
    }
}

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("AuditLogs", "shared");
        b.HasKey(x => x.Id);
        b.Property(x => x.EntityType).HasMaxLength(50);
        b.Property(x => x.Action).HasMaxLength(30);
    }
}

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> b)
    {
        b.ToTable("OutboxMessages", "shared");
        b.HasKey(x => x.Id);
        b.Property(x => x.EventType).HasMaxLength(100);
        b.Property(x => x.AggregateType).HasMaxLength(100);
    }
}
