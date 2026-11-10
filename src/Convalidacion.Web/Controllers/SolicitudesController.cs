using Convalidacion.Web.Data;
using Convalidacion.Web.Models;
using Convalidacion.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Convalidacion.Web.Controllers;

public class SolicitudesController(AppDbContext db, SolicitudService service, PerfilDemoService perfiles) : Controller
{
    public async Task<IActionResult> Index(EstadoSolicitud? estado, string? buscar, bool pendientes, bool conDictamen, bool conResolucion, CancellationToken ct)
    {
        if (!ModelState.IsValid || (estado.HasValue && !Enum.IsDefined(estado.Value)) || buscar?.Length > 120) return BadRequest();
        var query = db.Solicitudes.AsNoTracking().Include(s => s.Cursos).ThenInclude(c => c.Asignatura).AsQueryable();
        if (estado.HasValue) query = query.Where(s => s.Estado == estado.Value);
        if (pendientes) { var estados = SolicitudService.EstadosResponsables(perfiles.Actual); query = query.Where(s => s.VersionProceso == 2 && estados.Contains(s.Estado)); }
        if (conDictamen) query = query.Where(s => s.Dictamenes.Any());
        if (conResolucion) query = query.Where(s => s.Resolucion != null);
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            buscar = buscar.Trim();
            query = query.Where(s => s.Estudiante.Contains(buscar) || s.Cursos.Any(c => c.Asignatura.Nombre.Contains(buscar) || c.AsignaturaOrigen.Contains(buscar))
                || s.Id.ToString() == buscar);
        }
        return View(new BandejaViewModel(await query.OrderByDescending(s => s.CreadaUtc).ThenByDescending(s => s.Id).Take(100).ToListAsync(ct),
            estado, buscar, pendientes, conDictamen, conResolucion));
    }

    public async Task<IActionResult> Crear(CancellationToken ct) { await CargarAsignaturas(ct); return View(new CrearSolicitudViewModel()); }

    [HttpPost]
    public async Task<IActionResult> Crear(CrearSolicitudViewModel model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var id = await service.CrearAsync(model, perfiles.Actual, ct);
                HttpContext.Session.SetInt32("ExpedienteActivoId", id);
                TempData["Mensaje"] = "Solicitud presentada. La Oficina de Admisión recibirá el expediente.";
                return RedirectToAction(nameof(Detalle), new { id });
            }
            catch (ReglaProcesoException ex) { ModelState.AddModelError("", ex.Message); }
        }
        await CargarAsignaturas(ct); return View(model);
    }

    public async Task<IActionResult> Detalle(int id, CancellationToken ct)
    {
        var s = await Buscar(id, ct); if (s is null) return NotFound();
        HttpContext.Session.SetInt32("ExpedienteActivoId", id);
        return View(Modelo(s));
    }

    [HttpPost]
    public async Task<IActionResult> Ejecutar(AccionSolicitudViewModel model, CancellationToken ct)
    {
        if (await Intentar(() => service.EjecutarAsync(model, perfiles.Actual, ct))) return Exito(model.Id, "Transición registrada en el historial del expediente.");
        var s = await Buscar(model.Id, ct); return s is null ? NotFound() : View("Detalle", Modelo(s, model));
    }

    [HttpPost]
    public async Task<IActionResult> Evaluar(EvaluarAsignaturaViewModel model, CancellationToken ct)
    {
        if (await Intentar(() => service.EvaluarAsync(model, perfiles.Actual, ct))) return Exito(model.Id, "Evaluación individual registrada por el Director del Departamento Académico.");
        var s = await Buscar(model.Id, ct); return s is null ? NotFound() : View("Detalle", Modelo(s, new() { Id = model.Id, Revision = model.Revision }) with { Evaluacion = model });
    }

    [HttpPost]
    public async Task<IActionResult> Subsanar(SubsanarSolicitudViewModel model, CancellationToken ct)
    {
        if (await Intentar(() => service.SubsanarAsync(model, perfiles.Actual, ct))) return Exito(model.Id, "Documentos corregidos enviados a la Oficina de Admisión. Se conservó su historial.");
        var s = await Buscar(model.Id, ct); return s is null ? NotFound() : View("Detalle", Modelo(s, new() { Id = model.Id, Revision = model.Revision }) with { Subsanacion = model });
    }

    public async Task<IActionResult> Dictamen(int id, int? version, CancellationToken ct)
    {
        var s = await Buscar(id, ct); if (s is null) return NotFound();
        var dictamen = version.HasValue ? s.Dictamenes.SingleOrDefault(d => d.Version == version) : s.Dictamenes.MaxBy(d => d.Version);
        return dictamen is null ? NotFound() : View(dictamen);
    }

    public async Task<IActionResult> Resolucion(int id, CancellationToken ct)
    {
        var s = await Buscar(id, ct); return s?.Resolucion is null ? NotFound() : View(s.Resolucion);
    }

    private DetalleViewModel Modelo(Solicitud s, AccionSolicitudViewModel? accion = null) => new(s, perfiles.Actual,
        accion ?? new() { Id = s.Id, Revision = s.Revision, NumeroResolucion = $"DEMO-RD-{s.Codigo}", FechaResolucion = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5)) });
    private IActionResult Exito(int id, string mensaje) { TempData["Mensaje"] = mensaje; return RedirectToAction(nameof(Detalle), new { id }); }
    private async Task<bool> Intentar(Func<Task> action)
    {
        if (!ModelState.IsValid) return false;
        try { await action(); return true; }
        catch (ReglaProcesoException ex) { ModelState.AddModelError("", ex.Message); }
        catch (DbUpdateConcurrencyException) { ModelState.AddModelError("", "Otra operación cambió el expediente. Recarga la página para continuar."); }
        return false;
    }

    private Task<Solicitud?> Buscar(int id, CancellationToken ct) => db.Solicitudes.AsNoTracking()
        .Include(s => s.Cursos).ThenInclude(c => c.Asignatura).Include(s => s.Cursos).ThenInclude(c => c.Evaluaciones)
        .Include(s => s.Cursos).ThenInclude(c => c.Registro).Include(s => s.Historial).Include(s => s.Correcciones)
        .Include(s => s.Dictamenes).Include(s => s.Resolucion).Include(s => s.Notificaciones).AsSplitQuery().SingleOrDefaultAsync(s => s.Id == id, ct);
    private async Task CargarAsignaturas(CancellationToken ct) => ViewBag.Asignaturas = await db.Asignaturas.AsNoTracking().OrderBy(a => a.Codigo).ToListAsync(ct);
}
