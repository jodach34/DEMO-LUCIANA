using System.Net;
using System.Text.RegularExpressions;
using Convalidacion.Web.Data;
using Convalidacion.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Convalidacion.Tests;

public class ComplementosWebTests
{
    private static HttpClient Client(DemoFactory factory) => factory.CreateClient(new() { AllowAutoRedirect = false });
    private static string Token(string html) => WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, string html, params (string, string)[] fields)
    {
        var values = fields.ToDictionary(p => p.Item1, p => p.Item2); values["__RequestVerificationToken"] = Token(html);
        return await client.PostAsync(path, new FormUrlEncodedContent(values));
    }
    private static int Unread(string html) => int.Parse(Regex.Match(html, "data-unread>([0-9]+)").Groups[1].Value);
    private static string Aviso(string html) => Regex.Match(html, "data-notification=\"([0-9]+)\"").Groups[1].Value;

    [Theory]
    [InlineData("Estudiante")][InlineData("Admision")][InlineData("Director")][InlineData("Decano")][InlineData("RegistrosAcademicos")]
    public async Task CincoPerfilesNaveganSinAccesosAHerramientasOcultas(string perfil)
    {
        using var factory = new DemoFactory(); using var client = Client(factory);
        var html = await client.GetStringAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, (await Post(client, "/Perfiles/Cambiar", html, ("perfil", perfil))).StatusCode);
        foreach(var path in new[] { "/", "/Solicitudes", "/Home/Privacy", "/Complementos/Indicadores", "/Complementos/Proceso", "/Complementos/Ayuda", "/Notificaciones" })
        {
            var response = await client.GetAsync(path); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            html = await response.Content.ReadAsStringAsync();
            Assert.Contains("value=\"" + perfil + "\" selected=\"selected\"", html);
            Assert.Contains("/images/usmp-logo.jpg", html); Assert.Contains("name=\"viewport\"", html);
            Assert.Contains("Reiniciar demostraci", WebUtility.HtmlDecode(html));
            Assert.DoesNotContain("Secretaría", WebUtility.HtmlDecode(html)); Assert.DoesNotContain("Comisión académica", WebUtility.HtmlDecode(html));
            Assert.DoesNotContain("Calculadora", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Equivalencias", html, StringComparison.OrdinalIgnoreCase);
            if (path == "/")
            {
                Assert.Contains("institutional-banner", html); Assert.Contains("dashboard-grid", html);
                Assert.Contains("quick-access-grid", html); Assert.Contains("dashboard-metrics", html);
            }
            foreach (var href in Regex.Matches(html, "href=\"(/[^\"#]*)\"").Select(m => WebUtility.HtmlDecode(m.Groups[1].Value)).Distinct())
                Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(href)).StatusCode);
        }
        foreach (var path in new[] { "/Complementos/Calculadora", "/Complementos/Equivalencias", "/Complementos/Equivalencias?buscar=DEMO-103&creditos=3", "/Complementos/Equivalencias?expedienteId=9999", "/Complementos/Equivalencias?creditos=abc" })
        {
            var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/", response.Headers.Location!.ToString());
            html = await client.GetStringAsync(response.Headers.Location);
            Assert.Contains("value=\"" + perfil + "\" selected=\"selected\"", html);
        }
    }

    [Fact]
    public async Task CalculadoraPostRedirigeSinCambiarDatosYConservaAntiforgery()
    {
        using var factory = new DemoFactory(); using var client = Client(factory);
        var html = await client.GetStringAsync("/");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var antes = await db.Solicitudes.IgnoreQueryFilters().OrderBy(s => s.Id).Select(s => new { s.Id, s.Revision, s.Estado }).ToListAsync();
        foreach (var destino in new[] { "1", "999" })
        {
            var response = await Post(client, "/Complementos/Calculadora", html, ("AsignaturaId", destino), ("AsignaturaOrigen", "Curso ficticio"), ("CreditosOrigen", "4"), ("Coincidencia", "70"), ("AfinidadDirecta", "true"));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/", response.Headers.Location!.ToString());
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Complementos/Calculadora", new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode);
        Assert.Equal(antes, await db.Solicitudes.IgnoreQueryFilters().OrderBy(s => s.Id).Select(s => new { s.Id, s.Revision, s.Estado }).ToListAsync());
    }

    [Fact]
    public async Task AvisosSeAislanPorPerfilYSesionYLeerEsIdempotente()
    {
        using var factory = new DemoFactory(); using var a = Client(factory); using var b = Client(factory);
        var html = await a.GetStringAsync("/Notificaciones"); var id = Aviso(html); var sinLeer = Unread(html);
        Assert.True(sinLeer > 5); Assert.Contains("Notificaciones sin leer del perfil: " + sinLeer, html);
        var ajeno = await b.GetStringAsync("/Notificaciones");
        Assert.Equal(HttpStatusCode.NotFound, (await Post(b, "/Notificaciones/Leer", ajeno, ("id", id))).StatusCode);
        await Post(a, "/Perfiles/Cambiar", html, ("perfil", "Director")); html = await a.GetStringAsync("/Notificaciones");
        Assert.Equal(HttpStatusCode.NotFound, (await Post(a, "/Notificaciones/Leer", html, ("id", id))).StatusCode);
        await Post(a, "/Perfiles/Cambiar", html, ("perfil", "Estudiante")); html = await a.GetStringAsync("/Notificaciones");
        Assert.Equal(HttpStatusCode.Redirect, (await Post(a, "/Notificaciones/Leer", html, ("id", id))).StatusCode);
        html = await a.GetStringAsync("/Notificaciones"); Assert.Equal(sinLeer - 1, Unread(html));
        Assert.Equal(HttpStatusCode.Redirect, (await Post(a, "/Notificaciones/Leer", html, ("id", id))).StatusCode);
        Assert.Equal(sinLeer - 1, Unread(await a.GetStringAsync("/Notificaciones")));
        Assert.DoesNotContain("data-notification=\"" + id + "\"", await a.GetStringAsync("/Notificaciones?soloSinLeer=true"));
        Assert.Equal(Unread(ajeno), Unread(await b.GetStringAsync("/Notificaciones")));
    }

    [Fact]
    public async Task RecargarPaginasNoGeneraAvisosYLaPaginacionConservaHistorial()
    {
        using var factory = new DemoFactory(); using var client = Client(factory);
        var html = await client.GetStringAsync("/Notificaciones");
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var originales = await db.Notificaciones.IgnoreQueryFilters().CountAsync();
        for(var i = 0; i < 3; i++)
            foreach(var url in new[] { "/", "/Notificaciones", "/Complementos/Indicadores", "/Complementos/Ayuda" })
                Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url)).StatusCode);
        Assert.Equal(originales, await db.Notificaciones.IgnoreQueryFilters().CountAsync());
        Assert.Equal(20, Regex.Matches(html, "data-notification=").Count);
        var pagina2 = await client.GetStringAsync("/Notificaciones?pagina=2");
        Assert.DoesNotContain("data-notification=\"" + Aviso(html) + "\"", pagina2);
        Assert.Contains("Página 2", WebUtility.HtmlDecode(pagina2));
    }

    [Fact]
    public async Task ReiniciarRequierePostAntiforgeryYConfirmacionYAislaVisitantes()
    {
        using var factory = new DemoFactory(); using var a = Client(factory); using var b = Client(factory);
        var html = await a.GetStringAsync("/Notificaciones"); var aviso = Aviso(html);
        await Post(a, "/Notificaciones/Leer", html, ("id", aviso));
        var otroAntes = await b.GetStringAsync("/Notificaciones");
        var lista = await a.GetStringAsync("/Solicitudes"); var id = Regex.Match(lista, "href=\"/Solicitudes/Detalle/([0-9]+)\"").Groups[1].Value;
        await a.GetAsync("/Solicitudes/Detalle/" + id);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await a.GetAsync("/Complementos/Reiniciar")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsync("/Complementos/Reiniciar", new FormUrlEncodedContent(new Dictionary<string,string> { ["confirmar"] = "true" }))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(a, "/Complementos/Reiniciar", html)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Post(a, "/Complementos/Reiniciar", html, ("confirmar", "true"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync("/Solicitudes/Detalle/" + id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(a, "/Notificaciones/Leer", html, ("id", aviso))).StatusCode);
        var despues = await a.GetStringAsync("/"); Assert.Contains("Demostración reiniciada", WebUtility.HtmlDecode(despues));
        var otroDespues = await b.GetStringAsync("/Notificaciones"); Assert.Equal(Unread(otroAntes), Unread(otroDespues));
        Assert.Equal(Aviso(otroAntes), Aviso(otroDespues));
        Assert.Equal(6, Regex.Matches(await a.GetStringAsync("/Solicitudes"), "<tr><td><strong>CON-").Count);
        var kpi = await a.GetStringAsync("/Complementos/Indicadores"); Assert.Contains("data-kpi=\"total\">6", kpi);
        Assert.Contains("data-kpi=\"finalizadas\">1", kpi);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(18, await db.Solicitudes.IgnoreQueryFilters().CountAsync()); Assert.Equal(4, await db.Asignaturas.CountAsync());
    }
}
