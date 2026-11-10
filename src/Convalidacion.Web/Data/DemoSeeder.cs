using Convalidacion.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Convalidacion.Web.Data;

public static class DemoSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        // Una sola transacción evita datos parciales. No reinicia solicitudes de los visitantes.
        await using var transaction = await db.Database.BeginTransactionAsync();
        if (!await db.Asignaturas.AnyAsync())
        {
            db.Asignaturas.AddRange(
                new() { Id = 1, Codigo = "DEMO-101", Nombre = "Algoritmos y programación", Creditos = 4 },
                new() { Id = 2, Codigo = "DEMO-102", Nombre = "Base de datos", Creditos = 4 },
                new() { Id = 3, Codigo = "DEMO-103", Nombre = "Gestión de procesos", Creditos = 3 },
                new() { Id = 4, Codigo = "DEMO-104", Nombre = "Ingeniería de software", Creditos = 4 });
            await db.SaveChangesAsync();
            var fecha = new DateTime(2026, 11, 1, 15, 0, 0, DateTimeKind.Utc);
            foreach (var estado in Enum.GetValues<EstadoSolicitud>())
            {
                var index = (int)estado;
                var creada = fecha.AddDays(index);
                var historial = new List<Seguimiento> { new() { Estado = EstadoSolicitud.Recibida,
                    Perfil = PerfilDemo.Estudiante, FechaUtc = creada, Comentario = "Registro de ejemplo con documentos simulados." } };
                if (estado is EstadoSolicitud.EnRevision or EstadoSolicitud.Aprobada or EstadoSolicitud.Rechazada)
                    historial.Add(new() { Estado = EstadoSolicitud.EnRevision, Perfil = PerfilDemo.Secretaria,
                        FechaUtc = creada.AddHours(1), Comentario = "Secretaría verificó la documentación simulada." });
                if (estado is EstadoSolicitud.Observada or EstadoSolicitud.Aprobada or EstadoSolicitud.Rechazada)
                    historial.Add(new() { Estado = estado, Perfil = estado == EstadoSolicitud.Observada ? PerfilDemo.Secretaria : PerfilDemo.Comision,
                        FechaUtc = creada.AddHours(2), Comentario = estado switch {
                            EstadoSolicitud.Observada => "Completar la descripción del sílabo simulado mediante una subsanación.",
                            EstadoSolicitud.Aprobada => "Equivalencia aceptada según los criterios didácticos.",
                            _ => "Coincidencia de contenidos insuficiente para la demo."
                        } });
                db.Solicitudes.Add(new() { Estudiante = $"Estudiante Demo {index + 1:D3}",
                    InstitucionOrigen = "Universidad Ficticia de Ejemplo", AsignaturaOrigen = "Curso de origen simulado",
                    AsignaturaId = index % 4 + 1, CreditosOrigen = 4, Nota = 16,
                    Coincidencia = estado == EstadoSolicitud.Rechazada ? 60 : 90, Estado = estado,
                    CreadaUtc = creada, ActualizadaUtc = historial.Last().FechaUtc,
                    Revision = new Guid(index + 1, 0, 0, new byte[8]), Historial = historial });
            }
            await db.SaveChangesAsync();
        }
        await transaction.CommitAsync();
    }
}
