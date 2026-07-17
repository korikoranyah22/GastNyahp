using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GastNyahp.Infrastructure.Migrations.Projections
{
    /// <inheritdoc />
    public partial class AddInstanceOwnerAndInviteMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsInstanceOwner",
                table: "families",
                type: "boolean",
                nullable: false,
                defaultValue: true);   // filas existentes: familias/códigos del operador = "del dueño"

            migrationBuilder.AddColumn<string>(
                name: "ExpiresAt",
                table: "admin_invites",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "GrantsOwner",
                table: "admin_invites",
                type: "boolean",
                nullable: false,
                defaultValue: true);   // filas existentes: familias/códigos del operador = "del dueño"
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsInstanceOwner",
                table: "families");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "admin_invites");

            migrationBuilder.DropColumn(
                name: "GrantsOwner",
                table: "admin_invites");
        }
    }
}
