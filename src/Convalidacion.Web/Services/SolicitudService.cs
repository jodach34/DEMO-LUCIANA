using Convalidacion.Web.Data;
using Convalidacion.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Convalidacion.Web.Services;

public class ReglaProcesoException(string message) : Exception(message);

public class SolicitudService(AppDbContext db, TimeProvider clock, ILogger<SolicitudService> logger)
{
    public static AccionSolicitud[] Acciones(EstadoSolicitud estado, PerfilDemo perfil) => (estado, perfil) switch
    {
        (EstadoSolicitud.Recibida, PerfilDemo.Secretaria) => [AccionSolicitud.Revisar, AccionSolicitud.Observar],
        (EstadoSolicitud.EnRevision, PerfilDemo.Comision) => [AccionSolicitud.Aprobar, AccionSolicitud.Observar, AccionSolicitud.Rechazar],
        (EstadoSolicitud.Observada, PerfilDemo.Estudiante) => [AccionSolicitud.Subsanar],
        _ => []
    };

    // Umbrales didácticos; no son un reglamento de la USMP.
    public static bool CumpleCriterios(Solicitud solicitud) => solicitud.Nota >= 11
        && solicitud.Coincidencia >= 80 && solicitud.CreditosOrigen >= solicitud.Asignatura.Creditos;

    public async Task<int> CrearAsync(CrearSolicitudViewModel model, PerfilDemo perfil, CancellationToken ct = default)
    {
        if (perfil != PerfilDemo.Estudiante)
            throw new ReglaProcesoException("Cambia al perfil Estudiante para registrar una solicitud.");
        var validation = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        if (!System.ComponentModel.DataAnnotations.Validator.TryValidateObject(model, new(model), validation, true))
            throw new ReglaProcesoException("Revisa los campos y los documentos simulados de la solicitud.");
        if (!await db.Asignaturas.AnyAsync(a => a.Id == model.AsignaturaId, ct))
            throw new ReglaProcesoException("La asignatura seleccionada no existe.");
        var now = clock.GetUtcNow().UtcDateTime;
        var solicitud = new Solicitud
        {
            AmbitoDemo = db.AmbitoActual,
            Estudiante = model.Estudiante.Trim(), InstitucionOrigen = model.InstitucionOrigen.Trim(),
            AsignaturaOrigen = model.AsignaturaOrigen.Trim(), AsignaturaId = model.AsignaturaId,
            CreditosOrigen = model.CreditosOrigen, Nota = model.Nota, Coincidencia = model.Coincidencia,
            CreadaUtc = now, ActualizadaUtc = now,
            Historial = [new() { FechaUtc = now, Perfil = perfil, Estado = EstadoSolicitud.Recibida,
                Comentario = "Solicitud registrada con certificado y sílabo simulados." }]
        };
        db.Solicitudes.Add(solicitud);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Solicitud {SolicitudId} registrada", solicitud.Id);
        return solicitud.Id;
    }

    public async Task EjecutarAsync(AccionSolicitudViewModel model, PerfilDemo perfil, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(model.Comentario) || model.Comentario.Trim().Length < 5 || model.Comentario.Length > 1000)
            throw new ReglaProcesoException("El sustento debe tener entre 5 y 1000 caracteres.");
        var solicitud = await db.Solicitudes.Include(s => s.Asignatura).SingleOrDefaultAsync(s => s.Id == model.Id, ct)
            ?? throw new ReglaProcesoException("La solicitud ya no está disponible.");
        if (solicitud.Revision != model.Revision)
            throw new ReglaProcesoException("Otra operación actualizó esta solicitud. Recarga la página y vuelve a evaluarla.");
        if (!Acciones(solicitud.Estado, perfil).Contains(model.Accion))
            throw new ReglaProcesoException("Esta acción no está disponible para el estado y perfil actuales.");
        if (model.Accion == AccionSolicitud.Aprobar && !CumpleCriterios(solicitud))
            throw new ReglaProcesoException("No cumple los criterios de la demo: nota ≥ 11, coincidencia ≥ 80% y créditos suficientes.");
        solicitud.Estado = model.Accion switch
        {
            AccionSolicitud.Revisar => EstadoSolicitud.EnRevision,
            AccionSolicitud.Observar => EstadoSolicitud.Observada,
            AccionSolicitud.Subsanar => EstadoSolicitud.Recibida,
            AccionSolicitud.Aprobar => EstadoSolicitud.Aprobada,
            _ => EstadoSolicitud.Rechazada
        };
        solicitud.ActualizadaUtc = clock.GetUtcNow().UtcDateTime;
        solicitud.Revision = Guid.NewGuid();
        db.Seguimientos.Add(new() { SolicitudId = solicitud.Id, Estado = solicitud.Estado, Perfil = perfil,
            Comentario = model.Comentario.Trim(), FechaUtc = solicitud.ActualizadaUtc });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Solicitud {SolicitudId}: {Estado} por perfil demo {Perfil}", solicitud.Id, solicitud.Estado, perfil);
    }
}
