using ERP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> b)
    {
        b.ToTable("Documents", "shared");
        b.HasKey(x => x.Id);
        b.Property(x => x.EntityType).HasMaxLength(50);
        b.Property(x => x.Category).HasMaxLength(40);
        b.Property(x => x.FileName).HasMaxLength(400);
        b.Property(x => x.ContentType).HasMaxLength(200);
        b.Property(x => x.BlobContainer).HasMaxLength(200);
        b.Property(x => x.BlobPath).HasMaxLength(1000);
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.Status).HasMaxLength(30);
    }
}

public sealed class PlanEntryConfiguration : IEntityTypeConfiguration<PlanEntry>
{
    public void Configure(EntityTypeBuilder<PlanEntry> b)
    {
        b.ToTable("PlanEntries", "project");
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).HasMaxLength(200);
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.EntryType).HasMaxLength(30);
        b.Property(x => x.Status).HasMaxLength(30);
    }
}

public sealed class RequirementSheetTypeConfiguration : IEntityTypeConfiguration<RequirementSheetType>
{
    public void Configure(EntityTypeBuilder<RequirementSheetType> b)
    {
        b.ToTable("RequirementSheetTypes", "inventory");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(40);
        b.Property(x => x.Name).HasMaxLength(150);
        b.Property(x => x.Description).HasMaxLength(500);
    }
}

public sealed class RequirementSheetSectionConfiguration : IEntityTypeConfiguration<RequirementSheetSection>
{
    public void Configure(EntityTypeBuilder<RequirementSheetSection> b)
    {
        b.ToTable("RequirementSheetSections", "inventory");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(150);
    }
}

public sealed class RequirementSheetSectionItemConfiguration : IEntityTypeConfiguration<RequirementSheetSectionItem>
{
    public void Configure(EntityTypeBuilder<RequirementSheetSectionItem> b)
    {
        b.ToTable("RequirementSheetSectionItems", "inventory");
        b.HasKey(x => x.Id);
    }
}

public sealed class ProjectPlanConfiguration : IEntityTypeConfiguration<ProjectPlan>
{
    public void Configure(EntityTypeBuilder<ProjectPlan> b)
    {
        b.ToTable("ProjectPlans", "project");
        b.HasKey(x => x.ProjectId);
        b.Property(x => x.Status).HasMaxLength(20);
        b.Property(x => x.Notes).HasMaxLength(1000);
    }
}
