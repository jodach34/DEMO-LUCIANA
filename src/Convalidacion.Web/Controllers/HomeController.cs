using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Convalidacion.Web.Models;
using Convalidacion.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Convalidacion.Web.Controllers;

public class HomeController(AppDbContext db, Convalidacion.Web.Services.PerfilDemoService perfiles,
    Convalidacion.Web.Services.ComplementosService complementos) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var counts = await db.Solicitudes.GroupBy(s => s.Estado)
            .Select(g => new { Estado = g.Key, Total = g.Count() }).ToDictionaryAsync(g => g.Estado, g => g.Total, ct);
        var estados = Convalidacion.Web.Services.SolicitudService.EstadosResponsables(perfiles.Actual);
        var query = db.Solicitudes.AsNoTracking().Include(s => s.Cursos).ThenInclude(c => c.Asignatura).AsQueryable();
        if (perfiles.Actual != PerfilDemo.Estudiante) query = query.Where(s => s.VersionProceso == 2 && estados.Contains(s.Estado));
        var recientes = await query.OrderByDescending(s => s.ActualizadaUtc).ThenByDescending(s => s.Id).Take(5).ToListAsync(ct);
        var activoId = HttpContext.Session.GetInt32("ExpedienteActivoId");
        var activa = activoId.HasValue ? await db.Solicitudes.AsNoTracking().Include(s => s.Cursos).ThenInclude(c => c.Asignatura)
            .SingleOrDefaultAsync(s => s.Id == activoId.Value, ct) : null;
        activa ??= recientes.FirstOrDefault(s => s.VersionProceso == 2 && s.Estado != EstadoSolicitud.Finalizada);
        var indicadores = await complementos.IndicadoresAsync(ct);
        return View(new DashboardViewModel(counts.Values.Sum(), counts.Where(p => estados.Contains(p.Key)).Sum(p => p.Value),
            indicadores.Observadas,
            counts.GetValueOrDefault(EstadoSolicitud.Finalizada), recientes, activa));
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
