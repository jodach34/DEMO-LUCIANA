using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Convalidacion.Web.Models;

// Los valores 0–4 se conservan para leer los expedientes de la versión anterior.
public enum EstadoSolicitud
{
    Recibida = 0, EnRevision = 1, Observada = 2, Aprobada = 3, Rechazada = 4,
    Presentada = 10, VerificacionDocumental = 11, PendienteSubsanacion = 12, Subsanada = 13,
    EvaluacionAcademica = 14, DictamenEmitido = 15, RevisionDecanal = 16, ResolucionEmitida = 17,
    NotificacionRealizada = 18, RegistroAcademicoPendiente = 19, Finalizada = 20, DictamenAprobado = 21
}
public enum PerfilDemo { Estudiante, Admision, Director, Decano, RegistrosAcademicos }
public enum ResultadoAcademico { Pendiente, Aprobada, Rechazada, Observada }
public enum SituacionDictamen { Emitido, EnRevision, Devuelto, Aprobado }
public enum AccionSolicitud
{
    Presentar = 9,
    Recibir = 10, ObservarDocumentos, DevolverEstudiante, Subsanar, VerificarCorreccion, DerivarDepartamento,
    EvaluarAsignatura, SolicitarCorreccionAcademica, EmitirDictamen, RemitirDecano, DevolverDictamen,
    AprobarDictamen, EmitirResolucion, Notificar, RemitirRegistro, RegistrarAsignaturas, Finalizar
}

public class Solicitud
{
    public int Id { get; set; }
    [MaxLength(32)] public string AmbitoDemo { get; set; } = "";
    [MaxLength(80)] public string Estudiante { get; set; } = "";
    [MaxLength(120)] public string InstitucionOrigen { get; set; } = "";
    // Columnas originales conservadas por compatibilidad; para el proceso nuevo se usa Cursos.
    [MaxLength(120)] public string AsignaturaOrigen { get; set; } = "";
    public int CreditosOrigen { get; set; }
    public int Nota { get; set; }
    public int Coincidencia { get; set; }
    public int AsignaturaId { get; set; }
    public Asignatura Asignatura { get; set; } = null!;
    public EstadoSolicitud Estado { get; set; } = EstadoSolicitud.Presentada;
    public int VersionProceso { get; set; } = 2;
    [MaxLength(60)] public string CertificadoDocumento { get; set; } = "";
    public DateTime? DocumentosVerificadosUtc { get; set; }
    public DateTime? CargaConfirmadaUtc { get; set; }
    public DateTime CreadaUtc { get; set; }
    public DateTime ActualizadaUtc { get; set; }
    [ConcurrencyCheck] public Guid Revision { get; set; } = Guid.NewGuid();
    public List<AsignaturaSolicitud> Cursos { get; set; } = [];
    public List<Seguimiento> Historial { get; set; } = [];
    public List<CorreccionDocumento> Correcciones { get; set; } = [];
    public List<DictamenAcademico> Dictamenes { get; set; } = [];
    public ResolucionDecanal? Resolucion { get; set; }
    public List<NotificacionExpediente> Notificaciones { get; set; } = [];
    public string Codigo => $"CON-{CreadaUtc.Year}-{Id:D4}";
    public int Etapa => Estado switch
    {
        EstadoSolicitud.EvaluacionAcademica or EstadoSolicitud.DictamenEmitido => 2,
        EstadoSolicitud.RevisionDecanal or EstadoSolicitud.DictamenAprobado or EstadoSolicitud.ResolucionEmitida => 3,
        EstadoSolicitud.NotificacionRealizada or EstadoSolicitud.RegistroAcademicoPendiente or EstadoSolicitud.Finalizada => 4,
        _ => 1
    };
}

public class Asignatura
{
    public int Id { get; set; }
    [MaxLength(20)] public string Codigo { get; set; } = "";
    [MaxLength(120)] public string Nombre { get; set; } = "";
    public int Creditos { get; set; }
}

