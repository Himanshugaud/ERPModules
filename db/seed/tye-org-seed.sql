/* =============================================================================
   Seed: The Young Entrepreneurs (TYE) — construction organization
   Roles: CEO, Execution Head, Manager  |  Sample projects/tasks/subtasks
   Re-runnable: clears existing TYE data first, then re-inserts.
   ========================================================================== */

SET NOCOUNT ON;

DECLARE @Org  UNIQUEIDENTIFIER = '2A000000-0000-0000-0000-000000000001';

/* Roles */
DECLARE @RoleCEO  UNIQUEIDENTIFIER = '2A000000-0000-0000-0000-0000000000C0';
DECLARE @RoleExec UNIQUEIDENTIFIER = '2A000000-0000-0000-0000-0000000000E0';
DECLARE @RoleMgr  UNIQUEIDENTIFIER = '2A000000-0000-0000-0000-0000000000A0';

/* Users */
DECLARE @UserCEO  UNIQUEIDENTIFIER = '2A000000-0000-0000-0000-000000000201';
DECLARE @UserExec UNIQUEIDENTIFIER = '2A000000-0000-0000-0000-000000000202';
DECLARE @UserMgr  UNIQUEIDENTIFIER = '2A000000-0000-0000-0000-000000000203';

/* Project statuses */
DECLARE @PS_Planning  UNIQUEIDENTIFIER = '2A000000-0000-0000-0000-000000000301';
DECLARE @PS_Active    UNIQUEIDENTIFIER = '2A000000-0000-0000-0000-000000000302';
DECLARE @PS_OnHold    UNIQUEIDENTIFIER = '2A000000-0000-0000-0000-000000000303';
DECLARE @PS_Completed UNIQUEIDENTIFIER = '2A000000-0000-0000-0000-000000000304';
DECLARE @PS_Cancelled UNIQUEIDENTIFIER = '2A000000-0000-0000-0000-000000000305';

/* Task statuses */
DECLARE @TS_Todo      UNIQUEIDENTIFIER = '2A000000-0000-0000-0000-000000000311';
DECLARE @TS_InProg    UNIQUEIDENTIFIER = '2A000000-0000-0000-0000-000000000312';
DECLARE @TS_Blocked   UNIQUEIDENTIFIER = '2A000000-0000-0000-0000-000000000313';
DECLARE @TS_Review    UNIQUEIDENTIFIER = '2A000000-0000-0000-0000-000000000314';
DECLARE @TS_Done      UNIQUEIDENTIFIER = '2A000000-0000-0000-0000-000000000315';
DECLARE @TS_Cancelled UNIQUEIDENTIFIER = '2A000000-0000-0000-0000-000000000316';

/* Priorities */
DECLARE @PP_Low  UNIQUEIDENTIFIER = '2A000000-0000-0000-0000-000000000321';
DECLARE @PP_Med  UNIQUEIDENTIFIER = '2A000000-0000-0000-0000-000000000322';
DECLARE @PP_High UNIQUEIDENTIFIER = '2A000000-0000-0000-0000-000000000323';
DECLARE @PP_Crit UNIQUEIDENTIFIER = '2A000000-0000-0000-0000-000000000324';
DECLARE @TP_Low  UNIQUEIDENTIFIER = '2A000000-0000-0000-0000-000000000331';
DECLARE @TP_Med  UNIQUEIDENTIFIER = '2A000000-0000-0000-0000-000000000332';
DECLARE @TP_High UNIQUEIDENTIFIER = '2A000000-0000-0000-0000-000000000333';
DECLARE @TP_Crit UNIQUEIDENTIFIER = '2A000000-0000-0000-0000-000000000334';

/* Clients */
DECLARE @Client1 UNIQUEIDENTIFIER = '2A000000-0000-0000-0000-000000000341';
DECLARE @Client2 UNIQUEIDENTIFIER = '2A000000-0000-0000-0000-000000000342';

/* Projects */
DECLARE @P1 UNIQUEIDENTIFIER = '2A000000-0000-0000-0000-000000000401';
DECLARE @P2 UNIQUEIDENTIFIER = '2A000000-0000-0000-0000-000000000402';
DECLARE @P3 UNIQUEIDENTIFIER = '2A000000-0000-0000-0000-000000000403';

/* ---------------------------------------------------------------------------
   Clean up any existing TYE data (child -> parent order)
   ------------------------------------------------------------------------ */
DELETE tw FROM project.TaskWatchers tw
    INNER JOIN project.Tasks t ON t.Id = tw.TaskId WHERE t.OrganizationId = @Org;
