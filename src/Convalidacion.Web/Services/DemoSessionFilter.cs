using Convalidacion.Web.Data;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Convalidacion.Web.Services;

// Mantiene los perfiles de la demo dentro del mismo navegador y aísla sus expedientes.
public class DemoSessionFilter(AppDbContext db, IConfiguration configuration) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var session = context.HttpContext.Session;
        await session.LoadAsync(context.HttpContext.RequestAborted);
        if (session.GetString("AmbitoDemo") is null)
            session.SetString("AmbitoDemo", Guid.NewGuid().ToString("N"));
        if (session.GetInt32("EjemplosInicializados") is null)
        {
            if (configuration.GetValue("Demo:SeedData", true)) await DemoSeeder.SeedAsync(db);
            session.SetInt32("EjemplosInicializados", 1);
        }
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        await next();
    }
}
