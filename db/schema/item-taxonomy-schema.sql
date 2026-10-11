-- Item taxonomy (category -> sub-category), sheet types fed from categories, fabrication links. Idempotent.
-- Category / sub-category reuses inventory.ItemCategories.ParentCategoryId (root = category, child = sub-category).

IF COL_LENGTH('inventory.ItemCategories', 'SortOrder') IS NULL
    ALTER TABLE inventory.ItemCategories ADD SortOrder INT NOT NULL CONSTRAINT DF_ItemCategories_SortOrder DEFAULT 0;
GO

IF COL_LENGTH('inventory.RequirementSheetTypes', 'CategoryId') IS NULL
    ALTER TABLE inventory.RequirementSheetTypes ADD CategoryId UNIQUEIDENTIFIER NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_RequirementSheetTypes_Category')
    ALTER TABLE inventory.RequirementSheetTypes ADD CONSTRAINT FK_RequirementSheetTypes_Category FOREIGN KEY (CategoryId) REFERENCES inventory.ItemCategories (Id);
GO

-- A fabrication list entry is a DRAFT work order raised for a published material requirement.
IF COL_LENGTH('inventory.WorkOrders', 'MaterialRequirementId') IS NULL
    ALTER TABLE inventory.WorkOrders ADD MaterialRequirementId UNIQUEIDENTIFIER NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_WorkOrders_MaterialRequirement')
    ALTER TABLE inventory.WorkOrders ADD CONSTRAINT FK_WorkOrders_MaterialRequirement FOREIGN KEY (MaterialRequirementId) REFERENCES inventory.MaterialRequirements (Id);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Inv_WorkOrders_Requirement')
    CREATE INDEX IX_Inv_WorkOrders_Requirement ON inventory.WorkOrders (MaterialRequirementId) WHERE MaterialRequirementId IS NOT NULL;
GO
