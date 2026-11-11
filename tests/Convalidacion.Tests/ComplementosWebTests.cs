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
    public async Task CincoPerfilesNaveganHerramientasSinCambiarSuPerspectiva(string perfil)
    {
        using var factory = new DemoFactory(); using var client = Client(factory);
        var html = await client.GetStringAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, (await Post(client, "/Perfiles/Cambiar", html, ("perfil", perfil))).StatusCode);
        foreach(var path in new[] { "/Complementos/Calculadora", "/Complementos/Equivalencias", "/Complementos/Indicadores", "/Complementos/Proceso", "/Complementos/Ayuda", "/Notificaciones" })
        {
            var response = await client.GetAsync(path); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            html = await response.Content.ReadAsStringAsync();
            Assert.Contains("value=\"" + perfil + "\" selected=\"selected\"", html);
            Assert.Contains("/images/usmp-logo.jpg", html); Assert.Contains("name=\"viewport\"", html);
            Assert.Contains("Reiniciar demostraci", WebUtility.HtmlDecode(html));
            Assert.DoesNotContain("Secretaría", WebUtility.HtmlDecode(html)); Assert.DoesNotContain("Comisión académica", WebUtility.HtmlDecode(html));
        }
    }

    [Fact]
    public async Task CalculadoraValidaYExplicaResultadoSinCambiarDatos()
    {
        using var factory = new DemoFactory(); using var client = Client(factory);
        var html = await client.GetStringAsync("/Complementos/Calculadora");
        var response = await Post(client, "/Complementos/Calculadora", html, ("AsignaturaId", "1"), ("AsignaturaOrigen", "Curso ficticio"), ("CreditosOrigen", "4"), ("Coincidencia", "70"), ("AfinidadDirecta", "true"));
        html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("excepción orientativa", html); Assert.Contains("No constituye aprobación", html);
        Assert.Contains("Director del Departamento Académico", html);
        response = await Post(client, "/Complementos/Calculadora", html, ("AsignaturaId", "999"), ("AsignaturaOrigen", "Curso ficticio"), ("CreditosOrigen", "4"), ("Coincidencia", "80"));
        Assert.Contains("validation-summary-errors", await response.Content.ReadAsStringAsync());
        response = await Post(client, "/Complementos/Calculadora", html, ("AsignaturaId", "1"), ("AsignaturaOrigen", "Curso ficticio"), ("CreditosOrigen", "4"), ("Coincidencia", "101"));
        Assert.Contains("validation-summary-errors", await response.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        Assert.Equal(12, await scope.ServiceProvider.GetRequiredService<AppDbContext>().Solicitudes.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task EquivalenciasFiltraYNoExponeExpedienteAjeno()
    {
        using var factory = new DemoFactory(); using var a = Client(factory); using var b = Client(factory);
        var html = await a.GetStringAsync("/Complementos/Equivalencias?buscar=DEMO-103&creditos=3");
        Assert.Single(Regex.Matches(html, "data-catalog-course"));
        Assert.Contains("DEMO-103", html); Assert.DoesNotContain("<strong>DEMO-101", html);
        var lista = await a.GetStringAsync("/Solicitudes?estado=Finalizada");
        var id = Regex.Match(lista, "href=\"/Solicitudes/Detalle/([0-9]+)\"").Groups[1].Value;
        html = await a.GetStringAsync("/Complementos/Equivalencias?expedienteId=" + id);
        Assert.Contains("result-Aprobada", html); Assert.Contains("result-Rechazada", html);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync("/Complementos/Equivalencias?expedienteId=" + id)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.GetAsync("/Complementos/Equivalencias?creditos=abc")).StatusCode);
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
            foreach(var url in new[] { "/", "/Notificaciones", "/Complementos/Indicadores", "/Complementos/Equivalencias" })
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
