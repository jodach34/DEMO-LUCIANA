using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Convalidacion.Web.Data;
using Convalidacion.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Convalidacion.Web.Services;

public class ReglaProcesoException(string message) : Exception(message);

public class SolicitudService(AppDbContext db, TimeProvider clock, ILogger<SolicitudService> logger)
{
    public static EstadoSolicitud[] EstadosResponsables(PerfilDemo perfil) => perfil switch
    {
        PerfilDemo.Estudiante => [EstadoSolicitud.PendienteSubsanacion],
        PerfilDemo.Admision => [EstadoSolicitud.Presentada, EstadoSolicitud.VerificacionDocumental, EstadoSolicitud.Observada, EstadoSolicitud.Subsanada],
        PerfilDemo.Director => [EstadoSolicitud.EvaluacionAcademica, EstadoSolicitud.DictamenEmitido],
        PerfilDemo.Decano => [EstadoSolicitud.RevisionDecanal, EstadoSolicitud.DictamenAprobado, EstadoSolicitud.ResolucionEmitida, EstadoSolicitud.NotificacionRealizada],
        PerfilDemo.RegistrosAcademicos => [EstadoSolicitud.RegistroAcademicoPendiente], _ => []
    };

    public static AccionSolicitud[] Acciones(Solicitud s, PerfilDemo perfil)
    {
        if (s.VersionProceso != 2) return [];
        return (s.Estado, perfil) switch
        {
            (EstadoSolicitud.Presentada, PerfilDemo.Admision) => [AccionSolicitud.Recibir],
            (EstadoSolicitud.VerificacionDocumental, PerfilDemo.Admision) => [AccionSolicitud.ObservarDocumentos, AccionSolicitud.DerivarDepartamento],
            (EstadoSolicitud.Observada, PerfilDemo.Admision) => [AccionSolicitud.DevolverEstudiante],
            (EstadoSolicitud.PendienteSubsanacion, PerfilDemo.Estudiante) => [AccionSolicitud.Subsanar],
            (EstadoSolicitud.Subsanada, PerfilDemo.Admision) => [AccionSolicitud.VerificarCorreccion],
            (EstadoSolicitud.EvaluacionAcademica, PerfilDemo.Director) => [AccionSolicitud.EmitirDictamen, AccionSolicitud.SolicitarCorreccionAcademica],
            (EstadoSolicitud.DictamenEmitido, PerfilDemo.Director) => [AccionSolicitud.RemitirDecano],
            (EstadoSolicitud.RevisionDecanal, PerfilDemo.Decano) => [AccionSolicitud.AprobarDictamen, AccionSolicitud.DevolverDictamen],
            (EstadoSolicitud.DictamenAprobado, PerfilDemo.Decano) => [AccionSolicitud.EmitirResolucion],
            (EstadoSolicitud.ResolucionEmitida, PerfilDemo.Decano) => [AccionSolicitud.Notificar],
            (EstadoSolicitud.NotificacionRealizada, PerfilDemo.Decano) => [AccionSolicitud.RemitirRegistro],
            (EstadoSolicitud.RegistroAcademicoPendiente, PerfilDemo.RegistrosAcademicos) =>
                s.CargaConfirmadaUtc.HasValue ? [AccionSolicitud.Finalizar] : [AccionSolicitud.RegistrarAsignaturas],
            _ => []
        };
    }

    public static bool CumpleCriterios(AsignaturaSolicitud curso, int coincidencia, bool excepcion, string? justificacionExcepcion) =>
        curso.Nota >= 11 && curso.CreditosOrigen >= curso.CreditosDestino &&
        (coincidencia >= 80 || (coincidencia >= 70 && excepcion && curso.CreditosOrigen == curso.CreditosDestino
            && !string.IsNullOrWhiteSpace(justificacionExcepcion) && justificacionExcepcion.Trim().Length >= 5));

