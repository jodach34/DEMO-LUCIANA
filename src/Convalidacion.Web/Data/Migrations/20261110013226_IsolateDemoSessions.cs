using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Convalidacion.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class IsolateDemoSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AmbitoDemo",
                table: "Solicitudes",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Solicitudes_AmbitoDemo",
                table: "Solicitudes",
                column: "AmbitoDemo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Solicitudes_AmbitoDemo",
                table: "Solicitudes");

            migrationBuilder.DropColumn(
                name: "AmbitoDemo",
                table: "Solicitudes");
        }
    }
}