DELETE FROM project.Tasks           WHERE OrganizationId = @Org;
DELETE FROM project.ProjectMembers  WHERE ProjectId IN (@P1, @P2, @P3);
DELETE FROM project.Projects        WHERE OrganizationId = @Org;
DELETE FROM project.Milestones      WHERE OrganizationId = @Org;
DELETE FROM project.TaskStatuses    WHERE OrganizationId = @Org;
DELETE FROM project.TaskPriorities  WHERE OrganizationId = @Org;
DELETE FROM project.ProjectStatuses WHERE OrganizationId = @Org;
DELETE FROM project.ProjectPriorities WHERE OrganizationId = @Org;
DELETE FROM core.UserRoles       WHERE UserId IN (@UserCEO, @UserExec, @UserMgr);
DELETE FROM core.RolePermissions WHERE RoleId IN (@RoleCEO, @RoleExec, @RoleMgr);
DELETE FROM core.Clients WHERE OrganizationId = @Org;
DELETE FROM core.Users   WHERE OrganizationId = @Org;
DELETE FROM core.Roles   WHERE OrganizationId = @Org;
DELETE FROM core.Organizations WHERE Id = @Org;

/* ---------------------------------------------------------------------------
   Organization
   ------------------------------------------------------------------------ */
INSERT INTO core.Organizations (Id, Code, Name, LegalName, Description, Email, CountryCode, Timezone, CurrencyCode, Status)
VALUES (@Org, N'TYE', N'The Young Entrepreneurs', N'The Young Entrepreneurs Pvt. Ltd.',
        N'Construction and infrastructure development company.', N'contact@tye.local',
        N'IN', N'Asia/Kolkata', 'INR', N'ACTIVE');

/* ---------------------------------------------------------------------------
   Roles
   ------------------------------------------------------------------------ */
INSERT INTO core.Roles (Id, OrganizationId, Name, Description, IsSystemRole, IsActive) VALUES
    (@RoleCEO,  @Org, N'CEO',            N'Chief Executive Officer — full organization access.', 0, 1),
    (@RoleExec, @Org, N'Execution Head', N'Heads on-ground project execution and delivery.',      0, 1),
    (@RoleMgr,  @Org, N'Manager',        N'Manages assigned projects, tasks and teams.',          0, 1);

/* Role permissions: CEO & Execution Head get everything; Manager gets all except admin (role/permission/user.delete) */
INSERT INTO core.RolePermissions (RoleId, PermissionId)
    SELECT @RoleCEO, p.Id FROM core.Permissions p WHERE p.IsActive = 1;
INSERT INTO core.RolePermissions (RoleId, PermissionId)
    SELECT @RoleExec, p.Id FROM core.Permissions p WHERE p.IsActive = 1;
INSERT INTO core.RolePermissions (RoleId, PermissionId)
    SELECT @RoleMgr, p.Id FROM core.Permissions p
    WHERE p.IsActive = 1
      AND p.Code NOT LIKE 'role.%'
      AND p.Code NOT LIKE 'permission.%'
      AND p.Code <> 'user.delete';

/* ---------------------------------------------------------------------------
   Users
   ------------------------------------------------------------------------ */
INSERT INTO core.Users (Id, OrganizationId, Email, FirstName, LastName, DisplayName, JobTitle, Status) VALUES
    (@UserCEO,  @Org, N'ceo@tye.local',     N'Arjun',  N'Mehta',  N'Arjun Mehta',  N'Chief Executive Officer', N'ACTIVE'),
    (@UserExec, @Org, N'exec@tye.local',    N'Priya',  N'Nair',   N'Priya Nair',   N'Execution Head',          N'ACTIVE'),
    (@UserMgr,  @Org, N'manager@tye.local', N'Rohan',  N'Kapoor', N'Rohan Kapoor', N'Project Manager',         N'ACTIVE');

INSERT INTO core.UserRoles (UserId, RoleId) VALUES
    (@UserCEO,  @RoleCEO),
    (@UserExec, @RoleExec),
    (@UserMgr,  @RoleMgr);

/* ---------------------------------------------------------------------------
   Lookups: statuses & priorities
   ------------------------------------------------------------------------ */
INSERT INTO project.ProjectStatuses (Id, OrganizationId, Code, Name, DisplayOrder, IsDefault, IsFinal) VALUES
    (@PS_Planning,  @Org, N'PLANNING',  N'Planning',  1, 1, 0),
    (@PS_Active,    @Org, N'ACTIVE',    N'Active',    2, 0, 0),
    (@PS_OnHold,    @Org, N'ON_HOLD',   N'On Hold',   3, 0, 0),
    (@PS_Completed, @Org, N'COMPLETED', N'Completed', 4, 0, 1),
    (@PS_Cancelled, @Org, N'CANCELLED', N'Cancelled', 5, 0, 1);

