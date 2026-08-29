/* =============================================================================
   ERP Platform - Initial Schema (0001)
   Target: Azure SQL Database (erpmodulessqldb)
   Modules: core, project, shared
   Idempotent: safe to run multiple times.
   ========================================================================== */

/* ---------------------------------------------------------------------------
   Schemas
   ------------------------------------------------------------------------ */
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'core')
    EXEC(N'CREATE SCHEMA core');
GO
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'project')
    EXEC(N'CREATE SCHEMA project');
GO
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'shared')
    EXEC(N'CREATE SCHEMA shared');
GO

/* =============================================================================
   CORE MODULE
   ========================================================================== */

IF OBJECT_ID(N'core.Organizations', N'U') IS NULL
BEGIN
    CREATE TABLE core.Organizations
    (
        Id           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Organizations PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        Code         NVARCHAR(50)     NOT NULL CONSTRAINT UQ_Organizations_Code UNIQUE,
        Name         NVARCHAR(200)    NOT NULL,
        LegalName    NVARCHAR(250)    NULL,
        Description  NVARCHAR(1000)   NULL,
        Email        NVARCHAR(255)    NULL,
        Phone        NVARCHAR(50)     NULL,
        CountryCode  NVARCHAR(10)     NULL,
        Timezone     NVARCHAR(100)    NULL,
        CurrencyCode CHAR(3)          NULL,
        Status       NVARCHAR(30)     NOT NULL CONSTRAINT DF_Organizations_Status DEFAULT N'ACTIVE',
        CreatedAt    DATETIME2        NOT NULL CONSTRAINT DF_Organizations_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt    DATETIME2        NULL,
        CreatedBy    UNIQUEIDENTIFIER NULL,
        UpdatedBy    UNIQUEIDENTIFIER NULL
    );
END
GO

IF OBJECT_ID(N'core.Departments', N'U') IS NULL
BEGIN
    CREATE TABLE core.Departments
    (
        Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Departments PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId UNIQUEIDENTIFIER NOT NULL,
        Name           NVARCHAR(200)    NOT NULL,
        Code           NVARCHAR(50)     NOT NULL,
        Description    NVARCHAR(MAX)    NULL,
        ManagerId      UNIQUEIDENTIFIER NULL,
        IsActive       BIT              NOT NULL CONSTRAINT DF_Departments_IsActive DEFAULT 1,
        CreatedAt      DATETIME2        NOT NULL CONSTRAINT DF_Departments_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt      DATETIME2        NULL,
        CreatedBy      UNIQUEIDENTIFIER NULL,
        UpdatedBy      UNIQUEIDENTIFIER NULL,
        CONSTRAINT UQ_Departments_Org_Code UNIQUE (OrganizationId, Code)
    );
END
GO

IF OBJECT_ID(N'core.Users', N'U') IS NULL
BEGIN
    CREATE TABLE core.Users
    (
        Id                 UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Users PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId     UNIQUEIDENTIFIER NOT NULL,
        ExternalIdentityId NVARCHAR(200)    NULL,
        Email              NVARCHAR(255)    NOT NULL,
        FirstName          NVARCHAR(100)    NULL,
        LastName           NVARCHAR(100)    NULL,
        DisplayName        NVARCHAR(200)    NULL,
        Phone              NVARCHAR(50)     NULL,
        JobTitle           NVARCHAR(150)    NULL,
        DepartmentId       UNIQUEIDENTIFIER NULL,
        Status             NVARCHAR(30)     NOT NULL CONSTRAINT DF_Users_Status DEFAULT N'ACTIVE',
        Timezone           NVARCHAR(100)    NULL,
        ProfileImageUrl    NVARCHAR(500)    NULL,
        LastLoginAt        DATETIME2        NULL,
        CreatedAt          DATETIME2        NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt          DATETIME2        NULL,
        CreatedBy          UNIQUEIDENTIFIER NULL,
        UpdatedBy          UNIQUEIDENTIFIER NULL,
        CONSTRAINT UQ_Users_Org_Email UNIQUE (OrganizationId, Email)
    );
END
GO

IF OBJECT_ID(N'core.OrganizationSettings', N'U') IS NULL
BEGIN
    CREATE TABLE core.OrganizationSettings
    (
        Id                     UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_OrganizationSettings PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId         UNIQUEIDENTIFIER NOT NULL CONSTRAINT UQ_OrganizationSettings_Org UNIQUE,
        DefaultCurrencyCode    CHAR(3)          NULL,
        Timezone               NVARCHAR(100)    NULL,
        DateFormat             NVARCHAR(50)     NULL,
        WeekStartDay           TINYINT          NULL,
        FiscalYearStartMonth   TINYINT          NULL,
        DefaultProjectStatusId UNIQUEIDENTIFIER NULL,
        DefaultTaskStatusId    UNIQUEIDENTIFIER NULL,
        SettingsJson           NVARCHAR(MAX)    NULL,
        CreatedAt              DATETIME2        NOT NULL CONSTRAINT DF_OrganizationSettings_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt              DATETIME2        NULL,
        CONSTRAINT CK_OrganizationSettings_WeekStartDay CHECK (WeekStartDay IS NULL OR WeekStartDay BETWEEN 0 AND 6),
        CONSTRAINT CK_OrganizationSettings_FiscalMonth CHECK (FiscalYearStartMonth IS NULL OR FiscalYearStartMonth BETWEEN 1 AND 12)
    );
END
GO

IF OBJECT_ID(N'core.Roles', N'U') IS NULL
BEGIN
    CREATE TABLE core.Roles
    (
        Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Roles PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId UNIQUEIDENTIFIER NOT NULL,
        Name           NVARCHAR(100)    NOT NULL,
        Description    NVARCHAR(500)    NULL,
        IsSystemRole   BIT              NOT NULL CONSTRAINT DF_Roles_IsSystemRole DEFAULT 0,
        IsActive       BIT              NOT NULL CONSTRAINT DF_Roles_IsActive DEFAULT 1,
        CreatedAt      DATETIME2        NOT NULL CONSTRAINT DF_Roles_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt      DATETIME2        NULL,
        CreatedBy      UNIQUEIDENTIFIER NULL,
        UpdatedBy      UNIQUEIDENTIFIER NULL,
        CONSTRAINT UQ_Roles_Org_Name UNIQUE (OrganizationId, Name)
    );
END
GO

IF OBJECT_ID(N'core.Permissions', N'U') IS NULL
BEGIN
    CREATE TABLE core.Permissions
    (
        Id          UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Permissions PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        Code        NVARCHAR(100)    NOT NULL CONSTRAINT UQ_Permissions_Code UNIQUE,
        Name        NVARCHAR(200)    NOT NULL,
        Description NVARCHAR(500)    NULL,
        Module      NVARCHAR(50)     NULL,
        Resource    NVARCHAR(50)     NULL,
        Action      NVARCHAR(50)     NULL,
        IsActive    BIT              NOT NULL CONSTRAINT DF_Permissions_IsActive DEFAULT 1
    );
END
GO

IF OBJECT_ID(N'core.UserRoles', N'U') IS NULL
BEGIN
    CREATE TABLE core.UserRoles
    (
        UserId     UNIQUEIDENTIFIER NOT NULL,
        RoleId     UNIQUEIDENTIFIER NOT NULL,
        AssignedAt DATETIME2        NOT NULL CONSTRAINT DF_UserRoles_AssignedAt DEFAULT SYSUTCDATETIME(),
        AssignedBy UNIQUEIDENTIFIER NULL,
        CONSTRAINT PK_UserRoles PRIMARY KEY (UserId, RoleId)
    );
END
GO

