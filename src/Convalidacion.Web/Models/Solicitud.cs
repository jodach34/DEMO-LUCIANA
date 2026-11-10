using System.ComponentModel.DataAnnotations;

namespace Convalidacion.Web.Models;

public enum EstadoSolicitud { Recibida, EnRevision, Observada, Aprobada, Rechazada }
public enum PerfilDemo { Estudiante, Secretaria, Comision }
public enum AccionSolicitud { Revisar, Observar, Subsanar, Aprobar, Rechazar }

public class Solicitud
{
    public int Id { get; set; }
    [MaxLength(80)] public string Estudiante { get; set; } = "";
    [MaxLength(120)] public string InstitucionOrigen { get; set; } = "";
    [MaxLength(120)] public string AsignaturaOrigen { get; set; } = "";
    public int CreditosOrigen { get; set; }
    public int Nota { get; set; }
    public int Coincidencia { get; set; }
    public int AsignaturaId { get; set; }
    public Asignatura Asignatura { get; set; } = null!;
    public EstadoSolicitud Estado { get; set; } = EstadoSolicitud.Recibida;
    public DateTime CreadaUtc { get; set; }
    public DateTime ActualizadaUtc { get; set; }
    [ConcurrencyCheck] public Guid Revision { get; set; } = Guid.NewGuid();
    public List<Seguimiento> Historial { get; set; } = [];
    public string Codigo => $"CON-{CreadaUtc.Year}-{Id:D4}";
}

public class Asignatura
{
    public int Id { get; set; }
    [MaxLength(20)] public string Codigo { get; set; } = "";
    [MaxLength(120)] public string Nombre { get; set; } = "";
    public int Creditos { get; set; }
}

public class Seguimiento
{
    public int Id { get; set; }
    public int SolicitudId { get; set; }
    public EstadoSolicitud Estado { get; set; }
    public PerfilDemo Perfil { get; set; }
    public DateTime FechaUtc { get; set; }
    [MaxLength(1000)] public string Comentario { get; set; } = "";
}

public static class Etiquetas
{
    public static string Texto(this EstadoSolicitud estado) => estado switch
    {
        EstadoSolicitud.EnRevision => "En revisión", _ => estado.ToString()
    };
    public static string Texto(this PerfilDemo perfil) => perfil switch
    {
        PerfilDemo.Secretaria => "Secretaría", PerfilDemo.Comision => "Comisión académica", _ => "Estudiante"
    };
    public static string Texto(this AccionSolicitud accion) => accion switch
    {
        AccionSolicitud.Revisar => "Iniciar revisión", AccionSolicitud.Observar => "Registrar observación",
        AccionSolicitud.Subsanar => "Enviar subsanación", AccionSolicitud.Aprobar => "Aprobar convalidación",
        _ => "Rechazar convalidación"
    };
}
