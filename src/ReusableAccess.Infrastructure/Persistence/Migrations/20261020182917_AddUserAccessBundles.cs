using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReusableAccess.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAccessBundles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserAccessBundles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ScopeSelectionMode = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAccessBundles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserAccessBundles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserAccessBundleRoles",
                columns: table => new
                {
                    BundleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAccessBundleRoles", x => new { x.BundleId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserAccessBundleRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserAccessBundleRoles_UserAccessBundles_BundleId",
                        column: x => x.BundleId,
                        principalTable: "UserAccessBundles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserAccessBundleScopes",
                columns: table => new
                {
                    BundleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScopeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAccessBundleScopes", x => new { x.BundleId, x.ScopeId });
                    table.ForeignKey(
                        name: "FK_UserAccessBundleScopes_AccessScopes_ScopeId",
                        column: x => x.ScopeId,
                        principalTable: "AccessScopes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserAccessBundleScopes_UserAccessBundles_BundleId",
                        column: x => x.BundleId,
                        principalTable: "UserAccessBundles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserAccessBundleRoles_RoleId",
                table: "UserAccessBundleRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAccessBundles_UserId_IsEnabled",
                table: "UserAccessBundles",
                columns: new[] { "UserId", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_UserAccessBundleScopes_ScopeId",
                table: "UserAccessBundleScopes",
                column: "ScopeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserAccessBundleRoles");

            migrationBuilder.DropTable(
                name: "UserAccessBundleScopes");

            migrationBuilder.DropTable(
                name: "UserAccessBundles");
        }
    }
}