INSERT INTO project.TaskStatuses (Id, OrganizationId, Code, Name, DisplayOrder, IsFinal) VALUES
    (@TS_Todo,      @Org, N'TODO',        N'To Do',       1, 0),
    (@TS_InProg,    @Org, N'IN_PROGRESS', N'In Progress', 2, 0),
    (@TS_Blocked,   @Org, N'BLOCKED',     N'Blocked',     3, 0),
    (@TS_Review,    @Org, N'IN_REVIEW',   N'In Review',   4, 0),
    (@TS_Done,      @Org, N'DONE',        N'Done',        5, 1),
    (@TS_Cancelled, @Org, N'CANCELLED',   N'Cancelled',   6, 1);

INSERT INTO project.ProjectPriorities (Id, OrganizationId, Code, Name, DisplayOrder) VALUES
    (@PP_Low,  @Org, N'LOW',      N'Low',      1),
    (@PP_Med,  @Org, N'MEDIUM',   N'Medium',   2),
    (@PP_High, @Org, N'HIGH',     N'High',     3),
    (@PP_Crit, @Org, N'CRITICAL', N'Critical', 4);

INSERT INTO project.TaskPriorities (Id, OrganizationId, Code, Name, DisplayOrder) VALUES
    (@TP_Low,  @Org, N'LOW',      N'Low',      1),
    (@TP_Med,  @Org, N'MEDIUM',   N'Medium',   2),
    (@TP_High, @Org, N'HIGH',     N'High',     3),
    (@TP_Crit, @Org, N'CRITICAL', N'Critical', 4);

/* ---------------------------------------------------------------------------
   Clients
   ------------------------------------------------------------------------ */
INSERT INTO core.Clients (Id, OrganizationId, Name, Code, Email, Status) VALUES
    (@Client1, @Org, N'Skyline Developers', N'SKY', N'projects@skyline.local', N'ACTIVE'),
    (@Client2, @Org, N'Urban Infra Ltd',    N'URB', N'contracts@urbaninfra.local', N'ACTIVE');

/* ---------------------------------------------------------------------------
   Projects
   ------------------------------------------------------------------------ */
INSERT INTO project.Projects
    (Id, OrganizationId, Code, Name, Description, ClientId, ManagerId, StatusId, PriorityId,
     StartDate, PlannedEndDate, CompletionPercentage, Budget, CurrencyCode)
VALUES
    (@P1, @Org, N'PRJ-TYE-01', N'Green Valley Residency',
        N'150-unit residential township with clubhouse and landscaped gardens.',
        @Client1, @UserMgr, @PS_Active, @PP_High,
        '2026-03-01', '2027-02-28', 35, 45000000, 'INR'),
    (@P2, @Org, N'PRJ-TYE-02', N'Metro Mall Complex',
        N'Four-storey commercial shopping mall with basement parking.',
        @Client2, @UserMgr, @PS_Planning, @PP_Med,
        '2026-09-15', '2028-03-31', 10, 82000000, 'INR'),
    (@P3, @Org, N'PRJ-TYE-03', N'Riverside Bridge',
        N'420-metre cable-stayed bridge across the Yamuna riverfront.',
        @Client2, @UserExec, @PS_Active, @PP_Crit,
        '2026-01-10', '2027-06-30', 60, 130000000, 'INR');

INSERT INTO project.ProjectMembers (ProjectId, UserId, ProjectRole, AllocationPercentage) VALUES
    (@P1, @UserMgr,  N'Manager', 100),
    (@P1, @UserExec, N'Execution Head', 40),
    (@P2, @UserMgr,  N'Manager', 100),
    (@P3, @UserExec, N'Execution Head', 100),
    (@P3, @UserMgr,  N'Manager', 50);

/* ---------------------------------------------------------------------------
   Tasks & subtasks
   ------------------------------------------------------------------------ */
DECLARE @T1  UNIQUEIDENTIFIER = '2A000000-0000-0000-0000-000000000501';
DECLARE @T2  UNIQUEIDENTIFIER = '2A000000-0000-0000-0000-000000000502';
DECLARE @T3  UNIQUEIDENTIFIER = '2A000000-0000-0000-0000-000000000503';
DECLARE @T4  UNIQUEIDENTIFIER = '2A000000-0000-0000-0000-000000000504';
DECLARE @T5  UNIQUEIDENTIFIER = '2A000000-0000-0000-0000-000000000505';
DECLARE @T6  UNIQUEIDENTIFIER = '2A000000-0000-0000-0000-000000000506';
DECLARE @T7  UNIQUEIDENTIFIER = '2A000000-0000-0000-0000-000000000507';

/* Parent tasks */
INSERT INTO project.Tasks
    (Id, OrganizationId, ProjectId, ParentTaskId, Title, Description, StatusId, PriorityId,
     AssigneeId, ReporterId, StartDate, DueDate, EstimatedHours, CompletionPercentage)