public class AsignaturaSolicitud
{
    public int Id { get; set; }
    public int SolicitudId { get; set; }
    public Solicitud Solicitud { get; set; } = null!;
    public int AsignaturaId { get; set; }
    public Asignatura Asignatura { get; set; } = null!;
    [MaxLength(120)] public string AsignaturaOrigen { get; set; } = "";
    public int CreditosOrigen { get; set; }
    public int CreditosDestino { get; set; }
    public int Nota { get; set; }
    [MaxLength(60)] public string SilaboDocumento { get; set; } = "";
    public int VersionDocumento { get; set; } = 1;
    public int? Coincidencia { get; set; }
    public ResultadoAcademico Resultado { get; set; }
    [MaxLength(1000)] public string Justificacion { get; set; } = "";
    public bool ExcepcionAfinidad { get; set; }
    [MaxLength(1000)] public string JustificacionExcepcion { get; set; } = "";
    public DateTime? EvaluadaUtc { get; set; }
    public List<EvaluacionAsignatura> Evaluaciones { get; set; } = [];
    public RegistroAcademico? Registro { get; set; }
}

public class EvaluacionAsignatura
{
    public int Id { get; set; }
    public int AsignaturaSolicitudId { get; set; }
    public AsignaturaSolicitud AsignaturaSolicitud { get; set; } = null!;
    public int VersionDocumento { get; set; }
    public int Coincidencia { get; set; }
    public ResultadoAcademico Resultado { get; set; }
    [MaxLength(1000)] public string Justificacion { get; set; } = "";
    public bool ExcepcionAfinidad { get; set; }
    [MaxLength(1000)] public string JustificacionExcepcion { get; set; } = "";
    public PerfilDemo Perfil { get; set; } = PerfilDemo.Director;
    public DateTime FechaUtc { get; set; }
}

public class Seguimiento
{
    public int Id { get; set; }
    public int SolicitudId { get; set; }
    public Solicitud Solicitud { get; set; } = null!;
    public EstadoSolicitud? EstadoAnterior { get; set; }
    public EstadoSolicitud Estado { get; set; }
    public AccionSolicitud? Accion { get; set; }
    public PerfilDemo Perfil { get; set; }
    // La migración copia aquí los nombres antiguos sin reinterpretar la autoría histórica.
    [MaxLength(80)] public string? PerfilHistorico { get; set; }
    public DateTime FechaUtc { get; set; }
    [MaxLength(1000)] public string Comentario { get; set; } = "";
    public string Responsable => PerfilHistorico ?? Perfil.Texto();
}

public class CorreccionDocumento
{
    public int Id { get; set; }
    public int SolicitudId { get; set; }
    public Solicitud Solicitud { get; set; } = null!;
    public int? AsignaturaSolicitudId { get; set; }
    [MaxLength(60)] public string Anterior { get; set; } = "";
    [MaxLength(60)] public string Corregido { get; set; } = "";
    [MaxLength(1000)] public string Justificacion { get; set; } = "";
    public DateTime FechaUtc { get; set; }
    public PerfilDemo Perfil { get; set; } = PerfilDemo.Estudiante;
}

public record ResultadoDictamen(int AsignaturaSolicitudId, string Origen, string Destino, string CodigoDestino,
    int CreditosOrigen, int CreditosDestino, int Nota, int Coincidencia, ResultadoAcademico Resultado,
    string Justificacion, bool ExcepcionAfinidad, string JustificacionExcepcion, DateTime EvaluadaUtc);

public class DictamenAcademico
{
    public int Id { get; set; }
    public int SolicitudId { get; set; }
    public Solicitud Solicitud { get; set; } = null!;
    public int Version { get; set; }
    [MaxLength(60)] public string Numero { get; set; } = "";
    public DateTime EmitidoUtc { get; set; }
    public SituacionDictamen Situacion { get; set; }
    [MaxLength(1000)] public string Justificacion { get; set; } = "";
    [MaxLength(1000)] public string ObservacionDecanal { get; set; } = "";
    public string DetallesJson { get; set; } = "[]";
    [NotMapped] public List<ResultadoDictamen> Detalles => JsonSerializer.Deserialize<List<ResultadoDictamen>>(DetallesJson) ?? [];
}

public class ResolucionDecanal
{
    public int Id { get; set; }
    public int SolicitudId { get; set; }
    public Solicitud Solicitud { get; set; } = null!;
    public int DictamenAcademicoId { get; set; }
    public DictamenAcademico DictamenAcademico { get; set; } = null!;
    [MaxLength(60)] public string Numero { get; set; } = "";
    public DateOnly FechaDocumento { get; set; }
    public DateTime EmitidaUtc { get; set; }
    [MaxLength(1000)] public string Justificacion { get; set; } = "";
    public string DetallesJson { get; set; } = "[]";
    [NotMapped] public List<ResultadoDictamen> Detalles => JsonSerializer.Deserialize<List<ResultadoDictamen>>(DetallesJson) ?? [];
    [NotMapped] public string Decision => Detalles.All(d => d.Resultado == ResultadoAcademico.Aprobada) ? "Convalidación total" : Detalles.Any(d => d.Resultado == ResultadoAcademico.Aprobada) ? "Convalidación parcial" : "Sin asignaturas aprobadas";
}

