using System.ComponentModel.DataAnnotations;

namespace Convalidacion.Web.Models;

public static class DocumentosDemo
{
    public static IReadOnlyDictionary<string, string> Certificados { get; } = new Dictionary<string, string>
    { ["CERT-DEMO-01"] = "Certificado ficticio de estudios", ["CERT-DEMO-CORREGIDO"] = "Certificado ficticio corregido" };
    public static IReadOnlyDictionary<string, string> Silabos { get; } = new Dictionary<string, string>
    { ["SIL-DEMO-01"] = "Sílabo ficticio de la asignatura", ["SIL-DEMO-02"] = "Sílabo ficticio con contenidos detallados", ["SIL-DEMO-CORREGIDO"] = "Sílabo ficticio corregido" };
}

public class CrearSolicitudViewModel
{
    [Required(ErrorMessage = "Ingresa un nombre ficticio."), StringLength(80), Display(Name = "Estudiante ficticio")]
    public string Estudiante { get; set; } = "Estudiante Demo";
    [Required, StringLength(120), Display(Name = "Institución de origen ficticia")]
    public string InstitucionOrigen { get; set; } = "Instituto Ejemplo";
    [Required(ErrorMessage = "Selecciona un certificado simulado."), Display(Name = "Certificado de estudios simulado")]
    public string CertificadoDocumento { get; set; } = "";
    [MinLength(1, ErrorMessage = "Agrega al menos una asignatura."), MaxLength(20, ErrorMessage = "Se permiten hasta 20 asignaturas por expediente.")]
    public List<CursoSolicitudViewModel> Cursos { get; set; } = [new()];
}

public class CursoSolicitudViewModel
{
    [Required(ErrorMessage = "Ingresa la asignatura de origen."), StringLength(120), Display(Name = "Asignatura de origen")]
    public string AsignaturaOrigen { get; set; } = "";
    [Range(1, int.MaxValue, ErrorMessage = "Selecciona una asignatura del catálogo ficticio."), Display(Name = "Asignatura USMP de demostración")]
    public int AsignaturaId { get; set; }
    [Range(1, 10), Display(Name = "Créditos de origen")]
    public int CreditosOrigen { get; set; } = 4;
    [Range(0, 20), Display(Name = "Nota obtenida (0–20)")]
    public int Nota { get; set; } = 15;
    [Required(ErrorMessage = "Selecciona un sílabo simulado."), Display(Name = "Sílabo simulado")]
    public string SilaboDocumento { get; set; } = "";
}

public class AccionSolicitudViewModel
{
    public int Id { get; set; }
    public Guid Revision { get; set; }
    [EnumDataType(typeof(AccionSolicitud))] public AccionSolicitud Accion { get; set; }
    [Required(ErrorMessage = "Registra el sustento o la observación."), StringLength(1000, MinimumLength = 5), Display(Name = "Sustento / comentario")]
    public string Comentario { get; set; } = "";
    public bool DocumentosVerificados { get; set; }
    [StringLength(60)] public string? NumeroResolucion { get; set; }
    [DataType(DataType.Date)] public DateOnly? FechaResolucion { get; set; }
}

public class EvaluarAsignaturaViewModel
{
    public int Id { get; set; }
    public Guid Revision { get; set; }
    public int AsignaturaSolicitudId { get; set; }
    [Range(0, 100)] public int Coincidencia { get; set; }
    [EnumDataType(typeof(ResultadoAcademico))] public ResultadoAcademico Resultado { get; set; }
    [Required, StringLength(1000, MinimumLength = 5)] public string Justificacion { get; set; } = "";
    public bool ExcepcionAfinidad { get; set; }
    [StringLength(1000)] public string? JustificacionExcepcion { get; set; }
}

public class SubsanarSolicitudViewModel
{
    public int Id { get; set; }
    public Guid Revision { get; set; }
    [Required, StringLength(1000, MinimumLength = 5)] public string Comentario { get; set; } = "";
    [Required] public string CertificadoDocumento { get; set; } = "";
    [MinLength(1), MaxLength(20)] public List<CorreccionSilaboViewModel> Cursos { get; set; } = [];
}
public class CorreccionSilaboViewModel
{
    public int AsignaturaSolicitudId { get; set; }
    [Required] public string SilaboDocumento { get; set; } = "";
}

public record DetalleViewModel(Solicitud Solicitud, PerfilDemo Perfil, AccionSolicitudViewModel Accion,
    EvaluarAsignaturaViewModel? Evaluacion = null, SubsanarSolicitudViewModel? Subsanacion = null);
public record BandejaViewModel(IReadOnlyList<Solicitud> Solicitudes, EstadoSolicitud? Estado, string? Buscar,
    bool Pendientes = false, bool ConDictamen = false, bool ConResolucion = false);
public record DashboardViewModel(int Total, int Pendientes, int Observadas, int Finalizadas, IReadOnlyList<Solicitud> Recientes, Solicitud? Activa);
public record ResumenSolicitudViewModel(Solicitud? Solicitud);
