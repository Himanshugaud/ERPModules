using ERP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class MaterialRequirementConfiguration : IEntityTypeConfiguration<MaterialRequirement>
{
    public void Configure(EntityTypeBuilder<MaterialRequirement> b)
    {
        b.ToTable("MaterialRequirements", "inventory");
        b.HasKey(x => x.Id);
        b.Property(x => x.ReqNumber).HasMaxLength(50);
        b.Property(x => x.Priority).HasMaxLength(20);
        b.Property(x => x.Status).HasMaxLength(30);
        b.Property(x => x.DestinationAddress).HasMaxLength(300);
        b.Property(x => x.RejectionReason).HasMaxLength(500);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.MaterialRequirementId);
    }
}

public sealed class MaterialRequirementLineConfiguration : IEntityTypeConfiguration<MaterialRequirementLine>
{
    public void Configure(EntityTypeBuilder<MaterialRequirementLine> b)
    {
        b.ToTable("MaterialRequirementLines", "inventory");
        b.HasKey(x => x.Id);
        b.Property(x => x.Qty).HasColumnType("decimal(19,4)");
        b.Property(x => x.Notes).HasMaxLength(500);
    }
}

public sealed class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> b)
    {
        b.ToTable("PurchaseOrders", "inventory");
        b.HasKey(x => x.Id);
        b.Property(x => x.PoNumber).HasMaxLength(50);
        b.Property(x => x.Status).HasMaxLength(30);
        b.Property(x => x.CurrencyCode).HasMaxLength(3);
        b.Property(x => x.SubTotal).HasColumnType("decimal(19,4)");
        b.Property(x => x.TaxAmount).HasColumnType("decimal(19,4)");
        b.Property(x => x.TotalAmount).HasColumnName("TotalAmount").HasColumnType("decimal(19,4)");
        b.Property(x => x.RejectionReason).HasMaxLength(500);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.PurchaseOrderId);
    }
}

public sealed class PurchaseOrderLineConfiguration : IEntityTypeConfiguration<PurchaseOrderLine>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderLine> b)
    {
        b.ToTable("PurchaseOrderLines", "inventory");
        b.HasKey(x => x.Id);
        b.Property(x => x.Qty).HasColumnType("decimal(19,4)");
        b.Property(x => x.UnitPrice).HasColumnType("decimal(19,4)");
        b.Property(x => x.TaxRatePercent).HasColumnType("decimal(9,4)");
        b.Property(x => x.TaxAmount).HasColumnType("decimal(19,4)");
        b.Property(x => x.LineTotal).HasColumnType("decimal(19,4)");
        b.Property(x => x.QtyReceived).HasColumnType("decimal(19,4)");
    }
}
