/* =============================================================================
   ERP — PROJECT WORKFLOW SCHEMA (additive, idempotent, GO-batched)
   Requirement -> Planning -> Publish -> Supply Chain -> Transport.
   Apply AFTER erp-schema.sql, inventory-schema.sql and procurement-schema.sql.
   ========================================================================== */

/* -----------------------------------------------------------------------------
   1. Attachments reuse shared.Documents (EntityType='PROJECT', EntityId=ProjectId).
      Adds a Category column that maps to a blob folder.
   -------------------------------------------------------------------------- */
IF COL_LENGTH(N'shared.Documents', N'Category') IS NULL
    ALTER TABLE shared.Documents ADD Category NVARCHAR(40) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Documents_Entity' AND object_id = OBJECT_ID(N'shared.Documents'))
    CREATE INDEX IX_Documents_Entity ON shared.Documents (OrganizationId, EntityType, EntityId) INCLUDE (Category, Status);
GO

/* -----------------------------------------------------------------------------
   2. Calendar planner entries (one row per planned item; may span several days)
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'project.PlanEntries', N'U') IS NULL
BEGIN
    CREATE TABLE project.PlanEntries
    (
        Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PlanEntries PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId UNIQUEIDENTIFIER NOT NULL,
        ProjectId      UNIQUEIDENTIFIER NOT NULL,
        StartDate      DATE             NOT NULL,
        EndDate        DATE             NOT NULL,
        Title          NVARCHAR(200)    NOT NULL,
        Description    NVARCHAR(1000)   NULL,
        EntryType      NVARCHAR(30)     NOT NULL CONSTRAINT DF_PlanEntries_Type DEFAULT N'ACTIVITY',
        Status         NVARCHAR(30)     NOT NULL CONSTRAINT DF_PlanEntries_Status DEFAULT N'PLANNED',
        AssignedTo     UNIQUEIDENTIFIER NULL,
        CreatedAt      DATETIME2        NOT NULL CONSTRAINT DF_PlanEntries_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt      DATETIME2        NULL,
        CreatedBy      UNIQUEIDENTIFIER NULL,
        UpdatedBy      UNIQUEIDENTIFIER NULL,
        CONSTRAINT CK_PlanEntries_Dates  CHECK (EndDate >= StartDate),
        CONSTRAINT CK_PlanEntries_Type   CHECK (EntryType IN (N'ACTIVITY', N'MILESTONE', N'MATERIAL', N'INSPECTION', N'MEETING', N'OTHER')),
        CONSTRAINT CK_PlanEntries_Status CHECK (Status IN (N'PLANNED', N'IN_PROGRESS', N'DONE', N'CANCELLED'))
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PlanEntries_Project')
    ALTER TABLE project.PlanEntries ADD CONSTRAINT FK_PlanEntries_Project
        FOREIGN KEY (ProjectId) REFERENCES project.Projects (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PlanEntries_Org')
    ALTER TABLE project.PlanEntries ADD CONSTRAINT FK_PlanEntries_Org
        FOREIGN KEY (OrganizationId) REFERENCES core.Organizations (Id);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PlanEntries_Project_Dates' AND object_id = OBJECT_ID(N'project.PlanEntries'))
    CREATE INDEX IX_PlanEntries_Project_Dates ON project.PlanEntries (ProjectId, StartDate, EndDate);
GO

/* -----------------------------------------------------------------------------
   3. Plan publication state (one row per project)
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'project.ProjectPlans', N'U') IS NULL
BEGIN
    CREATE TABLE project.ProjectPlans
    (
        ProjectId      UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ProjectPlans PRIMARY KEY,
        OrganizationId UNIQUEIDENTIFIER NOT NULL,
        Status         NVARCHAR(20)     NOT NULL CONSTRAINT DF_ProjectPlans_Status DEFAULT N'DRAFT',
        PublishedAt    DATETIME2        NULL,
        PublishedBy    UNIQUEIDENTIFIER NULL,
        Notes          NVARCHAR(1000)   NULL,
        UpdatedAt      DATETIME2        NULL,
        CONSTRAINT CK_ProjectPlans_Status CHECK (Status IN (N'DRAFT', N'PUBLISHED'))
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProjectPlans_Project')
    ALTER TABLE project.ProjectPlans ADD CONSTRAINT FK_ProjectPlans_Project
        FOREIGN KEY (ProjectId) REFERENCES project.Projects (Id);
GO

/* -----------------------------------------------------------------------------
   4. Material requirements can now be DRAFT until the plan is published
   -------------------------------------------------------------------------- */
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_MaterialRequirements_Status'
           AND definition NOT LIKE N'%DRAFT%')
    ALTER TABLE inventory.MaterialRequirements DROP CONSTRAINT CK_MaterialRequirements_Status;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_MaterialRequirements_Status')
    ALTER TABLE inventory.MaterialRequirements ADD CONSTRAINT CK_MaterialRequirements_Status CHECK (Status IN
        (N'DRAFT', N'SUBMITTED', N'APPROVED', N'REJECTED', N'CONVERTED', N'CANCELLED'));
GO

/* -----------------------------------------------------------------------------
   5. Transport details captured by the Transport team at dispatch
   -------------------------------------------------------------------------- */
IF COL_LENGTH(N'inventory.StockTransfers', N'TransporterName') IS NULL
    ALTER TABLE inventory.StockTransfers ADD
        TransporterName      NVARCHAR(150) NULL,
        VehicleNumber        NVARCHAR(30)  NULL,
        VehicleType          NVARCHAR(50)  NULL,
        DriverName           NVARCHAR(150) NULL,
        DriverPhone          NVARCHAR(30)  NULL,
        ExpectedDeliveryDate DATE          NULL,
        DispatchNotes        NVARCHAR(500) NULL;
GO

/* -----------------------------------------------------------------------------
   6. Permission: plan.manage (calendar planner + publish). Idempotent by Code.
   -------------------------------------------------------------------------- */
INSERT INTO core.Permissions (Code, Name, Module, Resource, Action)
SELECT v.Code, v.Name, v.Module, v.Resource, v.Action
FROM (VALUES
    (N'plan.manage', N'Manage Project Plan (calendar + publish)', N'project', N'plan', N'manage')
) AS v(Code, Name, Module, Resource, Action)
WHERE NOT EXISTS (SELECT 1 FROM core.Permissions p WHERE p.Code = v.Code);
GO
