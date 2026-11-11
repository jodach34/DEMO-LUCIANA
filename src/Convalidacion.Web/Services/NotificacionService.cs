using Convalidacion.Web.Data;
using Convalidacion.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Convalidacion.Web.Services;

public class NotificacionService(AppDbContext db, PerfilDemoService perfiles, TimeProvider clock)
{
    public IQueryable<NotificacionExpediente> DelPerfil => db.Notificaciones.Where(n => n.Destinatario == perfiles.Actual);

    public async Task<bool> MarcarLeidaAsync(int id, CancellationToken ct = default)
    {
        // El filtro global y el destinatario se aplican también a la escritura.
        var aviso = await DelPerfil.SingleOrDefaultAsync(n => n.Id == id, ct);
        if (aviso is null) return false;
        if (aviso.LeidaUtc is null)
        {
            aviso.LeidaUtc = clock.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(ct);
        }
        return true;
    }

    public static void Agregar(Solicitud s, AccionSolicitud accion, DateTime fecha)
    {
        PerfilDemo[] destinatarios = accion switch
        {
            AccionSolicitud.Presentar => [PerfilDemo.Estudiante, PerfilDemo.Admision],
            AccionSolicitud.Recibir or AccionSolicitud.ObservarDocumentos or AccionSolicitud.DevolverEstudiante => [PerfilDemo.Estudiante],
            AccionSolicitud.Subsanar => [PerfilDemo.Admision],
            AccionSolicitud.VerificarCorreccion => [PerfilDemo.Estudiante],
            AccionSolicitud.DerivarDepartamento => [PerfilDemo.Director, PerfilDemo.Estudiante],
            AccionSolicitud.SolicitarCorreccionAcademica => [PerfilDemo.Admision, PerfilDemo.Estudiante],
            AccionSolicitud.EvaluarAsignatura when s.Cursos.Any(c => c.Resultado == ResultadoAcademico.Observada) => [PerfilDemo.Director],
            AccionSolicitud.EmitirDictamen => [PerfilDemo.Director, PerfilDemo.Estudiante],
            AccionSolicitud.RemitirDecano => [PerfilDemo.Decano],
            AccionSolicitud.DevolverDictamen => [PerfilDemo.Director],
            AccionSolicitud.AprobarDictamen or AccionSolicitud.EmitirResolucion => [PerfilDemo.Estudiante],
            AccionSolicitud.Notificar => [PerfilDemo.Estudiante, PerfilDemo.RegistrosAcademicos],
            AccionSolicitud.RemitirRegistro => [PerfilDemo.RegistrosAcademicos],
            AccionSolicitud.RegistrarAsignaturas or AccionSolicitud.Finalizar => [PerfilDemo.Estudiante],
            _ => []
        };
        var evento = Guid.NewGuid();
        var mensaje = accion == AccionSolicitud.Notificar
            ? $"Notificación decanal: resolución simulada {s.Resolucion!.Numero} disponible."
            : $"{accion.Texto()}: {s.Estado.Texto()}.";
        foreach (var destinatario in destinatarios)
            s.Notificaciones.Add(new() { EventoId = evento, Tipo = accion, Destinatario = destinatario, FechaUtc = fecha, Mensaje = mensaje });
    }
}
