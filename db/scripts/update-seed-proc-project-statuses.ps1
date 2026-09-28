$ErrorActionPreference = "Stop"
$tok = az account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv

$server = "erpmodulesqlserver.database.windows.net"
$db = "erpmodulessqldb"

$procQuery = @"
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
        (N'PLANNING',             N'Planning',             1, 1, 0),
        (N'INVENTORY_CHECK',      N'Inventory Check',      2, 0, 0),
        (N'SHIPMENT_IN_TRANSIT',  N'Shipment In Transit',  3, 0, 0),
        (N'SHIPMENT_COMPLETED',   N'Shipment Completed',   4, 0, 0),
        (N'ACTIVE',               N'Active',               5, 0, 0),
        (N'ON_HOLD',              N'On Hold',              6, 0, 0),
        (N'COMPLETED',            N'Completed',            7, 0, 1),
        (N'CANCELLED',            N'Cancelled',            8, 0, 1)
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
"@
Invoke-Sqlcmd -ServerInstance $server -Database $db -AccessToken $tok -Query $procQuery
Write-Output "usp_SeedOrganizationDefaults updated."
