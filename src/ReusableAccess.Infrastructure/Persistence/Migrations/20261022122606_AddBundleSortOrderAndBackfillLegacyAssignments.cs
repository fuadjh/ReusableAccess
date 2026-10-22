using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReusableAccess.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBundleSortOrderAndBackfillLegacyAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "UserAccessBundles",
                type: "int",
                nullable: false,
                defaultValue: 0);
            migrationBuilder.Sql(
    """
    DECLARE @LegacyAssignments TABLE
    (
        AssignmentId uniqueidentifier NOT NULL PRIMARY KEY,
        BundleId uniqueidentifier NOT NULL,
        UserId uniqueidentifier NOT NULL,
        RoleId uniqueidentifier NOT NULL,
        ScopeId uniqueidentifier NOT NULL,
        IsEnabled bit NOT NULL,
        BundleName nvarchar(200) NOT NULL,
        SortOrder int NOT NULL
    );

    INSERT INTO @LegacyAssignments
    (
        AssignmentId,
        BundleId,
        UserId,
        RoleId,
        ScopeId,
        IsEnabled,
        BundleName,
        SortOrder
    )
    SELECT
        assignment.Id,
        NEWID(),
        assignment.UserId,
        assignment.RoleId,
        assignment.ScopeId,
        assignment.IsEnabled,
        LEFT(
            CONCAT(role.DisplayName, N' — ', accessScope.DisplayName),
            200),
        CONVERT(
            int,
            ROW_NUMBER() OVER
            (
                PARTITION BY assignment.UserId
                ORDER BY role.Code, accessScope.Code, assignment.Id
            ) - 1)
    FROM dbo.UserRoleAssignments AS assignment
    INNER JOIN dbo.Roles AS role
        ON role.Id = assignment.RoleId
    INNER JOIN dbo.AccessScopes AS accessScope
        ON accessScope.Id = assignment.ScopeId;

    INSERT INTO dbo.UserAccessBundles
    (
        Id,
        UserId,
        Name,
        ScopeSelectionMode,
        IsEnabled,
        SortOrder
    )
    SELECT
        BundleId,
        UserId,
        BundleName,
        1,
        IsEnabled,
        SortOrder
    FROM @LegacyAssignments;

    INSERT INTO dbo.UserAccessBundleRoles (BundleId, RoleId)
    SELECT BundleId, RoleId
    FROM @LegacyAssignments;

    INSERT INTO dbo.UserAccessBundleScopes (BundleId, ScopeId)
    SELECT BundleId, ScopeId
    FROM @LegacyAssignments;
    """);
            migrationBuilder.CreateIndex(
                name: "IX_UserAccessBundles_UserId_SortOrder",
                table: "UserAccessBundles",
                columns: new[] { "UserId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserAccessBundles_UserId_SortOrder",
                table: "UserAccessBundles");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "UserAccessBundles");
        }
    }
}