    public async Task<int> CrearAsync(CrearSolicitudViewModel model, PerfilDemo perfil, CancellationToken ct = default)
    {
        Exigir(perfil == PerfilDemo.Estudiante, "Sólo el estudiante puede presentar solicitudes.");
        Validar(model);
        Exigir(model.Cursos is { Count: >= 1 and <= 20 }, "Registra entre una y veinte asignaturas.");
        Exigir(DocumentosDemo.Certificados.ContainsKey(model.CertificadoDocumento), "Selecciona un certificado simulado válido.");
        foreach (var curso in model.Cursos) { Validar(curso); Exigir(DocumentosDemo.Silabos.ContainsKey(curso.SilaboDocumento), "Cada asignatura requiere un sílabo simulado válido."); }
        Exigir(model.Cursos.Select(c => c.AsignaturaId).Distinct().Count() == model.Cursos.Count, "No repitas asignaturas de destino en el expediente.");
        var ids = model.Cursos.Select(c => c.AsignaturaId).ToArray();
        var catalogo = await db.Asignaturas.Where(a => ids.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
        Exigir(catalogo.Count == ids.Length, "Una asignatura de destino no existe en el catálogo ficticio.");
        var ahora = clock.GetUtcNow().UtcDateTime;
        var primera = model.Cursos[0];
        var solicitud = new Solicitud
        {
            AmbitoDemo = db.AmbitoActual, Estudiante = model.Estudiante.Trim(), InstitucionOrigen = model.InstitucionOrigen.Trim(),
            CertificadoDocumento = model.CertificadoDocumento, Estado = EstadoSolicitud.Presentada,
            CreadaUtc = ahora, ActualizadaUtc = ahora,
            AsignaturaId = primera.AsignaturaId, AsignaturaOrigen = primera.AsignaturaOrigen.Trim(), CreditosOrigen = primera.CreditosOrigen, Nota = primera.Nota,
            Cursos = model.Cursos.Select(c => new AsignaturaSolicitud
            {
                AsignaturaId = c.AsignaturaId, AsignaturaOrigen = c.AsignaturaOrigen.Trim(), CreditosOrigen = c.CreditosOrigen,
                CreditosDestino = catalogo[c.AsignaturaId].Creditos, Nota = c.Nota, SilaboDocumento = c.SilaboDocumento
            }).ToList(),
            Historial = [new() { Estado = EstadoSolicitud.Presentada, Accion = AccionSolicitud.Presentar, Perfil = perfil, FechaUtc = ahora,
                Comentario = "Solicitud presentada con certificado y sílabos simulados." }]
        };
        NotificacionService.Agregar(solicitud, AccionSolicitud.Presentar, ahora);
        db.Solicitudes.Add(solicitud);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Expediente {SolicitudId} presentado con {Cursos} asignaturas", solicitud.Id, solicitud.Cursos.Count);
        return solicitud.Id;
    }

    public async Task EjecutarAsync(AccionSolicitudViewModel model, PerfilDemo perfil, CancellationToken ct = default)
    {
        Validar(model); Sustento(model.Comentario);
        var s = await Cargar(model.Id, model.Revision, ct);
        Exigir(Acciones(s, perfil).Contains(model.Accion), "Esta acción no está disponible para el estado y perfil actuales.");
        var anterior = s.Estado;
        var ahora = clock.GetUtcNow().UtcDateTime;
        switch (model.Accion)
        {
            case AccionSolicitud.Recibir: s.Estado = EstadoSolicitud.VerificacionDocumental; break;
            case AccionSolicitud.ObservarDocumentos:
                s.DocumentosVerificadosUtc = null; s.Estado = EstadoSolicitud.Observada; break;
            case AccionSolicitud.DevolverEstudiante: s.Estado = EstadoSolicitud.PendienteSubsanacion; break;
            case AccionSolicitud.Subsanar: throw new ReglaProcesoException("Utiliza el formulario de documentos corregidos.");
            case AccionSolicitud.VerificarCorreccion:
                Exigir(model.DocumentosVerificados, "Confirma la verificación del certificado y de todos los sílabos corregidos.");
                s.Estado = EstadoSolicitud.VerificacionDocumental; break;
            case AccionSolicitud.DerivarDepartamento:
                Exigir(model.DocumentosVerificados, "Confirma la verificación del certificado y de todos los sílabos.");
                Exigir(DocumentosDemo.Certificados.ContainsKey(s.CertificadoDocumento) && s.Cursos.Count > 0
                    && s.Cursos.All(c => DocumentosDemo.Silabos.ContainsKey(c.SilaboDocumento)), "Faltan documentos simulados requeridos.");
                s.DocumentosVerificadosUtc = ahora; s.Estado = EstadoSolicitud.EvaluacionAcademica; break;
            case AccionSolicitud.SolicitarCorreccionAcademica:
                Exigir(s.Cursos.Any(c => c.Resultado == ResultadoAcademico.Observada), "Primero registra al menos una asignatura observada con su justificación.");
                s.DocumentosVerificadosUtc = null; s.Estado = EstadoSolicitud.Observada; break;
            case AccionSolicitud.EmitirDictamen:
                Exigir(s.Cursos.Count > 0 && s.Cursos.All(c => c.EvaluadaUtc.HasValue && c.Resultado is ResultadoAcademico.Aprobada or ResultadoAcademico.Rechazada),
                    "Evalúa todas las asignaturas y resuelve las observaciones antes de emitir el dictamen.");
                var version = s.Dictamenes.Count + 1;
                s.Dictamenes.Add(new()
                {
                    Version = version, Numero = $"DEMO-DA-{s.Id:D4}-V{version}", EmitidoUtc = ahora,
                    Situacion = SituacionDictamen.Emitido, Justificacion = model.Comentario.Trim(),
                    DetallesJson = JsonSerializer.Serialize(s.Cursos.OrderBy(c => c.Id).Select(c => new ResultadoDictamen(c.Id,
                        c.AsignaturaOrigen, c.Asignatura.Nombre, c.Asignatura.Codigo, c.CreditosOrigen, c.CreditosDestino,
                        c.Nota, c.Coincidencia!.Value, c.Resultado, c.Justificacion, c.ExcepcionAfinidad, c.JustificacionExcepcion, c.EvaluadaUtc!.Value)))
                });
                s.Estado = EstadoSolicitud.DictamenEmitido; break;
            case AccionSolicitud.RemitirDecano:
                UltimoDictamen(s).Situacion = SituacionDictamen.EnRevision; s.Estado = EstadoSolicitud.RevisionDecanal; break;
            case AccionSolicitud.DevolverDictamen:
                var devuelto = UltimoDictamen(s); devuelto.Situacion = SituacionDictamen.Devuelto; devuelto.ObservacionDecanal = model.Comentario.Trim();
                // Obliga a una nueva revisión; las evaluaciones y los dictámenes previos conservan sus datos.
                foreach (var c in s.Cursos) { c.Resultado = ResultadoAcademico.Pendiente; c.EvaluadaUtc = null; }
                s.Estado = EstadoSolicitud.EvaluacionAcademica; break;
            case AccionSolicitud.AprobarDictamen:
                UltimoDictamen(s).Situacion = SituacionDictamen.Aprobado; s.Estado = EstadoSolicitud.DictamenAprobado; break;
            case AccionSolicitud.EmitirResolucion:
                var dictamen = UltimoDictamen(s);
                Exigir(dictamen.Situacion == SituacionDictamen.Aprobado && s.Resolucion is null, "Se requiere un dictamen aprobado sin resolución previa.");
                Exigir(!string.IsNullOrWhiteSpace(model.NumeroResolucion) && model.NumeroResolucion.StartsWith("DEMO-", StringComparison.Ordinal)
                    && model.NumeroResolucion.Trim().Length is >= 8 and <= 60, "El número ficticio debe comenzar con DEMO- y tener entre 8 y 60 caracteres.");
                Exigir(model.FechaResolucion.HasValue && model.FechaResolucion.Value.Year is >= 2000 and <= 2100, "Indica una fecha ficticia de resolución entre 2000 y 2100.");
                Exigir(!await db.Resoluciones.AnyAsync(r => r.Numero == model.NumeroResolucion!.Trim(), ct), "Ese número ficticio ya existe en tu sesión.");
                s.Resolucion = new() { DictamenAcademicoId = dictamen.Id, Numero = model.NumeroResolucion!.Trim(),
                    FechaDocumento = model.FechaResolucion!.Value, EmitidaUtc = ahora, Justificacion = model.Comentario.Trim(), DetallesJson = dictamen.DetallesJson };
                s.Estado = EstadoSolicitud.ResolucionEmitida; break;
            case AccionSolicitud.Notificar:
                Exigir(s.Resolucion is not null, "No existe una resolución para notificar.");
                s.Estado = EstadoSolicitud.NotificacionRealizada; break;
            case AccionSolicitud.RemitirRegistro: s.Estado = EstadoSolicitud.RegistroAcademicoPendiente; break;
            case AccionSolicitud.RegistrarAsignaturas:
                Exigir(s.Resolucion is not null && !s.CargaConfirmadaUtc.HasValue, "La carga no está disponible o ya fue confirmada.");
                var aprobadas = s.Resolucion!.Detalles.Where(d => d.Resultado == ResultadoAcademico.Aprobada).ToList();
                foreach (var d in aprobadas)
                {
                    var curso = s.Cursos.Single(c => c.Id == d.AsignaturaSolicitudId);
                    Exigir(curso.Resultado == ResultadoAcademico.Aprobada && curso.Registro is null, "La asignatura no está aprobada o ya fue registrada.");
                    curso.Registro = new() { ResolucionDecanalId = s.Resolucion.Id, Creditos = d.CreditosDestino, FechaUtc = ahora };
                }
                s.CargaConfirmadaUtc = ahora; break;
            case AccionSolicitud.Finalizar:
                Exigir(s.CargaConfirmadaUtc.HasValue && s.Cursos.All(c => c.Resultado != ResultadoAcademico.Aprobada || c.Registro is not null)
                    && s.Cursos.All(c => c.Resultado == ResultadoAcademico.Aprobada || c.Registro is null), "La carga académica debe estar confirmada y contener únicamente asignaturas aprobadas.");
                s.Estado = EstadoSolicitud.Finalizada; break;
            default: throw new ReglaProcesoException("Acción no admitida.");
        }
        await Guardar(s, anterior, model.Accion, perfil, model.Comentario, ahora, ct);
    }

    public async Task EvaluarAsync(EvaluarAsignaturaViewModel model, PerfilDemo perfil, CancellationToken ct = default)
    {
        Validar(model); Sustento(model.Justificacion);
        var s = await Cargar(model.Id, model.Revision, ct);
        Exigir(perfil == PerfilDemo.Director && s.Estado == EstadoSolicitud.EvaluacionAcademica, "Sólo el Director puede evaluar durante la etapa académica.");
        var curso = s.Cursos.SingleOrDefault(c => c.Id == model.AsignaturaSolicitudId)
            ?? throw new ReglaProcesoException("La asignatura no pertenece a este expediente.");
        Exigir(model.Resultado != ResultadoAcademico.Pendiente, "Selecciona aprobar, rechazar u observar y registra una justificación.");
        Exigir(!model.ExcepcionAfinidad || (!string.IsNullOrWhiteSpace(model.JustificacionExcepcion) && model.JustificacionExcepcion.Trim().Length >= 5), "Justifica la excepción por afinidad temática directa.");
        Exigir(model.Resultado != ResultadoAcademico.Aprobada || CumpleCriterios(curso, model.Coincidencia, model.ExcepcionAfinidad, model.JustificacionExcepcion),
            "Para aprobar se requiere nota mínima 11 y créditos suficientes; coincidencia mínima 80%, o 70% con afinidad directa justificada y créditos equivalentes. La decisión siempre es manual.");
        var ahora = clock.GetUtcNow().UtcDateTime;
        curso.Coincidencia = model.Coincidencia; curso.Resultado = model.Resultado; curso.Justificacion = model.Justificacion.Trim();
        curso.ExcepcionAfinidad = model.ExcepcionAfinidad; curso.JustificacionExcepcion = model.JustificacionExcepcion?.Trim() ?? ""; curso.EvaluadaUtc = ahora;
        curso.Evaluaciones.Add(new() { VersionDocumento = curso.VersionDocumento, Coincidencia = model.Coincidencia,
            Resultado = model.Resultado, Justificacion = curso.Justificacion, ExcepcionAfinidad = curso.ExcepcionAfinidad,
            JustificacionExcepcion = curso.JustificacionExcepcion, FechaUtc = ahora });
        // El detalle y la asignatura quedan en Evaluaciones; el historial conserva el sustento completo sin exceder su límite.
        await Guardar(s, s.Estado, AccionSolicitud.EvaluarAsignatura, perfil, curso.Justificacion, ahora, ct);
    }

    public async Task SubsanarAsync(SubsanarSolicitudViewModel model, PerfilDemo perfil, CancellationToken ct = default)
    {
        Validar(model); Sustento(model.Comentario);
        var s = await Cargar(model.Id, model.Revision, ct);
        Exigir(perfil == PerfilDemo.Estudiante && s.Estado == EstadoSolicitud.PendienteSubsanacion, "El estudiante sólo puede subsanar los expedientes devueltos para corrección.");
        Exigir(DocumentosDemo.Certificados.ContainsKey(model.CertificadoDocumento), "Selecciona un certificado simulado válido.");
        Exigir(model.Cursos.Count == s.Cursos.Count && model.Cursos.Select(c => c.AsignaturaSolicitudId).Distinct().Count() == s.Cursos.Count
            && model.Cursos.All(c => s.Cursos.Any(x => x.Id == c.AsignaturaSolicitudId)), "Incluye exactamente los sílabos de este expediente.");
        foreach (var c in model.Cursos) { Validar(c); Exigir(DocumentosDemo.Silabos.ContainsKey(c.SilaboDocumento), "Selecciona sílabos simulados válidos."); }
        Exigir(model.CertificadoDocumento != s.CertificadoDocumento || model.Cursos.Any(c => s.Cursos.Single(x => x.Id == c.AsignaturaSolicitudId).SilaboDocumento != c.SilaboDocumento),
            "Selecciona al menos un documento corregido distinto del presentado.");
        var ahora = clock.GetUtcNow().UtcDateTime;
        if (s.CertificadoDocumento != model.CertificadoDocumento)
            s.Correcciones.Add(new() { Anterior = s.CertificadoDocumento, Corregido = model.CertificadoDocumento, Justificacion = model.Comentario.Trim(), FechaUtc = ahora });
        s.CertificadoDocumento = model.CertificadoDocumento;
        foreach (var correccion in model.Cursos)
        {
            var curso = s.Cursos.Single(c => c.Id == correccion.AsignaturaSolicitudId);
            if (curso.SilaboDocumento == correccion.SilaboDocumento) continue;
            s.Correcciones.Add(new() { AsignaturaSolicitudId = curso.Id, Anterior = curso.SilaboDocumento, Corregido = correccion.SilaboDocumento,
                Justificacion = model.Comentario.Trim(), FechaUtc = ahora });
            curso.SilaboDocumento = correccion.SilaboDocumento; curso.VersionDocumento++;
            curso.Resultado = ResultadoAcademico.Pendiente; curso.EvaluadaUtc = null; curso.Coincidencia = null;
            curso.Justificacion = ""; curso.ExcepcionAfinidad = false; curso.JustificacionExcepcion = "";
        }
        var anterior = s.Estado; s.Estado = EstadoSolicitud.Subsanada;
        await Guardar(s, anterior, AccionSolicitud.Subsanar, perfil, model.Comentario, ahora, ct);
    }

    private async Task<Solicitud> Cargar(int id, Guid revision, CancellationToken ct)
    {
        var s = await db.Solicitudes.Include(s => s.Cursos).ThenInclude(c => c.Asignatura)
            .Include(s => s.Cursos).ThenInclude(c => c.Registro).Include(s => s.Dictamenes).Include(s => s.Resolucion)
            .AsSplitQuery().SingleOrDefaultAsync(s => s.Id == id, ct) ?? throw new ReglaProcesoException("El expediente no está disponible en esta sesión.");
        Exigir(s.VersionProceso == 2, "Este expediente pertenece a la versión anterior y se conserva para consulta histórica.");
        Exigir(s.Revision == revision, "Otra operación actualizó este expediente. Recarga la página antes de continuar.");
        return s;
    }

    private async Task Guardar(Solicitud s, EstadoSolicitud anterior, AccionSolicitud accion, PerfilDemo perfil, string comentario, DateTime fecha, CancellationToken ct)
    {
        s.ActualizadaUtc = fecha; s.Revision = Guid.NewGuid();
        s.Historial.Add(new() { EstadoAnterior = anterior, Estado = s.Estado, Accion = accion, Perfil = perfil, FechaUtc = fecha, Comentario = comentario.Trim() });
        NotificacionService.Agregar(s, accion, fecha);
        await db.SaveChangesAsync(ct); // Una transacción EF guarda transición, detalles, documentos y revisión de forma atómica.
        logger.LogInformation("Expediente {SolicitudId}: {Accion}, {Anterior} → {Estado}, {Perfil}", s.Id, accion, anterior, s.Estado, perfil);
    }
    private static DictamenAcademico UltimoDictamen(Solicitud s) => s.Dictamenes.MaxBy(d => d.Version)
        ?? throw new ReglaProcesoException("No existe un dictamen académico.");
    private static void Validar(object model)
    {
        var errores = new List<ValidationResult>();
        if (!Validator.TryValidateObject(model, new ValidationContext(model), errores, true))
            throw new ReglaProcesoException(string.Join(" ", errores.Select(e => e.ErrorMessage)));
    }
    private static void Sustento(string? texto) => Exigir(!string.IsNullOrWhiteSpace(texto) && texto.Trim().Length is >= 5 and <= 1000, "El sustento debe tener entre 5 y 1000 caracteres.");
    private static void Exigir(bool condicion, string mensaje) { if (!condicion) throw new ReglaProcesoException(mensaje); }
}
