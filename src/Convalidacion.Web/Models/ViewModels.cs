using System.ComponentModel.DataAnnotations;

namespace Convalidacion.Web.Models;

public class CrearSolicitudViewModel : IValidatableObject
{
    [Required(ErrorMessage = "Ingresa un nombre ficticio."), StringLength(80), Display(Name = "Estudiante ficticio")]
    public string Estudiante { get; set; } = "Estudiante Demo";
    [Required, StringLength(120), Display(Name = "Institución de origen ficticia")]
    public string InstitucionOrigen { get; set; } = "Instituto Ejemplo";
    [Required(ErrorMessage = "Ingresa la asignatura de origen."), StringLength(120), Display(Name = "Asignatura de origen")]
    public string AsignaturaOrigen { get; set; } = "";
    [Range(1, 10), Display(Name = "Créditos de origen")]
    public int CreditosOrigen { get; set; } = 4;
    [Range(0, 20), Display(Name = "Nota obtenida (0–20)")]
    public int Nota { get; set; } = 15;
    [Range(0, 100), Display(Name = "Coincidencia de contenidos simulada (%)")]
    public int Coincidencia { get; set; } = 85;
    [Range(1, int.MaxValue, ErrorMessage = "Selecciona una asignatura de destino."), Display(Name = "Asignatura de destino FIA–EPICS")]
    public int AsignaturaId { get; set; }
    [Display(Name = "Certificado de estudios simulado disponible")]
    public bool Certificado { get; set; }
    [Display(Name = "Sílabo simulado disponible")]
    public bool Silabo { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Certificado || !Silabo)
            yield return new ValidationResult("Confirma los dos documentos simulados antes de enviar.", [nameof(Certificado), nameof(Silabo)]);
    }
}

public class AccionSolicitudViewModel
{
    public int Id { get; set; }
    public Guid Revision { get; set; }
    [EnumDataType(typeof(AccionSolicitud))] public AccionSolicitud Accion { get; set; }
    [Required(ErrorMessage = "Registra el sustento o la subsanación."), StringLength(1000, MinimumLength = 5), Display(Name = "Sustento / comentario")]
    public string Comentario { get; set; } = "";
}

public record DetalleViewModel(Solicitud Solicitud, PerfilDemo Perfil, AccionSolicitudViewModel Accion);
public record BandejaViewModel(IReadOnlyList<Solicitud> Solicitudes, EstadoSolicitud? Estado, string? Buscar);
public record DashboardViewModel(int Total, int Pendientes, int Observadas, int Aprobadas, IReadOnlyList<Solicitud> Recientes);