VALUES
    (@T1, @Org, @P1, NULL, N'Site Excavation',      N'Excavate and level the construction site.',        @TS_Done,   @TP_High, @UserMgr,  @UserExec, '2026-03-01', '2026-03-20', 160, 100),
    (@T2, @Org, @P1, NULL, N'Foundation Work',      N'Lay footings, rebar and pour foundation concrete.', @TS_InProg, @TP_High, @UserMgr,  @UserExec, '2026-03-21', '2026-05-15', 320, 45),
    (@T3, @Org, @P1, NULL, N'Structural Framing',   N'Erect RCC columns, beams and slabs.',               @TS_Todo,   @TP_Med,  @UserExec, @UserMgr,  '2026-05-16', '2026-08-30', 480, 0),
    (@T4, @Org, @P2, NULL, N'Design Approval',      N'Obtain architectural and municipal approvals.',     @TS_InProg, @TP_High, @UserMgr,  @UserCEO,  '2026-09-15', '2026-11-30', 120, 40),
    (@T5, @Org, @P2, NULL, N'Budget Estimation',    N'Prepare detailed cost estimate and BOQ.',           @TS_Todo,   @TP_Med,  @UserMgr,  @UserCEO,  '2026-10-01', '2026-10-31', 80,  0),
    (@T6, @Org, @P3, NULL, N'Pile Foundation',      N'Bored pile foundation for bridge pylons.',          @TS_InProg, @TP_Crit, @UserExec, @UserCEO,  '2026-01-10', '2026-04-30', 600, 55),
    (@T7, @Org, @P3, NULL, N'Deck Construction',    N'Cast-in-situ bridge deck and cable stays.',         @TS_Todo,   @TP_High, @UserExec, @UserCEO,  '2026-05-01', '2027-03-31', 900, 0);

/* Subtasks */
INSERT INTO project.Tasks
    (Id, OrganizationId, ProjectId, ParentTaskId, Title, Description, StatusId, PriorityId,
     AssigneeId, ReporterId, StartDate, DueDate, EstimatedHours, CompletionPercentage)
VALUES
    (NEWID(), @Org, @P1, @T1, N'Soil testing',                 N'Geotechnical soil survey and report.',      @TS_Done,   @TP_High, @UserMgr,  @UserExec, '2026-03-01', '2026-03-05', 24, 100),
    (NEWID(), @Org, @P1, @T1, N'Clear vegetation',             N'Remove trees, shrubs and debris.',          @TS_Done,   @TP_Med,  @UserMgr,  @UserExec, '2026-03-06', '2026-03-12', 40, 100),
    (NEWID(), @Org, @P1, @T2, N'Rebar installation',           N'Fabricate and tie reinforcement cages.',    @TS_InProg, @TP_High, @UserMgr,  @UserExec, '2026-03-21', '2026-04-20', 120, 60),
    (NEWID(), @Org, @P1, @T2, N'Concrete pouring',             N'Pour and cure M30 foundation concrete.',    @TS_Todo,   @TP_High, @UserMgr,  @UserExec, '2026-04-21', '2026-05-15', 100, 0),
    (NEWID(), @Org, @P2, @T4, N'Submit blueprints to municipality', N'File drawings for building permit.',   @TS_InProg, @TP_High, @UserMgr,  @UserCEO,  '2026-09-15', '2026-10-15', 40, 50),
    (NEWID(), @Org, @P3, @T6, N'Drilling boreholes',           N'Drill boreholes for pile casing.',          @TS_Done,   @TP_Crit, @UserExec, @UserCEO,  '2026-01-10', '2026-02-28', 200, 100),
    (NEWID(), @Org, @P3, @T6, N'Install steel piles',          N'Lower and grout steel piles.',              @TS_InProg, @TP_Crit, @UserExec, @UserCEO,  '2026-03-01', '2026-04-30', 220, 40);

/* ---------------------------------------------------------------------------
   Summary
   ------------------------------------------------------------------------ */
SELECT
    (SELECT COUNT(*) FROM core.Roles   WHERE OrganizationId = @Org) AS Roles,
    (SELECT COUNT(*) FROM core.Users   WHERE OrganizationId = @Org) AS Users,
    (SELECT COUNT(*) FROM project.Projects WHERE OrganizationId = @Org) AS Projects,
    (SELECT COUNT(*) FROM project.Tasks    WHERE OrganizationId = @Org AND ParentTaskId IS NULL) AS ParentTasks,
    (SELECT COUNT(*) FROM project.Tasks    WHERE OrganizationId = @Org AND ParentTaskId IS NOT NULL) AS Subtasks;
