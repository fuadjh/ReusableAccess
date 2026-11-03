using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReusableAccess.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteAdminIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "AspNetUsers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "AspNetUsers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OrganizationalCode",
                table: "AspNetUsers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SessionVersion",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AccountChallenges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    ProtectedCode = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    UserSessionVersion = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    Consumed = table.Column<bool>(type: "bit", nullable: false),
                    RememberMe = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountChallenges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuthenticationPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PublicRegistrationEnabled = table.Column<bool>(type: "bit", nullable: false),
                    RegistrationStartsUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RegistrationEndsUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LoginMode = table.Column<int>(type: "int", nullable: false),
                    RecoveryEnabled = table.Column<bool>(type: "bit", nullable: false),
                    RecoveryChannel = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthenticationPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SecurityAuditEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OccurredUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Entity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ChangedFields = table.Column<string>(type: "nvarchar(1500)", maxLength: 1500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityAuditEntries", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "AuthenticationPolicies",
                columns: new[] { "Id", "LoginMode", "PublicRegistrationEnabled", "RecoveryChannel", "RecoveryEnabled", "RegistrationEndsUtc", "RegistrationStartsUtc", "Version" },
                values: new object[] { 1, 0, false, 0, false, null, null, 0 });

            migrationBuilder.CreateIndex(
                name: "IX_AccountChallenges_UserId_Purpose_CreatedUtc",
                table: "AccountChallenges",
                columns: new[] { "UserId", "Purpose", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAuditEntries_OccurredUtc",
                table: "SecurityAuditEntries",
                column: "OccurredUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountChallenges");

            migrationBuilder.DropTable(
                name: "AuthenticationPolicies");

            migrationBuilder.DropTable(
                name: "SecurityAuditEntries");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "OrganizationalCode",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "SessionVersion",
                table: "AspNetUsers");
        }
    }
}
