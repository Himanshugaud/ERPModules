using ERP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskStatus = ERP.Domain.Entities.TaskStatus;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> b)
    {
        b.ToTable("Tasks", "project");
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).HasMaxLength(300);
        b.Property(x => x.EstimatedHours).HasColumnType("decimal(10,2)");
        b.Property(x => x.ActualHours).HasColumnType("decimal(10,2)");
        b.Property(x => x.CompletionPercentage).HasColumnType("decimal(5,2)");
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public sealed class TaskStatusConfiguration : IEntityTypeConfiguration<TaskStatus>
{
    public void Configure(EntityTypeBuilder<TaskStatus> b)
    {
        b.ToTable("TaskStatuses", "project");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(50);
        b.Property(x => x.Name).HasMaxLength(100);
    }
}

public sealed class TaskPriorityConfiguration : IEntityTypeConfiguration<TaskPriority>
{
    public void Configure(EntityTypeBuilder<TaskPriority> b)
    {
        b.ToTable("TaskPriorities", "project");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(50);
        b.Property(x => x.Name).HasMaxLength(100);
    }
}

public sealed class TaskWatcherConfiguration : IEntityTypeConfiguration<TaskWatcher>
{
    public void Configure(EntityTypeBuilder<TaskWatcher> b)
    {
        b.ToTable("TaskWatchers", "project");
        b.HasKey(x => new { x.TaskId, x.UserId });
    }
}

public sealed class MilestoneConfiguration : IEntityTypeConfiguration<Milestone>
{
    public void Configure(EntityTypeBuilder<Milestone> b)
    {
        b.ToTable("Milestones", "project");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.CompletionPercentage).HasColumnType("decimal(5,2)");
        b.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class SprintConfiguration : IEntityTypeConfiguration<Sprint>
{
    public void Configure(EntityTypeBuilder<Sprint> b)
    {
        b.ToTable("Sprints", "project");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200);
    }
}
