/*
  Replaces legacy TYE users with the employees supplied from the organization chart.
  Operational ownership is remapped before deletion so projects and tasks remain valid.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @orgId UNIQUEIDENTIFIER = (SELECT Id FROM core.Organizations WHERE Code = N'TYE');
IF @orgId IS NULL THROW 50000, 'TYE organization not found.', 1;

DECLARE @UserMap TABLE (OldUserId UNIQUEIDENTIFIER PRIMARY KEY, NewUserId UNIQUEIDENTIFIER NOT NULL);
INSERT INTO @UserMap (OldUserId, NewUserId) VALUES
    ('2a000000-0000-0000-0000-000000000201', '2ffa6930-89d3-404c-a632-b86f325e3c22'), -- CEO -> Harish
    ('2a000000-0000-0000-0000-000000000202', 'ecfbf56a-aa69-4374-ae67-df1988dd07d7'), -- Execution Head -> Nikhil
    ('2a000000-0000-0000-0000-000000000203', '408a0314-fb97-4b86-942c-118ac69ce893'), -- Manager -> Kamlesh
    ('9ac33712-6968-4c4b-9c8b-982ed9aa07c0', 'ecfbf56a-aa69-4374-ae67-df1988dd07d7'),
    ('a1529c2f-3e3e-4cac-801f-a1ac7f2e296a', 'ecfbf56a-aa69-4374-ae67-df1988dd07d7');

UPDATE d SET ManagerId = m.NewUserId
FROM core.Departments d JOIN @UserMap m ON m.OldUserId = d.ManagerId;
UPDATE p SET ManagerId = m.NewUserId
FROM project.Projects p JOIN @UserMap m ON m.OldUserId = p.ManagerId;
UPDATE ms SET OwnerId = m.NewUserId
FROM project.Milestones ms JOIN @UserMap m ON m.OldUserId = ms.OwnerId;
UPDATE t SET AssigneeId = m.NewUserId
FROM project.Tasks t JOIN @UserMap m ON m.OldUserId = t.AssigneeId;
UPDATE t SET ReporterId = m.NewUserId
FROM project.Tasks t JOIN @UserMap m ON m.OldUserId = t.ReporterId;
UPDATE ta SET ApproverId = m.NewUserId
FROM project.TimesheetApprovals ta JOIN @UserMap m ON m.OldUserId = ta.ApproverId;
UPDATE c SET UserId = m.NewUserId
FROM shared.Comments c JOIN @UserMap m ON m.OldUserId = c.UserId;

INSERT INTO project.ProjectMembers (ProjectId, UserId, ProjectRole, AllocationPercentage, StartDate, EndDate, Status, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
SELECT pm.ProjectId, m.NewUserId, pm.ProjectRole, pm.AllocationPercentage, pm.StartDate, pm.EndDate, pm.Status, pm.CreatedAt, pm.UpdatedAt, pm.CreatedBy, pm.UpdatedBy
FROM project.ProjectMembers pm
JOIN @UserMap m ON m.OldUserId = pm.UserId
WHERE NOT EXISTS (
    SELECT 1 FROM project.ProjectMembers existing
    WHERE existing.ProjectId = pm.ProjectId AND existing.UserId = m.NewUserId
);
DELETE pm FROM project.ProjectMembers pm JOIN @UserMap m ON m.OldUserId = pm.UserId;

INSERT INTO project.TaskWatchers (TaskId, UserId, CreatedAt)
SELECT tw.TaskId, m.NewUserId, tw.CreatedAt
FROM project.TaskWatchers tw
JOIN @UserMap m ON m.OldUserId = tw.UserId
WHERE NOT EXISTS (
    SELECT 1 FROM project.TaskWatchers existing
    WHERE existing.TaskId = tw.TaskId AND existing.UserId = m.NewUserId
);
DELETE tw FROM project.TaskWatchers tw JOIN @UserMap m ON m.OldUserId = tw.UserId;

UPDATE ts SET UserId = m.NewUserId
FROM project.Timesheets ts JOIN @UserMap m ON m.OldUserId = ts.UserId;
UPDATE np SET UserId = m.NewUserId
FROM shared.NotificationPreferences np JOIN @UserMap m ON m.OldUserId = np.UserId;
UPDATE n SET UserId = m.NewUserId
FROM shared.Notifications n JOIN @UserMap m ON m.OldUserId = n.UserId;

DELETE ur FROM core.UserRoles ur JOIN @UserMap m ON m.OldUserId = ur.UserId;
DELETE u FROM core.Users u JOIN @UserMap m ON m.OldUserId = u.Id WHERE u.OrganizationId = @orgId;

COMMIT TRANSACTION;

SELECT u.Id, u.DisplayName, u.Email, u.JobTitle, r.Name AS RoleName
FROM core.Users u
LEFT JOIN core.UserRoles ur ON ur.UserId = u.Id
LEFT JOIN core.Roles r ON r.Id = ur.RoleId
WHERE u.OrganizationId = @orgId
ORDER BY u.DisplayName, r.Name;
