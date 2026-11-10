using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Convalidacion.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class FlujoConvalidacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CargaConfirmadaUtc",
                table: "Solicitudes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CertificadoDocumento",
                table: "Solicitudes",
                type: "TEXT",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "DocumentosVerificadosUtc",
                table: "Solicitudes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VersionProceso",
                table: "Solicitudes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Accion",
                table: "Seguimientos",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EstadoAnterior",
                table: "Seguimientos",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PerfilHistorico",
                table: "Seguimientos",
                type: "TEXT",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Correcciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SolicitudId = table.Column<int>(type: "INTEGER", nullable: false),
                    AsignaturaSolicitudId = table.Column<int>(type: "INTEGER", nullable: true),
                    Anterior = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Corregido = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Justificacion = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Perfil = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Correcciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Correcciones_Solicitudes_SolicitudId",
                        column: x => x.SolicitudId,
                        principalTable: "Solicitudes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Cursos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SolicitudId = table.Column<int>(type: "INTEGER", nullable: false),
                    AsignaturaId = table.Column<int>(type: "INTEGER", nullable: false),
                    AsignaturaOrigen = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    CreditosOrigen = table.Column<int>(type: "INTEGER", nullable: false),
                    CreditosDestino = table.Column<int>(type: "INTEGER", nullable: false),
                    Nota = table.Column<int>(type: "INTEGER", nullable: false),
                    SilaboDocumento = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    VersionDocumento = table.Column<int>(type: "INTEGER", nullable: false),
                    Coincidencia = table.Column<int>(type: "INTEGER", nullable: true),
                    Resultado = table.Column<int>(type: "INTEGER", nullable: false),
                    Justificacion = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    ExcepcionAfinidad = table.Column<bool>(type: "INTEGER", nullable: false),
                    JustificacionExcepcion = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    EvaluadaUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cursos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cursos_Asignaturas_AsignaturaId",
                        column: x => x.AsignaturaId,
                        principalTable: "Asignaturas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cursos_Solicitudes_SolicitudId",
                        column: x => x.SolicitudId,
                        principalTable: "Solicitudes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Dictamenes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SolicitudId = table.Column<int>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    Numero = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    EmitidoUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Situacion = table.Column<int>(type: "INTEGER", nullable: false),
                    Justificacion = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    ObservacionDecanal = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    DetallesJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dictamenes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Dictamenes_Solicitudes_SolicitudId",
                        column: x => x.SolicitudId,
                        principalTable: "Solicitudes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notificaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SolicitudId = table.Column<int>(type: "INTEGER", nullable: false),
                    Destinatario = table.Column<int>(type: "INTEGER", nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Mensaje = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notificaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notificaciones_Solicitudes_SolicitudId",
                        column: x => x.SolicitudId,
                        principalTable: "Solicitudes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Evaluaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AsignaturaSolicitudId = table.Column<int>(type: "INTEGER", nullable: false),
                    VersionDocumento = table.Column<int>(type: "INTEGER", nullable: false),
                    Coincidencia = table.Column<int>(type: "INTEGER", nullable: false),
                    Resultado = table.Column<int>(type: "INTEGER", nullable: false),
                    Justificacion = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    ExcepcionAfinidad = table.Column<bool>(type: "INTEGER", nullable: false),
                    JustificacionExcepcion = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Perfil = table.Column<int>(type: "INTEGER", nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Evaluaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Evaluaciones_Cursos_AsignaturaSolicitudId",
                        column: x => x.AsignaturaSolicitudId,
                        principalTable: "Cursos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Resoluciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SolicitudId = table.Column<int>(type: "INTEGER", nullable: false),
                    DictamenAcademicoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Numero = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    FechaDocumento = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    EmitidaUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Justificacion = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    DetallesJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Resoluciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Resoluciones_Dictamenes_DictamenAcademicoId",
                        column: x => x.DictamenAcademicoId,
                        principalTable: "Dictamenes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Resoluciones_Solicitudes_SolicitudId",
                        column: x => x.SolicitudId,
                        principalTable: "Solicitudes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Registros",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AsignaturaSolicitudId = table.Column<int>(type: "INTEGER", nullable: false),
                    ResolucionDecanalId = table.Column<int>(type: "INTEGER", nullable: false),
                    Creditos = table.Column<int>(type: "INTEGER", nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Perfil = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Registros", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Registros_Cursos_AsignaturaSolicitudId",
                        column: x => x.AsignaturaSolicitudId,
                        principalTable: "Cursos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Registros_Resoluciones_ResolucionDecanalId",
                        column: x => x.ResolucionDecanalId,
                        principalTable: "Resoluciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Correcciones_SolicitudId",
                table: "Correcciones",
                column: "SolicitudId");

            migrationBuilder.CreateIndex(
                name: "IX_Cursos_AsignaturaId",
                table: "Cursos",
                column: "AsignaturaId");

            migrationBuilder.CreateIndex(
                name: "IX_Cursos_SolicitudId",
                table: "Cursos",
                column: "SolicitudId");

            migrationBuilder.CreateIndex(
                name: "IX_Dictamenes_SolicitudId_Version",
                table: "Dictamenes",
                columns: new[] { "SolicitudId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Evaluaciones_AsignaturaSolicitudId",
                table: "Evaluaciones",
                column: "AsignaturaSolicitudId");

            migrationBuilder.CreateIndex(
                name: "IX_Notificaciones_SolicitudId_Destinatario",
                table: "Notificaciones",
                columns: new[] { "SolicitudId", "Destinatario" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Registros_AsignaturaSolicitudId",
                table: "Registros",
                column: "AsignaturaSolicitudId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Registros_ResolucionDecanalId",
                table: "Registros",
                column: "ResolucionDecanalId");

            migrationBuilder.CreateIndex(
                name: "IX_Resoluciones_DictamenAcademicoId",
                table: "Resoluciones",
                column: "DictamenAcademicoId");

            migrationBuilder.CreateIndex(
                name: "IX_Resoluciones_SolicitudId",
                table: "Resoluciones",
                column: "SolicitudId",
                unique: true);

            // Conserva la autoría original, incluso cuando el nuevo enum reutiliza sus valores numéricos.
            // No modifica Estado, Perfil, fechas, comentarios ni identificadores históricos.
            migrationBuilder.Sql("""
                UPDATE Seguimientos SET PerfilHistorico = CASE Perfil
                    WHEN 0 THEN 'Estudiante' WHEN 1 THEN 'Secretaría' WHEN 2 THEN 'Comisión académica'
                    ELSE 'Perfil histórico ' || Perfil END;
                INSERT INTO Cursos
                    (SolicitudId, AsignaturaId, AsignaturaOrigen, CreditosOrigen, CreditosDestino, Nota,
                     SilaboDocumento, VersionDocumento, Coincidencia, Resultado, Justificacion,
                     ExcepcionAfinidad, JustificacionExcepcion, EvaluadaUtc)
                SELECT s.Id, s.AsignaturaId, s.AsignaturaOrigen, s.CreditosOrigen, a.Creditos, s.Nota,
                       '', 1, s.Coincidencia,
                       CASE s.Estado WHEN 3 THEN 1 WHEN 4 THEN 2 WHEN 2 THEN 3 ELSE 0 END,
                       'Dato conservado del expediente de la versión anterior.', 0, '', NULL
                FROM Solicitudes s INNER JOIN Asignaturas a ON a.Id = s.AsignaturaId
                WHERE s.VersionProceso = 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Correcciones");

            migrationBuilder.DropTable(
                name: "Evaluaciones");

            migrationBuilder.DropTable(
                name: "Notificaciones");

            migrationBuilder.DropTable(
                name: "Registros");

            migrationBuilder.DropTable(
                name: "Cursos");

            migrationBuilder.DropTable(
                name: "Resoluciones");

            migrationBuilder.DropTable(
                name: "Dictamenes");

            migrationBuilder.DropColumn(
                name: "CargaConfirmadaUtc",
                table: "Solicitudes");

            migrationBuilder.DropColumn(
                name: "CertificadoDocumento",
                table: "Solicitudes");

            migrationBuilder.DropColumn(
                name: "DocumentosVerificadosUtc",
                table: "Solicitudes");

            migrationBuilder.DropColumn(
                name: "VersionProceso",
                table: "Solicitudes");

            migrationBuilder.DropColumn(
                name: "Accion",
                table: "Seguimientos");

            migrationBuilder.DropColumn(
                name: "EstadoAnterior",
                table: "Seguimientos");

            migrationBuilder.DropColumn(
                name: "PerfilHistorico",
                table: "Seguimientos");
        }
    }
}
