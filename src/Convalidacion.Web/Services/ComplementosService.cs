using System.ComponentModel.DataAnnotations;
using Convalidacion.Web.Data;
using Convalidacion.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Convalidacion.Web.Services;

public class ComplementosService(AppDbContext db, TimeProvider clock)
{
    public async Task<ResultadoCalculadora> CalcularAsync(CalculadoraViewModel model, CancellationToken ct = default)
    {
        var errores = new List<ValidationResult>();
        if (!Validator.TryValidateObject(model, new ValidationContext(model), errores, true))
            throw new ReglaProcesoException(string.Join(" ", errores.Select(e => e.ErrorMessage)));
        var destino = await db.Asignaturas.AsNoTracking().SingleOrDefaultAsync(a => a.Id == model.AsignaturaId, ct)
            ?? throw new ReglaProcesoException("Selecciona una asignatura del catálogo ficticio.");
        var mensaje = model.CreditosOrigen < destino.Creditos ? "Créditos de origen insuficientes para la referencia didáctica."
            : model.Coincidencia >= 80 ? "Alcanza la referencia general de contenidos del 80%. Requiere evaluación académica manual."
            : model.Coincidencia >= 70 && model.AfinidadDirecta && model.CreditosOrigen == destino.Creditos
                ? "Puede ser considerado en la excepción orientativa desde el 70%, con afinidad directa y créditos equivalentes. Requiere sustento del Director."
                : "No alcanza las condiciones orientativas de contenidos y afinidad de esta demostración.";
        return new(destino, mensaje);
    }

    public async Task<EquivalenciasViewModel> EquivalenciasAsync(string? buscar, int? creditos, int? expedienteId, CancellationToken ct = default)
    {
        var query = db.Asignaturas.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var texto = buscar.Trim();
            query = query.Where(a => a.Nombre.Contains(texto) || a.Codigo.Contains(texto));
        }
        if (creditos.HasValue) query = query.Where(a => a.Creditos == creditos);
        Solicitud? expediente = null;
        if (expedienteId.HasValue)
            expediente = await db.Solicitudes.AsNoTracking().Include(s => s.Cursos).ThenInclude(c => c.Asignatura)
                .SingleOrDefaultAsync(s => s.Id == expedienteId, ct)
                ?? throw new ReglaProcesoException("El expediente no está disponible en esta sesión.");
        return new(await query.OrderBy(a => a.Codigo).ToListAsync(ct),
            await db.Solicitudes.AsNoTracking().OrderByDescending(s => s.Id).ToListAsync(ct), expediente, buscar, creditos);
    }

    public async Task<IndicadoresViewModel> IndicadoresAsync(CancellationToken ct = default)
    {
        var solicitudes = await db.Solicitudes.AsNoTracking().Include(s => s.Historial)
            .Include(s => s.Cursos).ThenInclude(c => c.Evaluaciones).AsSplitQuery().ToListAsync(ct);
        var ahora = clock.GetUtcNow().UtcDateTime;
        var duraciones = solicitudes.Where(s => s.Estado == EstadoSolicitud.Finalizada).Select(s =>
        {
            var fin = s.Historial.Where(h => h.Accion == AccionSolicitud.Finalizar).Select(h => (DateTime?)h.FechaUtc).Max();
            return fin.HasValue && fin >= s.CreadaUtc && fin <= ahora ? (double?)(fin.Value - s.CreadaUtc).TotalHours : null;
        }).Where(d => d.HasValue).Select(d => d!.Value).ToList();
        var observadas = solicitudes.Count(s => s.Estado is EstadoSolicitud.Observada or EstadoSolicitud.PendienteSubsanacion
            || s.Historial.Any(h => h.Estado is EstadoSolicitud.Observada or EstadoSolicitud.PendienteSubsanacion || h.Accion == AccionSolicitud.DevolverDictamen)
            || s.Cursos.Any(c => c.Resultado == ResultadoAcademico.Observada || c.Evaluaciones.Any(e => e.Resultado == ResultadoAcademico.Observada)));
        var cursos = solicitudes.SelectMany(s => s.Cursos).ToList();
        var decididas = cursos.Count(c => c.Resultado is ResultadoAcademico.Aprobada or ResultadoAcademico.Rechazada);
        var aprobadas = cursos.Count(c => c.Resultado == ResultadoAcademico.Aprobada);
        return new(solicitudes.Count, solicitudes.Count(s => s.Estado == EstadoSolicitud.Finalizada),
            duraciones.Count > 0 ? duraciones.Average() : null, duraciones.Count, observadas,
            solicitudes.Count > 0 ? 100m * observadas / solicitudes.Count : null,
            aprobadas, decididas, decididas > 0 ? 100m * aprobadas / decididas : null,
            Enumerable.Range(1, 4).ToDictionary(e => e, e => solicitudes.Count(s => s.Etapa == e)),
            Enum.GetValues<PerfilDemo>().ToDictionary(p => p, p => solicitudes.Count(s => s.VersionProceso == 2 && SolicitudService.EstadosResponsables(p).Contains(s.Estado))));
    }

    public async Task ReiniciarAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(db.AmbitoActual)) throw new ReglaProcesoException("Se requiere una sesión de demostración activa.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Se respetan los filtros de ámbito. Primero se eliminan las referencias restrictivas.
        await db.Registros.ExecuteDeleteAsync(ct);
        await db.Resoluciones.ExecuteDeleteAsync(ct);
        await db.Solicitudes.ExecuteDeleteAsync(ct);
        db.ChangeTracker.Clear();
        await DemoSeeder.SeedAsync(db);
        await transaction.CommitAsync(ct);
    }
}
