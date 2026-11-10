using Convalidacion.Web.Models;

namespace Convalidacion.Web.Services;

// Solo cambia la perspectiva de la demo. No representa autenticación ni autorización real.
public class PerfilDemoService(IHttpContextAccessor accessor)
{
    public PerfilDemo Actual => Enum.TryParse<PerfilDemo>(accessor.HttpContext?.Session.GetString("PerfilFlujoV2"), out var perfil)
                               && Enum.IsDefined(perfil) ? perfil : PerfilDemo.Estudiante;
    public void Cambiar(PerfilDemo perfil) => accessor.HttpContext!.Session.SetString("PerfilFlujoV2", perfil.ToString());
}