IF OBJECT_ID(N'core.RolePermissions', N'U') IS NULL
BEGIN
    CREATE TABLE core.RolePermissions
    (
        RoleId       UNIQUEIDENTIFIER NOT NULL,
        PermissionId UNIQUEIDENTIFIER NOT NULL,
        AssignedAt   DATETIME2        NOT NULL CONSTRAINT DF_RolePermissions_AssignedAt DEFAULT SYSUTCDATETIME(),
        AssignedBy   UNIQUEIDENTIFIER NULL,
        CONSTRAINT PK_RolePermissions PRIMARY KEY (RoleId, PermissionId)
    );
END
GO

IF OBJECT_ID(N'core.Clients', N'U') IS NULL
BEGIN
    CREATE TABLE core.Clients
    (
        Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Clients PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId UNIQUEIDENTIFIER NOT NULL,
        Name           NVARCHAR(200)    NOT NULL,
        Code           NVARCHAR(50)     NOT NULL,
        Email          NVARCHAR(255)    NULL,
        Phone          NVARCHAR(50)     NULL,
        Status         NVARCHAR(30)     NOT NULL CONSTRAINT DF_Clients_Status DEFAULT N'ACTIVE',
        CreatedAt      DATETIME2        NOT NULL CONSTRAINT DF_Clients_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt      DATETIME2        NULL,
        CONSTRAINT UQ_Clients_Org_Code UNIQUE (OrganizationId, Code)
    );
END
GO

/* =============================================================================
   PROJECT MODULE
   ========================================================================== */

IF OBJECT_ID(N'project.ProjectStatuses', N'U') IS NULL
BEGIN
    CREATE TABLE project.ProjectStatuses
    (
        Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ProjectStatuses PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId UNIQUEIDENTIFIER NOT NULL,
        Code           NVARCHAR(50)     NOT NULL,
        Name           NVARCHAR(100)    NOT NULL,
        Description    NVARCHAR(500)    NULL,
        DisplayOrder   INT              NOT NULL CONSTRAINT DF_ProjectStatuses_DisplayOrder DEFAULT 0,
        IsDefault      BIT              NOT NULL CONSTRAINT DF_ProjectStatuses_IsDefault DEFAULT 0,
        IsFinal        BIT              NOT NULL CONSTRAINT DF_ProjectStatuses_IsFinal DEFAULT 0,
        IsActive       BIT              NOT NULL CONSTRAINT DF_ProjectStatuses_IsActive DEFAULT 1,
        CreatedAt      DATETIME2        NOT NULL CONSTRAINT DF_ProjectStatuses_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt      DATETIME2        NULL,
        CreatedBy      UNIQUEIDENTIFIER NULL,
        UpdatedBy      UNIQUEIDENTIFIER NULL,
        CONSTRAINT UQ_ProjectStatuses_Org_Code UNIQUE (OrganizationId, Code)
    );
END
GO

IF OBJECT_ID(N'project.ProjectPriorities', N'U') IS NULL
BEGIN
    CREATE TABLE project.ProjectPriorities
    (
        Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ProjectPriorities PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId UNIQUEIDENTIFIER NOT NULL,
        Code           NVARCHAR(50)     NOT NULL,
        Name           NVARCHAR(100)    NOT NULL,
        DisplayOrder   INT              NOT NULL CONSTRAINT DF_ProjectPriorities_DisplayOrder DEFAULT 0,
        IsActive       BIT              NOT NULL CONSTRAINT DF_ProjectPriorities_IsActive DEFAULT 1,
        CreatedAt      DATETIME2        NOT NULL CONSTRAINT DF_ProjectPriorities_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt      DATETIME2        NULL,
        CONSTRAINT UQ_ProjectPriorities_Org_Code UNIQUE (OrganizationId, Code)
    );
END
GO

IF OBJECT_ID(N'project.Projects', N'U') IS NULL
BEGIN
    CREATE TABLE project.Projects
    (
        Id                   UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Projects PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId       UNIQUEIDENTIFIER NOT NULL,
        Code                 NVARCHAR(50)     NOT NULL,
        Name                 NVARCHAR(200)    NOT NULL,
        Description          NVARCHAR(MAX)    NULL,
        ClientId             UNIQUEIDENTIFIER NULL,
        ManagerId            UNIQUEIDENTIFIER NULL,
        StatusId             UNIQUEIDENTIFIER NULL,
        PriorityId           UNIQUEIDENTIFIER NULL,
        StartDate            DATE             NULL,
        PlannedEndDate       DATE             NULL,
        ActualEndDate        DATE             NULL,
        CompletionPercentage DECIMAL(5,2)     NOT NULL CONSTRAINT DF_Projects_Completion DEFAULT 0,
        Budget               DECIMAL(19,4)    NULL,
        CurrencyCode         CHAR(3)          NULL,
        IsArchived           BIT              NOT NULL CONSTRAINT DF_Projects_IsArchived DEFAULT 0,
        IsDeleted            BIT              NOT NULL CONSTRAINT DF_Projects_IsDeleted DEFAULT 0,
        DeletedAt            DATETIME2        NULL,
        DeletedBy            UNIQUEIDENTIFIER NULL,
        CreatedAt            DATETIME2        NOT NULL CONSTRAINT DF_Projects_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt            DATETIME2        NULL,
        CreatedBy            UNIQUEIDENTIFIER NULL,
        UpdatedBy            UNIQUEIDENTIFIER NULL,
        RowVersion           ROWVERSION       NOT NULL,
        CONSTRAINT UQ_Projects_Org_Code UNIQUE (OrganizationId, Code),
        CONSTRAINT CK_Projects_Completion CHECK (CompletionPercentage >= 0 AND CompletionPercentage <= 100),
        CONSTRAINT CK_Projects_Budget CHECK (Budget IS NULL OR Budget >= 0),
        CONSTRAINT CK_Projects_Dates CHECK (StartDate IS NULL OR PlannedEndDate IS NULL OR PlannedEndDate >= StartDate)
    );
END
GO

IF OBJECT_ID(N'project.ProjectMembers', N'U') IS NULL
BEGIN
    CREATE TABLE project.ProjectMembers
    (
        ProjectId            UNIQUEIDENTIFIER NOT NULL,
        UserId               UNIQUEIDENTIFIER NOT NULL,
        ProjectRole          NVARCHAR(50)     NULL,
        AllocationPercentage DECIMAL(5,2)     NULL,
        StartDate            DATE             NULL,
        EndDate              DATE             NULL,
        Status               NVARCHAR(30)     NOT NULL CONSTRAINT DF_ProjectMembers_Status DEFAULT N'ACTIVE',
        CreatedAt            DATETIME2        NOT NULL CONSTRAINT DF_ProjectMembers_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt            DATETIME2        NULL,
        CreatedBy            UNIQUEIDENTIFIER NULL,
        UpdatedBy            UNIQUEIDENTIFIER NULL,
        CONSTRAINT PK_ProjectMembers PRIMARY KEY (ProjectId, UserId),
        CONSTRAINT CK_ProjectMembers_Allocation CHECK (AllocationPercentage IS NULL OR (AllocationPercentage >= 0 AND AllocationPercentage <= 100))
    );
END
GO

IF OBJECT_ID(N'project.TaskStatuses', N'U') IS NULL
BEGIN
    CREATE TABLE project.TaskStatuses
    (
        Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TaskStatuses PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId UNIQUEIDENTIFIER NOT NULL,
        Code           NVARCHAR(50)     NOT NULL,
        Name           NVARCHAR(100)    NOT NULL,
        Description    NVARCHAR(500)    NULL,
        DisplayOrder   INT              NOT NULL CONSTRAINT DF_TaskStatuses_DisplayOrder DEFAULT 0,
        IsFinal        BIT              NOT NULL CONSTRAINT DF_TaskStatuses_IsFinal DEFAULT 0,
        IsActive       BIT              NOT NULL CONSTRAINT DF_TaskStatuses_IsActive DEFAULT 1,
        CreatedAt      DATETIME2        NOT NULL CONSTRAINT DF_TaskStatuses_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt      DATETIME2        NULL,
        CONSTRAINT UQ_TaskStatuses_Org_Code UNIQUE (OrganizationId, Code)
    );
