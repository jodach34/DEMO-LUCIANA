using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Convalidacion.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class ComplementosNotificaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notificaciones_SolicitudId_Destinatario",
                table: "Notificaciones");

            migrationBuilder.AddColumn<Guid>(
                name: "EventoId",
                table: "Notificaciones",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "LeidaUtc",
                table: "Notificaciones",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Tipo",
                table: "Notificaciones",
                type: "INTEGER",
                nullable: false,
                defaultValue: (int)Convalidacion.Web.Models.AccionSolicitud.Notificar);

            migrationBuilder.CreateIndex(
                name: "IX_Notificaciones_SolicitudId_Destinatario_EventoId",
                table: "Notificaciones",
                columns: new[] { "SolicitudId", "Destinatario", "EventoId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notificaciones_SolicitudId_Destinatario_EventoId",
                table: "Notificaciones");

            migrationBuilder.DropColumn(
                name: "EventoId",
                table: "Notificaciones");

            migrationBuilder.DropColumn(
                name: "LeidaUtc",
                table: "Notificaciones");

            migrationBuilder.DropColumn(
                name: "Tipo",
                table: "Notificaciones");

            migrationBuilder.CreateIndex(
                name: "IX_Notificaciones_SolicitudId_Destinatario",
                table: "Notificaciones",
                columns: new[] { "SolicitudId", "Destinatario" },
                unique: true);
        }
    }
}
