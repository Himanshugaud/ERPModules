/* =============================================================================
   ERP — REQUIREMENT SHEETS (additive, idempotent, GO-batched)
   Sheet types (Civil, Plumbing, Electrical ...) -> sections -> items from the item master.
   Apply AFTER procurement-schema.sql and workflow-schema.sql.
   ========================================================================== */

IF OBJECT_ID(N'inventory.RequirementSheetTypes', N'U') IS NULL
BEGIN
    CREATE TABLE inventory.RequirementSheetTypes
    (
        Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_RequirementSheetTypes PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId UNIQUEIDENTIFIER NOT NULL,
        Code           NVARCHAR(40)     NOT NULL,
        Name           NVARCHAR(150)    NOT NULL,
        Description    NVARCHAR(500)    NULL,
        DisplayOrder   INT              NOT NULL CONSTRAINT DF_RequirementSheetTypes_Order DEFAULT 0,
        IsActive       BIT              NOT NULL CONSTRAINT DF_RequirementSheetTypes_Active DEFAULT 1,
        CreatedAt      DATETIME2        NOT NULL CONSTRAINT DF_RequirementSheetTypes_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_RequirementSheetTypes_Org_Code UNIQUE (OrganizationId, Code),
        CONSTRAINT FK_RequirementSheetTypes_Org FOREIGN KEY (OrganizationId) REFERENCES core.Organizations (Id)
    );
END
GO

IF OBJECT_ID(N'inventory.RequirementSheetSections', N'U') IS NULL
BEGIN
    CREATE TABLE inventory.RequirementSheetSections
    (
        Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_RequirementSheetSections PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId UNIQUEIDENTIFIER NOT NULL,
        SheetTypeId    UNIQUEIDENTIFIER NOT NULL,
        Name           NVARCHAR(150)    NOT NULL,
        DisplayOrder   INT              NOT NULL CONSTRAINT DF_RequirementSheetSections_Order DEFAULT 0,
        IsActive       BIT              NOT NULL CONSTRAINT DF_RequirementSheetSections_Active DEFAULT 1,
        CONSTRAINT UQ_RequirementSheetSections_Type_Name UNIQUE (SheetTypeId, Name),
        CONSTRAINT FK_RequirementSheetSections_Type FOREIGN KEY (SheetTypeId) REFERENCES inventory.RequirementSheetTypes (Id)
    );
END
GO

/* A section pulls items from a whole item category, or from an explicit item (explicit wins within a sheet type). */
IF OBJECT_ID(N'inventory.RequirementSheetSectionItems', N'U') IS NULL
BEGIN
    CREATE TABLE inventory.RequirementSheetSectionItems
    (
        Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_RequirementSheetSectionItems PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId UNIQUEIDENTIFIER NOT NULL,
        SectionId      UNIQUEIDENTIFIER NOT NULL,
        CategoryId     UNIQUEIDENTIFIER NULL,
        ItemId         UNIQUEIDENTIFIER NULL,
        DisplayOrder   INT              NOT NULL CONSTRAINT DF_RequirementSheetSectionItems_Order DEFAULT 0,
        CONSTRAINT CK_RequirementSheetSectionItems_One CHECK ((CategoryId IS NULL AND ItemId IS NOT NULL) OR (CategoryId IS NOT NULL AND ItemId IS NULL)),
        CONSTRAINT FK_RequirementSheetSectionItems_Section FOREIGN KEY (SectionId) REFERENCES inventory.RequirementSheetSections (Id),
        CONSTRAINT FK_RequirementSheetSectionItems_Category FOREIGN KEY (CategoryId) REFERENCES inventory.ItemCategories (Id),
        CONSTRAINT FK_RequirementSheetSectionItems_Item FOREIGN KEY (ItemId) REFERENCES inventory.Items (Id)
    );
END
GO

IF COL_LENGTH(N'inventory.MaterialRequirements', N'SheetType') IS NULL
    ALTER TABLE inventory.MaterialRequirements ADD
        SheetType NVARCHAR(40)  NULL,
        BlockName NVARCHAR(100) NULL,
        BlockSize NVARCHAR(100) NULL;
GO

IF COL_LENGTH(N'inventory.MaterialRequirementLines', N'SectionName') IS NULL
    ALTER TABLE inventory.MaterialRequirementLines ADD SectionName NVARCHAR(150) NULL;
GO
