using Convalidacion.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Convalidacion.Web.Controllers;

public class ComplementosController(ComplementosService service) : Controller
{
    // Estas herramientas independientes quedan fuera del recorrido de la demo.
    [AcceptVerbs("GET", "POST")]
    public IActionResult Calculadora() => RedirectToAction("Index", "Home");

    public IActionResult Equivalencias() => RedirectToAction("Index", "Home");
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
