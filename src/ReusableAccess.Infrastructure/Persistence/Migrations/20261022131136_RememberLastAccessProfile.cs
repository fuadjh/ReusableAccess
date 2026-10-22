using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReusableAccess.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RememberLastAccessProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LastSelectedAccessBundleId",
                table: "AspNetUsers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastSelectedAccessRoleId",
                table: "AspNetUsers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastSelectedAccessScopeId",
                table: "AspNetUsers",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastSelectedAccessBundleId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LastSelectedAccessRoleId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LastSelectedAccessScopeId",
                table: "AspNetUsers");
        }
    }
}
