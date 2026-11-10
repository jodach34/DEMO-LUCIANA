using Convalidacion.Web.Models;
using Convalidacion.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Convalidacion.Web.Controllers;

public class PerfilesController(PerfilDemoService perfiles) : Controller
{
    [HttpPost]
    public IActionResult Cambiar(PerfilDemo perfil, string? returnUrl)
    {
        if (!ModelState.IsValid || !Enum.IsDefined(perfil)) return BadRequest();
        perfiles.Cambiar(perfil);
        TempData["Mensaje"] = $"Perspectiva de demo: {perfil.Texto()}.";
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : RedirectToAction("Index", "Home");
    }
}
