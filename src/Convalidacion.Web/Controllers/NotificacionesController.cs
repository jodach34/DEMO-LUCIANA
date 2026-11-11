using Convalidacion.Web.Models;
using Convalidacion.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Convalidacion.Web.Controllers;

public class NotificacionesController(NotificacionService service) : Controller
{
    public async Task<IActionResult> Index(bool soloSinLeer = false, int pagina = 1, CancellationToken ct = default)
    {
        if (!ModelState.IsValid || pagina < 1) return BadRequest();
        var query = service.DelPerfil.AsNoTracking();
        var sinLeer = await query.CountAsync(n => n.LeidaUtc == null, ct);
        if (soloSinLeer) query = query.Where(n => n.LeidaUtc == null);
        var paginas = Math.Max(1, (int)Math.Ceiling(await query.CountAsync(ct) / 20d));
        pagina = Math.Min(pagina, paginas);
        var avisos = await query.Include(n => n.Solicitud).OrderByDescending(n => n.FechaUtc).ThenByDescending(n => n.Id)
            .Skip((pagina - 1) * 20).Take(20).ToListAsync(ct);
        return View(new NotificacionesViewModel(avisos, sinLeer, soloSinLeer, pagina, paginas));
    }

    [HttpPost]
    public async Task<IActionResult> Leer(int id, CancellationToken ct)
    {
        if (!await service.MarcarLeidaAsync(id, ct)) return NotFound();
        return RedirectToAction(nameof(Index));
    }
}