END
GO

IF OBJECT_ID(N'project.TaskPriorities', N'U') IS NULL
BEGIN
    CREATE TABLE project.TaskPriorities
    (
        Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TaskPriorities PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId UNIQUEIDENTIFIER NOT NULL,
        Code           NVARCHAR(50)     NOT NULL,
        Name           NVARCHAR(100)    NOT NULL,
        DisplayOrder   INT              NOT NULL CONSTRAINT DF_TaskPriorities_DisplayOrder DEFAULT 0,
        IsActive       BIT              NOT NULL CONSTRAINT DF_TaskPriorities_IsActive DEFAULT 1,
        CreatedAt      DATETIME2        NOT NULL CONSTRAINT DF_TaskPriorities_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt      DATETIME2        NULL,
        CONSTRAINT UQ_TaskPriorities_Org_Code UNIQUE (OrganizationId, Code)
    );
END
GO

IF OBJECT_ID(N'project.Milestones', N'U') IS NULL
BEGIN
    CREATE TABLE project.Milestones
    (
        Id                   UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Milestones PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId       UNIQUEIDENTIFIER NOT NULL,
        ProjectId            UNIQUEIDENTIFIER NOT NULL,
        Name                 NVARCHAR(200)    NOT NULL,
        Description          NVARCHAR(MAX)    NULL,
        Status               NVARCHAR(30)     NOT NULL CONSTRAINT DF_Milestones_Status DEFAULT N'OPEN',
        OwnerId              UNIQUEIDENTIFIER NULL,
        DueDate              DATE             NULL,
        CompletionPercentage DECIMAL(5,2)     NOT NULL CONSTRAINT DF_Milestones_Completion DEFAULT 0,
        CreatedAt            DATETIME2        NOT NULL CONSTRAINT DF_Milestones_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt            DATETIME2        NULL,
        CreatedBy            UNIQUEIDENTIFIER NULL,
        UpdatedBy            UNIQUEIDENTIFIER NULL,
        RowVersion           ROWVERSION       NOT NULL,
        CONSTRAINT CK_Milestones_Completion CHECK (CompletionPercentage >= 0 AND CompletionPercentage <= 100)
    );
END
GO

IF OBJECT_ID(N'project.Sprints', N'U') IS NULL
BEGIN
    CREATE TABLE project.Sprints
    (
        Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Sprints PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId UNIQUEIDENTIFIER NOT NULL,
        ProjectId      UNIQUEIDENTIFIER NOT NULL,
        Name           NVARCHAR(200)    NOT NULL,
        Goal           NVARCHAR(MAX)    NULL,
        Status         NVARCHAR(30)     NOT NULL CONSTRAINT DF_Sprints_Status DEFAULT N'PLANNED',
        StartDate      DATE             NULL,
        EndDate        DATE             NULL,
        CreatedAt      DATETIME2        NOT NULL CONSTRAINT DF_Sprints_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt      DATETIME2        NULL,
        CreatedBy      UNIQUEIDENTIFIER NULL,
        UpdatedBy      UNIQUEIDENTIFIER NULL,
        CONSTRAINT CK_Sprints_Dates CHECK (StartDate IS NULL OR EndDate IS NULL OR EndDate >= StartDate)
    );
END
GO

IF OBJECT_ID(N'project.Tasks', N'U') IS NULL
BEGIN
    CREATE TABLE project.Tasks
    (
        Id                   UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Tasks PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId       UNIQUEIDENTIFIER NOT NULL,
        ProjectId            UNIQUEIDENTIFIER NOT NULL,
        ParentTaskId         UNIQUEIDENTIFIER NULL,
        Title                NVARCHAR(300)    NOT NULL,
        Description          NVARCHAR(MAX)    NULL,
        StatusId             UNIQUEIDENTIFIER NULL,
        PriorityId           UNIQUEIDENTIFIER NULL,
        AssigneeId           UNIQUEIDENTIFIER NULL,
        ReporterId           UNIQUEIDENTIFIER NULL,
        MilestoneId          UNIQUEIDENTIFIER NULL,
        SprintId             UNIQUEIDENTIFIER NULL,
        StartDate            DATE             NULL,
        DueDate              DATE             NULL,
        EstimatedHours       DECIMAL(10,2)    NULL,
        ActualHours          DECIMAL(10,2)    NULL,
        CompletionPercentage DECIMAL(5,2)     NOT NULL CONSTRAINT DF_Tasks_Completion DEFAULT 0,
        IsArchived           BIT              NOT NULL CONSTRAINT DF_Tasks_IsArchived DEFAULT 0,
        IsDeleted            BIT              NOT NULL CONSTRAINT DF_Tasks_IsDeleted DEFAULT 0,
        DeletedAt            DATETIME2        NULL,
        DeletedBy            UNIQUEIDENTIFIER NULL,
        CreatedAt            DATETIME2        NOT NULL CONSTRAINT DF_Tasks_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt            DATETIME2        NULL,
        CreatedBy            UNIQUEIDENTIFIER NULL,
        UpdatedBy            UNIQUEIDENTIFIER NULL,
        RowVersion           ROWVERSION       NOT NULL,
        CONSTRAINT CK_Tasks_Completion CHECK (CompletionPercentage >= 0 AND CompletionPercentage <= 100)
    );
END
GO

IF OBJECT_ID(N'project.TaskWatchers', N'U') IS NULL
BEGIN
    CREATE TABLE project.TaskWatchers
    (
        TaskId    UNIQUEIDENTIFIER NOT NULL,
        UserId    UNIQUEIDENTIFIER NOT NULL,
        CreatedAt DATETIME2        NOT NULL CONSTRAINT DF_TaskWatchers_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_TaskWatchers PRIMARY KEY (TaskId, UserId)
    );
END
GO

IF OBJECT_ID(N'project.SprintTasks', N'U') IS NULL
BEGIN
    CREATE TABLE project.SprintTasks
    (
        SprintId UNIQUEIDENTIFIER NOT NULL,
        TaskId   UNIQUEIDENTIFIER NOT NULL,
        AddedAt  DATETIME2        NOT NULL CONSTRAINT DF_SprintTasks_AddedAt DEFAULT SYSUTCDATETIME(),
        AddedBy  UNIQUEIDENTIFIER NULL,
        CONSTRAINT PK_SprintTasks PRIMARY KEY (SprintId, TaskId)
    );
END
GO

IF OBJECT_ID(N'project.Timesheets', N'U') IS NULL
BEGIN
    CREATE TABLE project.Timesheets
    (
        Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Timesheets PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId UNIQUEIDENTIFIER NOT NULL,
        UserId         UNIQUEIDENTIFIER NOT NULL,
        ProjectId      UNIQUEIDENTIFIER NOT NULL,
        TaskId         UNIQUEIDENTIFIER NULL,
        Date           DATE             NOT NULL,
        Hours          DECIMAL(10,2)    NOT NULL,
        Description    NVARCHAR(MAX)    NULL,
        Status         NVARCHAR(30)     NOT NULL CONSTRAINT DF_Timesheets_Status DEFAULT N'DRAFT',
        SubmittedAt    DATETIME2        NULL,
        ApprovedAt     DATETIME2        NULL,
        ApprovedBy     UNIQUEIDENTIFIER NULL,
        RejectedAt     DATETIME2        NULL,
        RejectedBy     UNIQUEIDENTIFIER NULL,
        CreatedAt      DATETIME2        NOT NULL CONSTRAINT DF_Timesheets_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt      DATETIME2        NULL,
        CreatedBy      UNIQUEIDENTIFIER NULL,
        UpdatedBy      UNIQUEIDENTIFIER NULL,
        RowVersion     ROWVERSION       NOT NULL,
        CONSTRAINT CK_Timesheets_Hours CHECK (Hours > 0),
        CONSTRAINT CK_Timesheets_Status CHECK (Status IN (N'DRAFT', N'SUBMITTED', N'APPROVED', N'REJECTED'))
    );
