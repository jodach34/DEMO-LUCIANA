using Convalidacion.Web.Data;
using Convalidacion.Web.Models;
using Convalidacion.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Convalidacion.Web.Controllers;

public class SolicitudesController(AppDbContext db, SolicitudService service, PerfilDemoService perfiles) : Controller
{
    public async Task<IActionResult> Index(EstadoSolicitud? estado, string? buscar, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest();
        if (estado.HasValue && !Enum.IsDefined(estado.Value)) return BadRequest();
        if (buscar?.Length > 120) return BadRequest();
        var query = db.Solicitudes.AsNoTracking().Include(s => s.Asignatura).AsQueryable();
        if (estado.HasValue) query = query.Where(s => s.Estado == estado.Value);
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            buscar = buscar.Trim();
            query = query.Where(s => s.Estudiante.Contains(buscar) || s.Asignatura.Nombre.Contains(buscar)
                || s.Id.ToString() == buscar);
        }
        return View(new BandejaViewModel(await query.OrderByDescending(s => s.CreadaUtc)
            .ThenByDescending(s => s.Id).Take(100).ToListAsync(ct), estado, buscar));
    }

    public async Task<IActionResult> Crear(CancellationToken ct)
    {
        await CargarAsignaturas(ct);
        return View(new CrearSolicitudViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Crear(CrearSolicitudViewModel model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var id = await service.CrearAsync(model, perfiles.Actual, ct);
                TempData["Mensaje"] = "Solicitud registrada. Cambia a Secretaría para iniciar su revisión.";
                return RedirectToAction(nameof(Detalle), new { id });
            }
            catch (ReglaProcesoException ex) { ModelState.AddModelError("", ex.Message); }
        }
        await CargarAsignaturas(ct);
        return View(model);
    }

    public async Task<IActionResult> Detalle(int id, CancellationToken ct)
    {
        var solicitud = await Buscar(id, ct);
        return solicitud is null ? NotFound() : View(new DetalleViewModel(solicitud, perfiles.Actual,
            new() { Id = id, Revision = solicitud.Revision }));
    }

    [HttpPost]
    public async Task<IActionResult> Ejecutar(AccionSolicitudViewModel model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await service.EjecutarAsync(model, perfiles.Actual, ct);
                TempData["Mensaje"] = "Cambio registrado en el historial de la solicitud.";
                return RedirectToAction(nameof(Detalle), new { id = model.Id });
            }
            catch (ReglaProcesoException ex) { ModelState.AddModelError("", ex.Message); }
            catch (DbUpdateConcurrencyException)
            {
                ModelState.AddModelError("", "Otra operación cambió esta solicitud. Recarga la página para continuar.");
            }
        }
        var solicitud = await Buscar(model.Id, ct);
        if (solicitud is null) return NotFound();
        // Conserva la revisión enviada: el usuario debe recargar antes de sobrescribir cambios concurrentes.
        return View("Detalle", new DetalleViewModel(solicitud, perfiles.Actual, model));
    }

    private Task<Solicitud?> Buscar(int id, CancellationToken ct) => db.Solicitudes.AsNoTracking()
        .Include(s => s.Asignatura).Include(s => s.Historial).SingleOrDefaultAsync(s => s.Id == id, ct);

    private async Task CargarAsignaturas(CancellationToken ct) => ViewBag.Asignaturas = new SelectList(
        await db.Asignaturas.AsNoTracking().OrderBy(a => a.Codigo).Select(a => new { a.Id,
            Nombre = a.Codigo + " · " + a.Nombre + " (" + a.Creditos + " créditos)" }).ToListAsync(ct), "Id", "Nombre");
}
