using ERP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class ItemCategoryConfiguration : IEntityTypeConfiguration<ItemCategory>
{
    public void Configure(EntityTypeBuilder<ItemCategory> b)
    {
        b.ToTable("ItemCategories", "inventory");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(50);
        b.Property(x => x.Name).HasMaxLength(150);
    }
}

public sealed class UnitOfMeasureConfiguration : IEntityTypeConfiguration<UnitOfMeasure>
{
    public void Configure(EntityTypeBuilder<UnitOfMeasure> b)
    {
        b.ToTable("UnitsOfMeasure", "inventory");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(20);
        b.Property(x => x.Name).HasMaxLength(100);
    }
}

public sealed class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> b)
    {
        b.ToTable("Items", "inventory");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(50);
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.ItemType).HasMaxLength(30);
        b.Property(x => x.ValuationMethod).HasMaxLength(20);
        b.Property(x => x.StandardCost).HasColumnType("decimal(19,4)");
        b.Property(x => x.ReorderLevel).HasColumnType("decimal(19,4)");
        b.Property(x => x.SafetyStock).HasColumnType("decimal(19,4)");
        b.Property(x => x.MinStock).HasColumnType("decimal(19,4)");
        b.Property(x => x.MaxStock).HasColumnType("decimal(19,4)");
        b.Property(x => x.ReorderQty).HasColumnType("decimal(19,4)");
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public sealed class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> b)
    {
        b.ToTable("Warehouses", "inventory");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(50);
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.WarehouseType).HasMaxLength(30);
    }
}

public sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> b)
    {
        b.ToTable("Suppliers", "inventory");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(50);
        b.Property(x => x.Name).HasMaxLength(200);
    }
}

public sealed class BatchConfiguration : IEntityTypeConfiguration<Batch>
{
    public void Configure(EntityTypeBuilder<Batch> b)
    {
        b.ToTable("Batches", "inventory");
        b.HasKey(x => x.Id);
        b.Property(x => x.BatchNo).HasMaxLength(100);
    }
}

public sealed class StockLevelConfiguration : IEntityTypeConfiguration<StockLevel>
{
    public void Configure(EntityTypeBuilder<StockLevel> b)
    {
        b.ToTable("StockLevels", "inventory");
        b.HasKey(x => x.Id);
        b.Property(x => x.QtyOnHand).HasColumnType("decimal(19,4)");
        b.Property(x => x.QtyReserved).HasColumnType("decimal(19,4)");
        b.Property(x => x.QtyInTransit).HasColumnType("decimal(19,4)");
        b.Property(x => x.AvgUnitCost).HasColumnType("decimal(19,4)");
        b.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> b)
    {
        b.ToTable("StockMovements", "inventory");
        b.HasKey(x => x.Id);
        b.Property(x => x.MovementType).HasMaxLength(30);
        b.Property(x => x.Direction).HasMaxLength(3);
        b.Property(x => x.RefDocType).HasMaxLength(30);
        b.Property(x => x.Qty).HasColumnType("decimal(19,4)");
        b.Property(x => x.UnitCost).HasColumnType("decimal(19,4)");
        b.Property(x => x.TotalCost).HasColumnType("decimal(19,4)");
    }
}

public sealed class GoodsReceiptConfiguration : IEntityTypeConfiguration<GoodsReceipt>
{
    public void Configure(EntityTypeBuilder<GoodsReceipt> b)
    {
        b.ToTable("GoodsReceipts", "inventory");
        b.HasKey(x => x.Id);
        b.Property(x => x.GrnNumber).HasMaxLength(50);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.GoodsReceiptId);
    }
}

public sealed class GoodsReceiptLineConfiguration : IEntityTypeConfiguration<GoodsReceiptLine>
{
    public void Configure(EntityTypeBuilder<GoodsReceiptLine> b)
    {
        b.ToTable("GoodsReceiptLines", "inventory");
        b.HasKey(x => x.Id);
        b.Property(x => x.Qty).HasColumnType("decimal(19,4)");
        b.Property(x => x.UnitCost).HasColumnType("decimal(19,4)");
    }
}

public sealed class MaterialIssueConfiguration : IEntityTypeConfiguration<MaterialIssue>
{
    public void Configure(EntityTypeBuilder<MaterialIssue> b)
    {
        b.ToTable("MaterialIssues", "inventory");
        b.HasKey(x => x.Id);
        b.Property(x => x.IssueNumber).HasMaxLength(50);
        b.Property(x => x.IssueType).HasMaxLength(30);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.MaterialIssueId);
    }
}

public sealed class MaterialIssueLineConfiguration : IEntityTypeConfiguration<MaterialIssueLine>
{
    public void Configure(EntityTypeBuilder<MaterialIssueLine> b)
    {
        b.ToTable("MaterialIssueLines", "inventory");
        b.HasKey(x => x.Id);
        b.Property(x => x.Qty).HasColumnType("decimal(19,4)");
        b.Property(x => x.UnitCost).HasColumnType("decimal(19,4)");
    }
}

public sealed class StockTransferConfiguration : IEntityTypeConfiguration<StockTransfer>
{
    public void Configure(EntityTypeBuilder<StockTransfer> b)
    {
        b.ToTable("StockTransfers", "inventory");
        b.HasKey(x => x.Id);
        b.Property(x => x.TransferNumber).HasMaxLength(50);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.StockTransferId);
    }
}

public sealed class StockTransferLineConfiguration : IEntityTypeConfiguration<StockTransferLine>
{
    public void Configure(EntityTypeBuilder<StockTransferLine> b)
    {
        b.ToTable("StockTransferLines", "inventory");
        b.HasKey(x => x.Id);
        b.Property(x => x.Qty).HasColumnType("decimal(19,4)");
    }
}

public sealed class StockAdjustmentConfiguration : IEntityTypeConfiguration<StockAdjustment>
{
    public void Configure(EntityTypeBuilder<StockAdjustment> b)
    {
        b.ToTable("StockAdjustments", "inventory");
        b.HasKey(x => x.Id);
        b.Property(x => x.AdjustmentNumber).HasMaxLength(50);
        b.Property(x => x.ReasonCode).HasMaxLength(50);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.StockAdjustmentId);
    }
}

public sealed class StockAdjustmentLineConfiguration : IEntityTypeConfiguration<StockAdjustmentLine>
{
    public void Configure(EntityTypeBuilder<StockAdjustmentLine> b)
    {
        b.ToTable("StockAdjustmentLines", "inventory");
        b.HasKey(x => x.Id);
        b.Property(x => x.QtyDelta).HasColumnType("decimal(19,4)");
        b.Property(x => x.UnitCost).HasColumnType("decimal(19,4)");
    }
}