END
GO

IF OBJECT_ID(N'project.TimesheetApprovals', N'U') IS NULL
BEGIN
    CREATE TABLE project.TimesheetApprovals
    (
        Id          UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TimesheetApprovals PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        TimesheetId UNIQUEIDENTIFIER NOT NULL,
        ApproverId  UNIQUEIDENTIFIER NULL,
        Action      NVARCHAR(20)     NOT NULL,
        Comment     NVARCHAR(MAX)    NULL,
        CreatedAt   DATETIME2        NOT NULL CONSTRAINT DF_TimesheetApprovals_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_TimesheetApprovals_Action CHECK (Action IN (N'SUBMITTED', N'APPROVED', N'REJECTED'))
    );
END
GO

/* =============================================================================
   SHARED MODULE
   ========================================================================== */

IF OBJECT_ID(N'shared.Comments', N'U') IS NULL
BEGIN
    CREATE TABLE shared.Comments
    (
        Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Comments PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId UNIQUEIDENTIFIER NOT NULL,
        EntityType     NVARCHAR(50)     NOT NULL,
        EntityId       UNIQUEIDENTIFIER NOT NULL,
        UserId         UNIQUEIDENTIFIER NULL,
        Content        NVARCHAR(MAX)    NOT NULL,
        IsDeleted      BIT              NOT NULL CONSTRAINT DF_Comments_IsDeleted DEFAULT 0,
        DeletedAt      DATETIME2        NULL,
        DeletedBy      UNIQUEIDENTIFIER NULL,
        CreatedAt      DATETIME2        NOT NULL CONSTRAINT DF_Comments_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt      DATETIME2        NULL,
        CreatedBy      UNIQUEIDENTIFIER NULL,
        UpdatedBy      UNIQUEIDENTIFIER NULL
    );
END
GO

IF OBJECT_ID(N'shared.Documents', N'U') IS NULL
BEGIN
    CREATE TABLE shared.Documents
    (
        Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Documents PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId UNIQUEIDENTIFIER NOT NULL,
        EntityType     NVARCHAR(50)     NOT NULL,
        EntityId       UNIQUEIDENTIFIER NOT NULL,
        FileName       NVARCHAR(400)    NOT NULL,
        ContentType    NVARCHAR(200)    NULL,
        FileSize       BIGINT           NULL,
        BlobContainer  NVARCHAR(200)    NULL,
        BlobPath       NVARCHAR(1000)   NULL,
        Description    NVARCHAR(1000)   NULL,
        Version        INT              NOT NULL CONSTRAINT DF_Documents_Version DEFAULT 1,
        Status         NVARCHAR(30)     NOT NULL CONSTRAINT DF_Documents_Status DEFAULT N'ACTIVE',
        UploadedBy     UNIQUEIDENTIFIER NULL,
        CreatedAt      DATETIME2        NOT NULL CONSTRAINT DF_Documents_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt      DATETIME2        NULL,
        CONSTRAINT CK_Documents_FileSize CHECK (FileSize IS NULL OR FileSize >= 0)
    );
END
GO

IF OBJECT_ID(N'shared.Notifications', N'U') IS NULL
BEGIN
    CREATE TABLE shared.Notifications
    (
        Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Notifications PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId UNIQUEIDENTIFIER NOT NULL,
        UserId         UNIQUEIDENTIFIER NOT NULL,
        Type           NVARCHAR(50)     NOT NULL,
        Title          NVARCHAR(300)    NOT NULL,
        Message        NVARCHAR(MAX)    NULL,
        EntityType     NVARCHAR(50)     NULL,
        EntityId       UNIQUEIDENTIFIER NULL,
        IsRead         BIT              NOT NULL CONSTRAINT DF_Notifications_IsRead DEFAULT 0,
        ReadAt         DATETIME2        NULL,
        CreatedAt      DATETIME2        NOT NULL CONSTRAINT DF_Notifications_CreatedAt DEFAULT SYSUTCDATETIME()
    );
END
GO

IF OBJECT_ID(N'shared.NotificationPreferences', N'U') IS NULL
BEGIN
    CREATE TABLE shared.NotificationPreferences
    (
        Id               UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_NotificationPreferences PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId   UNIQUEIDENTIFIER NOT NULL,
        UserId           UNIQUEIDENTIFIER NOT NULL,
        NotificationType NVARCHAR(50)     NOT NULL,
        EmailEnabled     BIT              NOT NULL CONSTRAINT DF_NotificationPreferences_Email DEFAULT 1,
        InAppEnabled     BIT              NOT NULL CONSTRAINT DF_NotificationPreferences_InApp DEFAULT 1,
        PushEnabled      BIT              NOT NULL CONSTRAINT DF_NotificationPreferences_Push DEFAULT 0,
        TeamsEnabled     BIT              NOT NULL CONSTRAINT DF_NotificationPreferences_Teams DEFAULT 0,
        CreatedAt        DATETIME2        NOT NULL CONSTRAINT DF_NotificationPreferences_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt        DATETIME2        NULL,
        CONSTRAINT UQ_NotificationPreferences_User_Type UNIQUE (UserId, NotificationType)
    );
END
GO

IF OBJECT_ID(N'shared.AuditLogs', N'U') IS NULL
BEGIN
    CREATE TABLE shared.AuditLogs
    (
        Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AuditLogs PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId UNIQUEIDENTIFIER NOT NULL,
        UserId         UNIQUEIDENTIFIER NULL,
        EntityType     NVARCHAR(50)     NOT NULL,
        EntityId       UNIQUEIDENTIFIER NULL,
        Action         NVARCHAR(30)     NOT NULL,
        OldValuesJson  NVARCHAR(MAX)    NULL,
        NewValuesJson  NVARCHAR(MAX)    NULL,
        IpAddress      NVARCHAR(50)     NULL,
        UserAgent      NVARCHAR(500)    NULL,
        CorrelationId  UNIQUEIDENTIFIER NULL,
        CreatedAt      DATETIME2        NOT NULL CONSTRAINT DF_AuditLogs_CreatedAt DEFAULT SYSUTCDATETIME()
    );
END
GO

IF OBJECT_ID(N'shared.CustomFields', N'U') IS NULL
BEGIN
    CREATE TABLE shared.CustomFields
    (
        Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CustomFields PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId UNIQUEIDENTIFIER NOT NULL,
        EntityType     NVARCHAR(50)     NOT NULL,
        Name           NVARCHAR(200)    NOT NULL,
        Code           NVARCHAR(50)     NOT NULL,
        Description    NVARCHAR(500)    NULL,
        FieldType      NVARCHAR(20)     NOT NULL,
        IsRequired     BIT              NOT NULL CONSTRAINT DF_CustomFields_IsRequired DEFAULT 0,
        IsActive       BIT              NOT NULL CONSTRAINT DF_CustomFields_IsActive DEFAULT 1,
        DisplayOrder   INT              NOT NULL CONSTRAINT DF_CustomFields_DisplayOrder DEFAULT 0,
        OptionsJson    NVARCHAR(MAX)    NULL,
        CreatedAt      DATETIME2        NOT NULL CONSTRAINT DF_CustomFields_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt      DATETIME2        NULL,
        CreatedBy      UNIQUEIDENTIFIER NULL,
        UpdatedBy      UNIQUEIDENTIFIER NULL,
        CONSTRAINT UQ_CustomFields_Org_Entity_Code UNIQUE (OrganizationId, EntityType, Code),
        CONSTRAINT CK_CustomFields_FieldType CHECK (FieldType IN (N'TEXT', N'NUMBER', N'BOOLEAN', N'DATE', N'DATETIME', N'SELECT', N'MULTI_SELECT'))
    );
