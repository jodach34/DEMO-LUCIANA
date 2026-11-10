using Convalidacion.Web.Models;
using Convalidacion.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Convalidacion.Web.Data;

public static class DemoSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        if (!await db.Asignaturas.AnyAsync())
        {
            db.Asignaturas.AddRange(
                new() { Id = 1, Codigo = "DEMO-101", Nombre = "Algoritmos y programación", Creditos = 4 },
                new() { Id = 2, Codigo = "DEMO-102", Nombre = "Base de datos", Creditos = 4 },
                new() { Id = 3, Codigo = "DEMO-103", Nombre = "Gestión de procesos", Creditos = 3 },
                new() { Id = 4, Codigo = "DEMO-104", Nombre = "Ingeniería de software", Creditos = 4 });
            await db.SaveChangesAsync();
        }
        // No reescribe ni reemplaza expedientes preexistentes, incluso los de la versión anterior.
        if (!await db.Solicitudes.AnyAsync())
        {
            var catalogo = await db.Asignaturas.OrderBy(a => a.Id).Take(2).ToListAsync();
            if (catalogo.Count < 2) { await transaction.CommitAsync(); return; }
            for (var i = 0; i < 6; i++)
            {
                var reloj = new RelojSemilla(new DateTimeOffset(2026, 11, 1 + i, 15, 0, 0, TimeSpan.Zero));
                var service = new SolicitudService(db, reloj, NullLogger<SolicitudService>.Instance);
                var id = await service.CrearAsync(new()
                {
                    Estudiante = $"Estudiante Demo {i + 1:D3}", InstitucionOrigen = "Institución Ficticia de Ejemplo", CertificadoDocumento = "CERT-DEMO-01",
                    Cursos = catalogo.Select((a, n) => new CursoSolicitudViewModel { AsignaturaId = a.Id, AsignaturaOrigen = $"Curso ficticio de origen {n + 1}",
                        CreditosOrigen = a.Creditos, Nota = 16, SilaboDocumento = "SIL-DEMO-01" }).ToList()
                }, PerfilDemo.Estudiante);
                async Task Aplicar(AccionSolicitud accion, PerfilDemo perfil)
                {
                    var s = await db.Solicitudes.SingleAsync(s => s.Id == id);
                    await service.EjecutarAsync(new() { Id = id, Revision = s.Revision, Accion = accion,
                        Comentario = $"Ejemplo ficticio: {accion.Texto()}.", DocumentosVerificados = true,
                        NumeroResolucion = $"DEMO-RD-{id:D4}", FechaResolucion = new DateOnly(2026, 11, 1 + i) }, perfil);
                }
                if (i == 0) continue;
                await Aplicar(AccionSolicitud.Recibir, PerfilDemo.Admision);
                if (i == 1)
                {
                    await Aplicar(AccionSolicitud.ObservarDocumentos, PerfilDemo.Admision);
                    await Aplicar(AccionSolicitud.DevolverEstudiante, PerfilDemo.Admision); continue;
                }
                await Aplicar(AccionSolicitud.DerivarDepartamento, PerfilDemo.Admision);
                if (i == 2) continue;
                foreach (var c in await db.Cursos.Where(c => c.SolicitudId == id).OrderBy(c => c.Id).ToListAsync())
                {
                    var s = await db.Solicitudes.SingleAsync(s => s.Id == id);
                    var aprobada = c.AsignaturaId == catalogo[0].Id;
                    await service.EvaluarAsync(new() { Id = id, Revision = s.Revision, AsignaturaSolicitudId = c.Id, Coincidencia = aprobada ? 85 : 60,
                        Resultado = aprobada ? ResultadoAcademico.Aprobada : ResultadoAcademico.Rechazada,
                        Justificacion = aprobada ? "Correspondencia académica ficticia verificada manualmente." : "Contenidos ficticios insuficientes para la convalidación." }, PerfilDemo.Director);
                }
                await Aplicar(AccionSolicitud.EmitirDictamen, PerfilDemo.Director);
                await Aplicar(AccionSolicitud.RemitirDecano, PerfilDemo.Director);
                if (i == 3) continue;
                await Aplicar(AccionSolicitud.AprobarDictamen, PerfilDemo.Decano);
                await Aplicar(AccionSolicitud.EmitirResolucion, PerfilDemo.Decano);
                await Aplicar(AccionSolicitud.Notificar, PerfilDemo.Decano);
                await Aplicar(AccionSolicitud.RemitirRegistro, PerfilDemo.Decano);
                if (i == 4) continue;
                await Aplicar(AccionSolicitud.RegistrarAsignaturas, PerfilDemo.RegistrosAcademicos);
                await Aplicar(AccionSolicitud.Finalizar, PerfilDemo.RegistrosAcademicos);
            }
        }
        await transaction.CommitAsync();
    }
    private sealed class RelojSemilla(DateTimeOffset fecha) : TimeProvider
    {
        private DateTimeOffset actual = fecha;
        public override DateTimeOffset GetUtcNow() { var valor = actual; actual = actual.AddMinutes(1); return valor; }
    }
}
