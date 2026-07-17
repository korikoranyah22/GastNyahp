using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GastNyahp.Infrastructure.Migrations.Projections
{
    /// <inheritdoc />
    public partial class AddMemberAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "family_members",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "family_members",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "member_sessions",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DeviceName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Revoked = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_member_sessions", x => x.SessionId);
                });

            migrationBuilder.CreateTable(
                name: "password_resets",
                columns: table => new
                {
                    ResetId = table.Column<Guid>(type: "uuid", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Redeemed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_password_resets", x => x.ResetId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_family_members_FamilyId_Email",
                table: "family_members",
                columns: new[] { "FamilyId", "Email" },
                unique: true,
                filter: "\"Email\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_member_sessions_MemberId",
                table: "member_sessions",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_member_sessions_TokenHash",
                table: "member_sessions",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_password_resets_CodeHash",
                table: "password_resets",
                column: "CodeHash");

            migrationBuilder.CreateIndex(
                name: "IX_password_resets_MemberId",
                table: "password_resets",
                column: "MemberId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "member_sessions");

            migrationBuilder.DropTable(
                name: "password_resets");

            migrationBuilder.DropIndex(
                name: "IX_family_members_FamilyId_Email",
                table: "family_members");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "family_members");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "family_members");
        }
    }
}