END
GO

IF OBJECT_ID(N'shared.CustomFieldValues', N'U') IS NULL
BEGIN
    CREATE TABLE shared.CustomFieldValues
    (
        Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CustomFieldValues PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId UNIQUEIDENTIFIER NOT NULL,
        CustomFieldId  UNIQUEIDENTIFIER NOT NULL,
        EntityType     NVARCHAR(50)     NOT NULL,
        EntityId       UNIQUEIDENTIFIER NOT NULL,
        ValueText      NVARCHAR(MAX)    NULL,
        ValueNumber    DECIMAL(19,4)    NULL,
        ValueBoolean   BIT              NULL,
        ValueDate      DATE             NULL,
        ValueDateTime  DATETIME2        NULL,
        CreatedAt      DATETIME2        NOT NULL CONSTRAINT DF_CustomFieldValues_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt      DATETIME2        NULL,
        UpdatedBy      UNIQUEIDENTIFIER NULL,
        CONSTRAINT UQ_CustomFieldValues_Field_Entity UNIQUE (CustomFieldId, EntityType, EntityId)
    );
END
GO

IF OBJECT_ID(N'shared.OutboxMessages', N'U') IS NULL
BEGIN
    CREATE TABLE shared.OutboxMessages
    (
        Id            UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_OutboxMessages PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        EventType     NVARCHAR(100)    NOT NULL,
        AggregateType NVARCHAR(100)    NULL,
        AggregateId   UNIQUEIDENTIFIER NULL,
        PayloadJson   NVARCHAR(MAX)    NULL,
        CreatedAt     DATETIME2        NOT NULL CONSTRAINT DF_OutboxMessages_CreatedAt DEFAULT SYSUTCDATETIME(),
        ProcessedAt   DATETIME2        NULL,
        RetryCount    INT              NOT NULL CONSTRAINT DF_OutboxMessages_RetryCount DEFAULT 0,
        Error         NVARCHAR(MAX)    NULL
    );
END
GO

/* =============================================================================
   FOREIGN KEYS  (ON DELETE NO ACTION - business-level deletion/archival)
   Added after all tables exist to avoid ordering / circular dependencies.
   ========================================================================== */

