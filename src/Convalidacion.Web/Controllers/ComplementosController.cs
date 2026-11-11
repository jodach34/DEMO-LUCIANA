using Convalidacion.Web.Data;
using Convalidacion.Web.Models;
using Convalidacion.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Convalidacion.Web.Controllers;

public class ComplementosController(ComplementosService service, AppDbContext db) : Controller
{
    public async Task<IActionResult> Calculadora(CancellationToken ct) => View(new CalculadoraViewModel { Catalogo = await db.Asignaturas.AsNoTracking().OrderBy(a => a.Codigo).ToListAsync(ct) });

    [HttpPost]
    public async Task<IActionResult> Calculadora(CalculadoraViewModel model, CancellationToken ct)
    {
        if (ModelState.IsValid)
            try { model.Resultado = await service.CalcularAsync(model, ct); }
            catch (ReglaProcesoException ex) { ModelState.AddModelError("", ex.Message); }
        model.Catalogo = await db.Asignaturas.AsNoTracking().OrderBy(a => a.Codigo).ToListAsync(ct);
        return View(model);
    }

    public async Task<IActionResult> Equivalencias(string? buscar, int? creditos, int? expedienteId, CancellationToken ct)
    {
        if (!ModelState.IsValid || buscar?.Length > 120 || creditos is < 1 or > 10) return BadRequest();
        try { return View(await service.EquivalenciasAsync(buscar, creditos, expedienteId, ct)); }
        catch (ReglaProcesoException) { return NotFound(); }
    }
    public async Task<IActionResult> Indicadores(CancellationToken ct) => View(await service.IndicadoresAsync(ct));
    public IActionResult Proceso() => View();
    public IActionResult Ayuda() => View();

    [HttpPost]
    public async Task<IActionResult> Reiniciar(bool confirmar, CancellationToken ct)
    {
        if (!confirmar) return BadRequest();
        await service.ReiniciarAsync(ct);
        HttpContext.Session.Remove("ExpedienteActivoId");
        TempData["Mensaje"] = "Demostración reiniciada: seis expedientes ficticios y sus notificaciones iniciales en esta sesión.";
        return RedirectToAction("Index", "Home");
    }
}
