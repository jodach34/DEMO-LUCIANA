using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Convalidacion.Web.Models;
using Convalidacion.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Convalidacion.Web.Controllers;

public class HomeController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var counts = await db.Solicitudes.GroupBy(s => s.Estado)
            .Select(g => new { Estado = g.Key, Total = g.Count() }).ToDictionaryAsync(g => g.Estado, g => g.Total, ct);
        var recientes = await db.Solicitudes.AsNoTracking().Include(s => s.Asignatura)
            .OrderByDescending(s => s.CreadaUtc).ThenByDescending(s => s.Id).Take(5).ToListAsync(ct);
        return View(new DashboardViewModel(counts.Values.Sum(), counts.GetValueOrDefault(EstadoSolicitud.Recibida)
            + counts.GetValueOrDefault(EstadoSolicitud.EnRevision), counts.GetValueOrDefault(EstadoSolicitud.Observada),
            counts.GetValueOrDefault(EstadoSolicitud.Aprobada), recientes));
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    public IActionResult StatusCodePage(int code)
    {
        ViewData["StatusCode"] = code;
        return View("StatusCodePage");
    }
}