-- core
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_OrganizationSettings_Organization')
    ALTER TABLE core.OrganizationSettings ADD CONSTRAINT FK_OrganizationSettings_Organization
        FOREIGN KEY (OrganizationId) REFERENCES core.Organizations (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_OrganizationSettings_DefaultProjectStatus')
    ALTER TABLE core.OrganizationSettings ADD CONSTRAINT FK_OrganizationSettings_DefaultProjectStatus
        FOREIGN KEY (DefaultProjectStatusId) REFERENCES project.ProjectStatuses (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_OrganizationSettings_DefaultTaskStatus')
    ALTER TABLE core.OrganizationSettings ADD CONSTRAINT FK_OrganizationSettings_DefaultTaskStatus
        FOREIGN KEY (DefaultTaskStatusId) REFERENCES project.TaskStatuses (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Departments_Organization')
    ALTER TABLE core.Departments ADD CONSTRAINT FK_Departments_Organization
        FOREIGN KEY (OrganizationId) REFERENCES core.Organizations (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Departments_Manager')
    ALTER TABLE core.Departments ADD CONSTRAINT FK_Departments_Manager
        FOREIGN KEY (ManagerId) REFERENCES core.Users (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Users_Organization')
    ALTER TABLE core.Users ADD CONSTRAINT FK_Users_Organization
        FOREIGN KEY (OrganizationId) REFERENCES core.Organizations (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Users_Department')
    ALTER TABLE core.Users ADD CONSTRAINT FK_Users_Department
        FOREIGN KEY (DepartmentId) REFERENCES core.Departments (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Roles_Organization')
    ALTER TABLE core.Roles ADD CONSTRAINT FK_Roles_Organization
        FOREIGN KEY (OrganizationId) REFERENCES core.Organizations (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_UserRoles_User')
    ALTER TABLE core.UserRoles ADD CONSTRAINT FK_UserRoles_User
        FOREIGN KEY (UserId) REFERENCES core.Users (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_UserRoles_Role')
    ALTER TABLE core.UserRoles ADD CONSTRAINT FK_UserRoles_Role
        FOREIGN KEY (RoleId) REFERENCES core.Roles (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_RolePermissions_Role')
    ALTER TABLE core.RolePermissions ADD CONSTRAINT FK_RolePermissions_Role
        FOREIGN KEY (RoleId) REFERENCES core.Roles (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_RolePermissions_Permission')
    ALTER TABLE core.RolePermissions ADD CONSTRAINT FK_RolePermissions_Permission
        FOREIGN KEY (PermissionId) REFERENCES core.Permissions (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Clients_Organization')
    ALTER TABLE core.Clients ADD CONSTRAINT FK_Clients_Organization
        FOREIGN KEY (OrganizationId) REFERENCES core.Organizations (Id);
GO

-- project
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProjectStatuses_Organization')
    ALTER TABLE project.ProjectStatuses ADD CONSTRAINT FK_ProjectStatuses_Organization
        FOREIGN KEY (OrganizationId) REFERENCES core.Organizations (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProjectPriorities_Organization')
    ALTER TABLE project.ProjectPriorities ADD CONSTRAINT FK_ProjectPriorities_Organization
        FOREIGN KEY (OrganizationId) REFERENCES core.Organizations (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Projects_Organization')
    ALTER TABLE project.Projects ADD CONSTRAINT FK_Projects_Organization
        FOREIGN KEY (OrganizationId) REFERENCES core.Organizations (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Projects_Client')
    ALTER TABLE project.Projects ADD CONSTRAINT FK_Projects_Client
        FOREIGN KEY (ClientId) REFERENCES core.Clients (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Projects_Manager')
    ALTER TABLE project.Projects ADD CONSTRAINT FK_Projects_Manager
        FOREIGN KEY (ManagerId) REFERENCES core.Users (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Projects_Status')
    ALTER TABLE project.Projects ADD CONSTRAINT FK_Projects_Status
        FOREIGN KEY (StatusId) REFERENCES project.ProjectStatuses (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Projects_Priority')
    ALTER TABLE project.Projects ADD CONSTRAINT FK_Projects_Priority
        FOREIGN KEY (PriorityId) REFERENCES project.ProjectPriorities (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProjectMembers_Project')
    ALTER TABLE project.ProjectMembers ADD CONSTRAINT FK_ProjectMembers_Project
        FOREIGN KEY (ProjectId) REFERENCES project.Projects (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProjectMembers_User')
    ALTER TABLE project.ProjectMembers ADD CONSTRAINT FK_ProjectMembers_User
        FOREIGN KEY (UserId) REFERENCES core.Users (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_TaskStatuses_Organization')
    ALTER TABLE project.TaskStatuses ADD CONSTRAINT FK_TaskStatuses_Organization
        FOREIGN KEY (OrganizationId) REFERENCES core.Organizations (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_TaskPriorities_Organization')
    ALTER TABLE project.TaskPriorities ADD CONSTRAINT FK_TaskPriorities_Organization
        FOREIGN KEY (OrganizationId) REFERENCES core.Organizations (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Milestones_Organization')
    ALTER TABLE project.Milestones ADD CONSTRAINT FK_Milestones_Organization
        FOREIGN KEY (OrganizationId) REFERENCES core.Organizations (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Milestones_Project')
    ALTER TABLE project.Milestones ADD CONSTRAINT FK_Milestones_Project
        FOREIGN KEY (ProjectId) REFERENCES project.Projects (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Milestones_Owner')
    ALTER TABLE project.Milestones ADD CONSTRAINT FK_Milestones_Owner
        FOREIGN KEY (OwnerId) REFERENCES core.Users (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Sprints_Organization')
    ALTER TABLE project.Sprints ADD CONSTRAINT FK_Sprints_Organization
        FOREIGN KEY (OrganizationId) REFERENCES core.Organizations (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Sprints_Project')
    ALTER TABLE project.Sprints ADD CONSTRAINT FK_Sprints_Project
        FOREIGN KEY (ProjectId) REFERENCES project.Projects (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Tasks_Organization')
    ALTER TABLE project.Tasks ADD CONSTRAINT FK_Tasks_Organization
        FOREIGN KEY (OrganizationId) REFERENCES core.Organizations (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Tasks_Project')
    ALTER TABLE project.Tasks ADD CONSTRAINT FK_Tasks_Project
        FOREIGN KEY (ProjectId) REFERENCES project.Projects (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Tasks_ParentTask')
    ALTER TABLE project.Tasks ADD CONSTRAINT FK_Tasks_ParentTask
        FOREIGN KEY (ParentTaskId) REFERENCES project.Tasks (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Tasks_Status')
    ALTER TABLE project.Tasks ADD CONSTRAINT FK_Tasks_Status
        FOREIGN KEY (StatusId) REFERENCES project.TaskStatuses (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Tasks_Priority')
    ALTER TABLE project.Tasks ADD CONSTRAINT FK_Tasks_Priority
        FOREIGN KEY (PriorityId) REFERENCES project.TaskPriorities (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Tasks_Assignee')
    ALTER TABLE project.Tasks ADD CONSTRAINT FK_Tasks_Assignee
        FOREIGN KEY (AssigneeId) REFERENCES core.Users (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Tasks_Reporter')
    ALTER TABLE project.Tasks ADD CONSTRAINT FK_Tasks_Reporter
        FOREIGN KEY (ReporterId) REFERENCES core.Users (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Tasks_Milestone')
    ALTER TABLE project.Tasks ADD CONSTRAINT FK_Tasks_Milestone
        FOREIGN KEY (MilestoneId) REFERENCES project.Milestones (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Tasks_Sprint')
    ALTER TABLE project.Tasks ADD CONSTRAINT FK_Tasks_Sprint
        FOREIGN KEY (SprintId) REFERENCES project.Sprints (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_TaskWatchers_Task')
    ALTER TABLE project.TaskWatchers ADD CONSTRAINT FK_TaskWatchers_Task
        FOREIGN KEY (TaskId) REFERENCES project.Tasks (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_TaskWatchers_User')
    ALTER TABLE project.TaskWatchers ADD CONSTRAINT FK_TaskWatchers_User
        FOREIGN KEY (UserId) REFERENCES core.Users (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_SprintTasks_Sprint')
    ALTER TABLE project.SprintTasks ADD CONSTRAINT FK_SprintTasks_Sprint
        FOREIGN KEY (SprintId) REFERENCES project.Sprints (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_SprintTasks_Task')
    ALTER TABLE project.SprintTasks ADD CONSTRAINT FK_SprintTasks_Task
        FOREIGN KEY (TaskId) REFERENCES project.Tasks (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Timesheets_Organization')
    ALTER TABLE project.Timesheets ADD CONSTRAINT FK_Timesheets_Organization
        FOREIGN KEY (OrganizationId) REFERENCES core.Organizations (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Timesheets_User')
    ALTER TABLE project.Timesheets ADD CONSTRAINT FK_Timesheets_User
        FOREIGN KEY (UserId) REFERENCES core.Users (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Timesheets_Project')
    ALTER TABLE project.Timesheets ADD CONSTRAINT FK_Timesheets_Project
        FOREIGN KEY (ProjectId) REFERENCES project.Projects (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Timesheets_Task')
    ALTER TABLE project.Timesheets ADD CONSTRAINT FK_Timesheets_Task
        FOREIGN KEY (TaskId) REFERENCES project.Tasks (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_TimesheetApprovals_Timesheet')
    ALTER TABLE project.TimesheetApprovals ADD CONSTRAINT FK_TimesheetApprovals_Timesheet
        FOREIGN KEY (TimesheetId) REFERENCES project.Timesheets (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_TimesheetApprovals_Approver')
    ALTER TABLE project.TimesheetApprovals ADD CONSTRAINT FK_TimesheetApprovals_Approver
        FOREIGN KEY (ApproverId) REFERENCES core.Users (Id);
GO

-- shared
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Comments_Organization')
    ALTER TABLE shared.Comments ADD CONSTRAINT FK_Comments_Organization
        FOREIGN KEY (OrganizationId) REFERENCES core.Organizations (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Comments_User')
    ALTER TABLE shared.Comments ADD CONSTRAINT FK_Comments_User
        FOREIGN KEY (UserId) REFERENCES core.Users (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Documents_Organization')
    ALTER TABLE shared.Documents ADD CONSTRAINT FK_Documents_Organization
        FOREIGN KEY (OrganizationId) REFERENCES core.Organizations (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Notifications_Organization')
    ALTER TABLE shared.Notifications ADD CONSTRAINT FK_Notifications_Organization
        FOREIGN KEY (OrganizationId) REFERENCES core.Organizations (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Notifications_User')
    ALTER TABLE shared.Notifications ADD CONSTRAINT FK_Notifications_User
        FOREIGN KEY (UserId) REFERENCES core.Users (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_NotificationPreferences_Organization')
    ALTER TABLE shared.NotificationPreferences ADD CONSTRAINT FK_NotificationPreferences_Organization
        FOREIGN KEY (OrganizationId) REFERENCES core.Organizations (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_NotificationPreferences_User')
    ALTER TABLE shared.NotificationPreferences ADD CONSTRAINT FK_NotificationPreferences_User
        FOREIGN KEY (UserId) REFERENCES core.Users (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_AuditLogs_Organization')
    ALTER TABLE shared.AuditLogs ADD CONSTRAINT FK_AuditLogs_Organization
        FOREIGN KEY (OrganizationId) REFERENCES core.Organizations (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CustomFields_Organization')
    ALTER TABLE shared.CustomFields ADD CONSTRAINT FK_CustomFields_Organization
        FOREIGN KEY (OrganizationId) REFERENCES core.Organizations (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CustomFieldValues_Organization')
    ALTER TABLE shared.CustomFieldValues ADD CONSTRAINT FK_CustomFieldValues_Organization
        FOREIGN KEY (OrganizationId) REFERENCES core.Organizations (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CustomFieldValues_CustomField')
    ALTER TABLE shared.CustomFieldValues ADD CONSTRAINT FK_CustomFieldValues_CustomField
        FOREIGN KEY (CustomFieldId) REFERENCES shared.CustomFields (Id);
GO

/* =============================================================================
   INDEXES  (aligned to expected API access patterns)
   ========================================================================== */

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Users_Org_Department' AND object_id = OBJECT_ID(N'core.Users'))
    CREATE INDEX IX_Users_Org_Department ON core.Users (OrganizationId, DepartmentId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Departments_Org' AND object_id = OBJECT_ID(N'core.Departments'))
    CREATE INDEX IX_Departments_Org ON core.Departments (OrganizationId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Clients_Org' AND object_id = OBJECT_ID(N'core.Clients'))
    CREATE INDEX IX_Clients_Org ON core.Clients (OrganizationId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Projects_Org' AND object_id = OBJECT_ID(N'project.Projects'))
    CREATE INDEX IX_Projects_Org ON project.Projects (OrganizationId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Projects_Org_Status' AND object_id = OBJECT_ID(N'project.Projects'))
    CREATE INDEX IX_Projects_Org_Status ON project.Projects (OrganizationId, StatusId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Projects_Org_Manager' AND object_id = OBJECT_ID(N'project.Projects'))
    CREATE INDEX IX_Projects_Org_Manager ON project.Projects (OrganizationId, ManagerId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Projects_Org_Client' AND object_id = OBJECT_ID(N'project.Projects'))
    CREATE INDEX IX_Projects_Org_Client ON project.Projects (OrganizationId, ClientId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Projects_Org_StartDate' AND object_id = OBJECT_ID(N'project.Projects'))
    CREATE INDEX IX_Projects_Org_StartDate ON project.Projects (OrganizationId, StartDate);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Projects_Org_PlannedEndDate' AND object_id = OBJECT_ID(N'project.Projects'))
    CREATE INDEX IX_Projects_Org_PlannedEndDate ON project.Projects (OrganizationId, PlannedEndDate);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Tasks_Org_Project' AND object_id = OBJECT_ID(N'project.Tasks'))
    CREATE INDEX IX_Tasks_Org_Project ON project.Tasks (OrganizationId, ProjectId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Tasks_Project_Status' AND object_id = OBJECT_ID(N'project.Tasks'))
    CREATE INDEX IX_Tasks_Project_Status ON project.Tasks (ProjectId, StatusId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Tasks_Project_Assignee' AND object_id = OBJECT_ID(N'project.Tasks'))
    CREATE INDEX IX_Tasks_Project_Assignee ON project.Tasks (ProjectId, AssigneeId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Tasks_Project_DueDate' AND object_id = OBJECT_ID(N'project.Tasks'))
    CREATE INDEX IX_Tasks_Project_DueDate ON project.Tasks (ProjectId, DueDate);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Tasks_Project_Sprint' AND object_id = OBJECT_ID(N'project.Tasks'))
    CREATE INDEX IX_Tasks_Project_Sprint ON project.Tasks (ProjectId, SprintId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Tasks_Project_Milestone' AND object_id = OBJECT_ID(N'project.Tasks'))
    CREATE INDEX IX_Tasks_Project_Milestone ON project.Tasks (ProjectId, MilestoneId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Tasks_Assignee_Status' AND object_id = OBJECT_ID(N'project.Tasks'))
    CREATE INDEX IX_Tasks_Assignee_Status ON project.Tasks (AssigneeId, StatusId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Tasks_ParentTask' AND object_id = OBJECT_ID(N'project.Tasks'))
    CREATE INDEX IX_Tasks_ParentTask ON project.Tasks (ParentTaskId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Milestones_Project_DueDate' AND object_id = OBJECT_ID(N'project.Milestones'))
    CREATE INDEX IX_Milestones_Project_DueDate ON project.Milestones (ProjectId, DueDate);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Sprints_Project_StartDate' AND object_id = OBJECT_ID(N'project.Sprints'))
    CREATE INDEX IX_Sprints_Project_StartDate ON project.Sprints (ProjectId, StartDate);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProjectMembers_User' AND object_id = OBJECT_ID(N'project.ProjectMembers'))
    CREATE INDEX IX_ProjectMembers_User ON project.ProjectMembers (UserId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SprintTasks_Task' AND object_id = OBJECT_ID(N'project.SprintTasks'))
    CREATE INDEX IX_SprintTasks_Task ON project.SprintTasks (TaskId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Timesheets_Org_User_Date' AND object_id = OBJECT_ID(N'project.Timesheets'))
    CREATE INDEX IX_Timesheets_Org_User_Date ON project.Timesheets (OrganizationId, UserId, Date);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Timesheets_Org_Project_Date' AND object_id = OBJECT_ID(N'project.Timesheets'))
    CREATE INDEX IX_Timesheets_Org_Project_Date ON project.Timesheets (OrganizationId, ProjectId, Date);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Timesheets_Task_Date' AND object_id = OBJECT_ID(N'project.Timesheets'))
    CREATE INDEX IX_Timesheets_Task_Date ON project.Timesheets (TaskId, Date);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Timesheets_Status_Date' AND object_id = OBJECT_ID(N'project.Timesheets'))
    CREATE INDEX IX_Timesheets_Status_Date ON project.Timesheets (Status, Date);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TimesheetApprovals_Timesheet' AND object_id = OBJECT_ID(N'project.TimesheetApprovals'))
    CREATE INDEX IX_TimesheetApprovals_Timesheet ON project.TimesheetApprovals (TimesheetId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Comments_Org_Entity' AND object_id = OBJECT_ID(N'shared.Comments'))
    CREATE INDEX IX_Comments_Org_Entity ON shared.Comments (OrganizationId, EntityType, EntityId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Comments_Entity_Created' AND object_id = OBJECT_ID(N'shared.Comments'))
    CREATE INDEX IX_Comments_Entity_Created ON shared.Comments (EntityType, EntityId, CreatedAt);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Documents_Org_Entity' AND object_id = OBJECT_ID(N'shared.Documents'))
    CREATE INDEX IX_Documents_Org_Entity ON shared.Documents (OrganizationId, EntityType, EntityId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Notifications_User_Read_Created' AND object_id = OBJECT_ID(N'shared.Notifications'))
    CREATE INDEX IX_Notifications_User_Read_Created ON shared.Notifications (UserId, IsRead, CreatedAt);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditLogs_Org_Entity_Created' AND object_id = OBJECT_ID(N'shared.AuditLogs'))
    CREATE INDEX IX_AuditLogs_Org_Entity_Created ON shared.AuditLogs (OrganizationId, EntityType, EntityId, CreatedAt);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditLogs_Org_User_Created' AND object_id = OBJECT_ID(N'shared.AuditLogs'))
    CREATE INDEX IX_AuditLogs_Org_User_Created ON shared.AuditLogs (OrganizationId, UserId, CreatedAt);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditLogs_Org_Created' AND object_id = OBJECT_ID(N'shared.AuditLogs'))
    CREATE INDEX IX_AuditLogs_Org_Created ON shared.AuditLogs (OrganizationId, CreatedAt);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CustomFieldValues_Entity' AND object_id = OBJECT_ID(N'shared.CustomFieldValues'))
    CREATE INDEX IX_CustomFieldValues_Entity ON shared.CustomFieldValues (OrganizationId, EntityType, EntityId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OutboxMessages_Pending' AND object_id = OBJECT_ID(N'shared.OutboxMessages'))
    CREATE INDEX IX_OutboxMessages_Pending ON shared.OutboxMessages (ProcessedAt, CreatedAt);
GO

/* =============================================================================
   SEED: Permissions (global, tenant-independent). Idempotent by Code.
   ========================================================================== */
INSERT INTO core.Permissions (Code, Name, Module, Resource, Action)
SELECT v.Code, v.Name, v.Module, v.Resource, v.Action
FROM (VALUES
    (N'project.read',     N'View Projects',        N'project', N'project',   N'read'),
    (N'project.create',   N'Create Projects',      N'project', N'project',   N'create'),
    (N'project.update',   N'Update Projects',      N'project', N'project',   N'update'),
    (N'project.delete',   N'Delete Projects',      N'project', N'project',   N'delete'),
    (N'task.read',        N'View Tasks',           N'project', N'task',      N'read'),
    (N'task.create',      N'Create Tasks',         N'project', N'task',      N'create'),
    (N'task.update',      N'Update Tasks',         N'project', N'task',      N'update'),
    (N'task.delete',      N'Delete Tasks',         N'project', N'task',      N'delete'),
    (N'task.assign',      N'Assign Tasks',         N'project', N'task',      N'assign'),
    (N'milestone.read',   N'View Milestones',      N'project', N'milestone', N'read'),
    (N'milestone.create', N'Create Milestones',    N'project', N'milestone', N'create'),
    (N'milestone.update', N'Update Milestones',    N'project', N'milestone', N'update'),
    (N'milestone.delete', N'Delete Milestones',    N'project', N'milestone', N'delete'),
    (N'sprint.read',      N'View Sprints',         N'project', N'sprint',    N'read'),
    (N'sprint.create',    N'Create Sprints',       N'project', N'sprint',    N'create'),
    (N'sprint.update',    N'Update Sprints',       N'project', N'sprint',    N'update'),
    (N'sprint.delete',    N'Delete Sprints',       N'project', N'sprint',    N'delete'),
    (N'timesheet.read',   N'View Timesheets',      N'project', N'timesheet', N'read'),
    (N'timesheet.create', N'Create Timesheets',    N'project', N'timesheet', N'create'),
    (N'timesheet.update', N'Update Timesheets',    N'project', N'timesheet', N'update'),
    (N'timesheet.approve',N'Approve Timesheets',   N'project', N'timesheet', N'approve'),
    (N'timesheet.reject', N'Reject Timesheets',    N'project', N'timesheet', N'reject'),
    (N'document.read',    N'View Documents',       N'shared',  N'document',  N'read'),
    (N'document.create',  N'Upload Documents',     N'shared',  N'document',  N'create'),
    (N'document.delete',  N'Delete Documents',     N'shared',  N'document',  N'delete'),
    (N'audit.read',       N'View Audit Logs',      N'shared',  N'audit',     N'read'),
    (N'notification.read',N'View Notifications',   N'shared',  N'notification', N'read')
) AS v(Code, Name, Module, Resource, Action)
WHERE NOT EXISTS (SELECT 1 FROM core.Permissions p WHERE p.Code = v.Code);
GO

/* =============================================================================
   PROCEDURE: core.usp_SeedOrganizationDefaults
   Idempotently seeds tenant-scoped defaults (roles, statuses, priorities)
   and default role -> permission mappings for a given organization.
   ========================================================================== */
CREATE OR ALTER PROCEDURE core.usp_SeedOrganizationDefaults
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM core.Organizations WHERE Id = @OrganizationId)
    BEGIN
        THROW 50001, N'Organization does not exist.', 1;
    END

    -- Roles
    INSERT INTO core.Roles (OrganizationId, Name, Description, IsSystemRole)
    SELECT @OrganizationId, v.Name, v.Description, 1
    FROM (VALUES
        (N'SUPER_ADMIN',     N'Full platform access'),
        (N'ADMIN',           N'Organization administrator'),
        (N'PROJECT_MANAGER', N'Manages projects and teams'),
        (N'PROJECT_MEMBER',  N'Works on assigned tasks'),
        (N'VIEWER',          N'Read-only access')
    ) AS v(Name, Description)
    WHERE NOT EXISTS (SELECT 1 FROM core.Roles r WHERE r.OrganizationId = @OrganizationId AND r.Name = v.Name);

    -- Project statuses
    INSERT INTO project.ProjectStatuses (OrganizationId, Code, Name, DisplayOrder, IsDefault, IsFinal)
    SELECT @OrganizationId, v.Code, v.Name, v.DisplayOrder, v.IsDefault, v.IsFinal
    FROM (VALUES
        (N'PLANNING',  N'Planning',   1, 1, 0),
        (N'ACTIVE',    N'Active',     2, 0, 0),
        (N'ON_HOLD',   N'On Hold',    3, 0, 0),
        (N'COMPLETED', N'Completed',  4, 0, 1),
        (N'CANCELLED', N'Cancelled',  5, 0, 1)
    ) AS v(Code, Name, DisplayOrder, IsDefault, IsFinal)
    WHERE NOT EXISTS (SELECT 1 FROM project.ProjectStatuses s WHERE s.OrganizationId = @OrganizationId AND s.Code = v.Code);

    -- Project priorities
    INSERT INTO project.ProjectPriorities (OrganizationId, Code, Name, DisplayOrder)
    SELECT @OrganizationId, v.Code, v.Name, v.DisplayOrder
    FROM (VALUES
        (N'LOW', N'Low', 1), (N'MEDIUM', N'Medium', 2), (N'HIGH', N'High', 3), (N'CRITICAL', N'Critical', 4)
    ) AS v(Code, Name, DisplayOrder)
    WHERE NOT EXISTS (SELECT 1 FROM project.ProjectPriorities p WHERE p.OrganizationId = @OrganizationId AND p.Code = v.Code);

    -- Task statuses
    INSERT INTO project.TaskStatuses (OrganizationId, Code, Name, DisplayOrder, IsFinal)
    SELECT @OrganizationId, v.Code, v.Name, v.DisplayOrder, v.IsFinal
    FROM (VALUES
        (N'TODO',        N'To Do',       1, 0),
        (N'IN_PROGRESS', N'In Progress', 2, 0),
        (N'BLOCKED',     N'Blocked',     3, 0),
        (N'IN_REVIEW',   N'In Review',   4, 0),
        (N'DONE',        N'Done',        5, 1),
        (N'CANCELLED',   N'Cancelled',   6, 1)
    ) AS v(Code, Name, DisplayOrder, IsFinal)
    WHERE NOT EXISTS (SELECT 1 FROM project.TaskStatuses s WHERE s.OrganizationId = @OrganizationId AND s.Code = v.Code);

    -- Task priorities
    INSERT INTO project.TaskPriorities (OrganizationId, Code, Name, DisplayOrder)
    SELECT @OrganizationId, v.Code, v.Name, v.DisplayOrder
    FROM (VALUES
        (N'LOW', N'Low', 1), (N'MEDIUM', N'Medium', 2), (N'HIGH', N'High', 3), (N'CRITICAL', N'Critical', 4)
    ) AS v(Code, Name, DisplayOrder)
    WHERE NOT EXISTS (SELECT 1 FROM project.TaskPriorities p WHERE p.OrganizationId = @OrganizationId AND p.Code = v.Code);

    -- Role -> permission mappings
    ;WITH RoleMap AS (
        SELECT r.Id AS RoleId, r.Name AS RoleName FROM core.Roles r WHERE r.OrganizationId = @OrganizationId
    ),
    Grants AS (
        -- SUPER_ADMIN and ADMIN: all permissions
        SELECT rm.RoleId, p.Id AS PermissionId
        FROM RoleMap rm CROSS JOIN core.Permissions p
        WHERE rm.RoleName IN (N'SUPER_ADMIN', N'ADMIN')
        UNION
        -- PROJECT_MANAGER: everything except delete of projects
        SELECT rm.RoleId, p.Id
        FROM RoleMap rm JOIN core.Permissions p ON p.Code <> N'project.delete'
        WHERE rm.RoleName = N'PROJECT_MANAGER'
        UNION
        -- PROJECT_MEMBER: read + create/update on task/timesheet/document + read comment/notification
        SELECT rm.RoleId, p.Id
        FROM RoleMap rm JOIN core.Permissions p
            ON p.Code IN (N'project.read', N'task.read', N'task.create', N'task.update', N'task.assign',
                          N'milestone.read', N'sprint.read',
                          N'timesheet.read', N'timesheet.create', N'timesheet.update',
                          N'document.read', N'document.create', N'notification.read')
        WHERE rm.RoleName = N'PROJECT_MEMBER'
        UNION
        -- VIEWER: all read permissions
        SELECT rm.RoleId, p.Id
        FROM RoleMap rm JOIN core.Permissions p ON p.Action = N'read'
        WHERE rm.RoleName = N'VIEWER'
    )
    INSERT INTO core.RolePermissions (RoleId, PermissionId)
    SELECT g.RoleId, g.PermissionId
    FROM Grants g
    WHERE NOT EXISTS (
        SELECT 1 FROM core.RolePermissions rp
        WHERE rp.RoleId = g.RoleId AND rp.PermissionId = g.PermissionId
    );
END
GO