public class NotificacionExpediente
{
    public int Id { get; set; }
    public int SolicitudId { get; set; }
    public Solicitud Solicitud { get; set; } = null!;
    public PerfilDemo Destinatario { get; set; }
    public DateTime FechaUtc { get; set; }
    [MaxLength(300)] public string Mensaje { get; set; } = "";
}

public class RegistroAcademico
{
    public int Id { get; set; }
    public int AsignaturaSolicitudId { get; set; }
    public AsignaturaSolicitud AsignaturaSolicitud { get; set; } = null!;
    public int ResolucionDecanalId { get; set; }
    public ResolucionDecanal ResolucionDecanal { get; set; } = null!;
    public int Creditos { get; set; }
    public DateTime FechaUtc { get; set; }
    public PerfilDemo Perfil { get; set; } = PerfilDemo.RegistrosAcademicos;
}

public static class Etiquetas
{
    public static string Texto(this EstadoSolicitud estado) => estado switch
    {
        EstadoSolicitud.Recibida => "Recibida (versión anterior)", EstadoSolicitud.EnRevision => "En revisión (versión anterior)",
        EstadoSolicitud.Aprobada => "Aprobada (versión anterior)", EstadoSolicitud.Rechazada => "Rechazada (versión anterior)",
        EstadoSolicitud.Presentada => "Solicitud presentada", EstadoSolicitud.VerificacionDocumental => "Verificación documental",
        EstadoSolicitud.PendienteSubsanacion => "Pendiente de subsanación", EstadoSolicitud.EvaluacionAcademica => "Evaluación académica",
        EstadoSolicitud.DictamenEmitido => "Dictamen emitido", EstadoSolicitud.RevisionDecanal => "Revisión decanal",
        EstadoSolicitud.DictamenAprobado => "Dictamen aprobado", EstadoSolicitud.ResolucionEmitida => "Resolución emitida",
        EstadoSolicitud.NotificacionRealizada => "Notificación realizada", EstadoSolicitud.RegistroAcademicoPendiente => "Registro académico pendiente",
        _ => estado.ToString()
    };
    public static string Texto(this PerfilDemo perfil) => perfil switch
    {
        PerfilDemo.Admision => "Oficina de Admisión", PerfilDemo.Director => "Director del Departamento Académico",
        PerfilDemo.Decano => "Decano", PerfilDemo.RegistrosAcademicos => "Registros Académicos", _ => "Estudiante"
    };
    public static string Texto(this AccionSolicitud accion) => accion switch
    {
        AccionSolicitud.Presentar => "Presentar solicitud",
        AccionSolicitud.Recibir => "Recibir e iniciar verificación", AccionSolicitud.ObservarDocumentos => "Registrar observación documental",
        AccionSolicitud.DevolverEstudiante => "Devolver al estudiante para subsanar", AccionSolicitud.Subsanar => "Enviar documentos corregidos",
        AccionSolicitud.VerificarCorreccion => "Verificar documentos corregidos", AccionSolicitud.DerivarDepartamento => "Derivar al Departamento Académico",
        AccionSolicitud.EvaluarAsignatura => "Evaluar asignatura", AccionSolicitud.SolicitarCorreccionAcademica => "Solicitar corrección de sílabos a través de Admisión",
        AccionSolicitud.EmitirDictamen => "Emitir dictamen académico", AccionSolicitud.RemitirDecano => "Remitir dictamen al Decano",
        AccionSolicitud.DevolverDictamen => "Devolver dictamen con observaciones", AccionSolicitud.AprobarDictamen => "Aprobar dictamen",
        AccionSolicitud.EmitirResolucion => "Emitir Resolución Decanal simulada", AccionSolicitud.Notificar => "Notificar al estudiante y a Registros Académicos",
        AccionSolicitud.RemitirRegistro => "Remitir a Registros Académicos", AccionSolicitud.RegistrarAsignaturas => "Confirmar carga académica simulada",
        AccionSolicitud.Finalizar => "Finalizar expediente", _ => accion.ToString()
    };
    public static string HoraPeru(this DateTime fecha) => DateTime.SpecifyKind(fecha, DateTimeKind.Utc).AddHours(-5).ToString("dd/MM/yyyy HH:mm") + " (Perú, UTC−5)";
}
