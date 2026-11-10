using System.Net;
using System.Text.RegularExpressions;
using Convalidacion.Web.Data;
using Convalidacion.Web.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Convalidacion.Tests;

public class DemoFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    public DemoFactory() => connection.Open();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
        });
    }
    protected override void Dispose(bool disposing) { base.Dispose(disposing); if (disposing) connection.Dispose(); }
}

public class WebTests
{
    private static HttpClient Client(DemoFactory factory) => factory.CreateClient(new() { AllowAutoRedirect = false });
    private static string Input(string html, string name)
    {
        var match = Regex.Match(html, "name=\"" + name + "\"[^>]*value=\"([^\"]*)\"");
        Assert.True(match.Success, $"No se encontró el campo {name}.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
    private static async Task<HttpResponseMessage> Post(HttpClient client, string url, string html, params (string, string)[] values)
    {
        var fields = values.ToDictionary(v => v.Item1, v => v.Item2);
        fields["__RequestVerificationToken"] = Input(html, "__RequestVerificationToken");
        return await client.PostAsync(url, new FormUrlEncodedContent(fields));
    }

    private static string DetalleUrl(string html)
    {
        var match = Regex.Match(html, "href=\"(/Solicitudes/Detalle/[0-9]+)\"");
        Assert.True(match.Success, "La bandeja no contiene expedientes de ejemplo.");
        return match.Groups[1].Value;
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Solicitudes")]
    [InlineData("/Solicitudes/Crear")]
    [InlineData("/Home/Privacy")]
    [InlineData("/health")]
    [InlineData("/css/site.css")]
    [InlineData("/lib/bootstrap/dist/css/bootstrap.min.css")]
    public async Task PaginasYRecursosResponden(string path)
    {
        using var factory = new DemoFactory(); using var client = Client(factory);
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RegistroRevisionYDecisionPorHttp()
    {
        using var factory = new DemoFactory(); using var client = Client(factory);
        var html = await client.GetStringAsync("/Solicitudes/Crear");
        var crear = await Post(client, "/Solicitudes/Crear", html, ("Estudiante", "Estudiante Demo HTTP"),
            ("InstitucionOrigen", "Instituto de Prueba Ficticio"), ("AsignaturaOrigen", "Programación ficticia"),
            ("AsignaturaId", "1"), ("CreditosOrigen", "4"), ("Nota", "16"), ("Coincidencia", "90"),
            ("Certificado", "true"), ("Silabo", "true"));
        Assert.Equal(HttpStatusCode.Redirect, crear.StatusCode);
        var location = crear.Headers.Location!.ToString();
        foreach (var step in new[] { ("Secretaria", "Revisar"), ("Comision", "Aprobar") })
        {
            html = await client.GetStringAsync(location);
            Assert.Equal(HttpStatusCode.Redirect, (await Post(client, "/Perfiles/Cambiar", html, ("perfil", step.Item1))).StatusCode);
            html = await client.GetStringAsync(location);
            var ejecutar = await Post(client, "/Solicitudes/Ejecutar", html, ("Id", Input(html, "Id")),
                ("Revision", Input(html, "Revision")), ("Accion", step.Item2), ("Comentario", "Sustento académico ficticio de prueba."));
            Assert.Equal(HttpStatusCode.Redirect, ejecutar.StatusCode);
        }
        html = await client.GetStringAsync(location);
        Assert.Contains("status-Aprobada", html);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var solicitud = await db.Solicitudes.IgnoreQueryFilters().Include(s => s.Historial).SingleAsync(s => s.Estudiante == "Estudiante Demo HTTP");
        Assert.Equal(EstadoSolicitud.Aprobada, solicitud.Estado);
        Assert.Equal(3, solicitud.Historial.Count);
    }

    [Fact]
    public async Task ServidorValidaFormularioAunqueNoHayaJavascript()
    {
        using var factory = new DemoFactory(); using var client = Client(factory);
        var html = await client.GetStringAsync("/Solicitudes/Crear");
        var response = await Post(client, "/Solicitudes/Crear", html, ("Nota", "99"), ("AsignaturaId", "0"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("validation-summary-errors", await response.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        Assert.Equal(5, await scope.ServiceProvider.GetRequiredService<AppDbContext>().Solicitudes.CountAsync());
    }

    [Fact]
    public async Task PeticionSinAntiforgerySeRechaza()
    {
        using var factory = new DemoFactory(); using var client = Client(factory);
        var response = await client.PostAsync("/Perfiles/Cambiar", new FormUrlEncodedContent(new Dictionary<string, string> { ["perfil"] = "Comision" }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PerfilesNoPermitenRedireccionExternaNiEnumInvalido()
    {
        using var factory = new DemoFactory(); using var client = Client(factory);
        var html = await client.GetStringAsync("/");
        var response = await Post(client, "/Perfiles/Cambiar", html, ("perfil", "Secretaria"), ("returnUrl", "https://example.com"));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location!.ToString());
        html = await client.GetStringAsync("/");
        response = await Post(client, "/Perfiles/Cambiar", html, ("perfil", "999"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task FiltrosYErroresMantienenEstadoHttp()
    {
        using var factory = new DemoFactory(); using var client = Client(factory);
        var html = await client.GetStringAsync("/Solicitudes?estado=Aprobada");
        Assert.Contains("status-Aprobada", html);
        Assert.DoesNotContain("status-Rechazada", html);
        html = await client.GetStringAsync("/Solicitudes?buscar=NINGUNA_COINCIDENCIA");
        Assert.Contains("No hay solicitudes", html);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/Solicitudes/Detalle/99999")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/ruta-inexistente")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/Solicitudes?estado=999")).StatusCode);
    }

    [Fact]
    public async Task EjemplosPersistenEnLaMismaSesion()
    {
        using var factory = new DemoFactory(); using var client = Client(factory);
        var html = await client.GetStringAsync("/Solicitudes");
        var url = DetalleUrl(html);
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(url, DetalleUrl(await client.GetStringAsync("/Solicitudes")));
    }

    [Fact]
    public async Task DosNavegadoresNoCompartenSolicitudesComentariosNiPermitenCambiosCruzados()
    {
        using var factory = new DemoFactory(); using var first = Client(factory); using var second = Client(factory);
        var html = await first.GetStringAsync("/Solicitudes/Crear");
        var crear = await Post(first, "/Solicitudes/Crear", html, ("Estudiante", "Estudiante Demo Visitante A"),
            ("InstitucionOrigen", "Instituto Ficticio A"), ("AsignaturaOrigen", "Curso ficticio A"),
            ("AsignaturaId", "1"), ("CreditosOrigen", "4"), ("Nota", "16"), ("Coincidencia", "90"),
            ("Certificado", "true"), ("Silabo", "true"));
        Assert.Equal(HttpStatusCode.Redirect, crear.StatusCode);
        var location = crear.Headers.Location!.ToString();
        html = await first.GetStringAsync(location);
        await Post(first, "/Perfiles/Cambiar", html, ("perfil", "Secretaria"));
        html = await first.GetStringAsync(location);
        await Post(first, "/Solicitudes/Ejecutar", html, ("Id", Input(html, "Id")), ("Revision", Input(html, "Revision")),
            ("Accion", "Observar"), ("Comentario", "Comentario exclusivo del visitante A."));
        html = await first.GetStringAsync(location);
        Assert.Contains("Comentario exclusivo del visitante A.", html);
        await Post(first, "/Perfiles/Cambiar", html, ("perfil", "Estudiante"));
        html = await first.GetStringAsync(location);
        var revision = Input(html, "Revision");
        var id = Input(html, "Id");
        var other = await second.GetStringAsync("/Solicitudes");
        Assert.DoesNotContain("Estudiante Demo Visitante A", other);
        Assert.DoesNotContain("Comentario exclusivo del visitante A.", other);
        Assert.NotEqual(location, DetalleUrl(other));
        Assert.Equal(HttpStatusCode.NotFound, (await second.GetAsync(location)).StatusCode);
        // Incluso conociendo el identificador y revisión, el ámbito del servidor impide modificarlo.
        var intento = await Post(second, "/Solicitudes/Ejecutar", other, ("Id", id), ("Revision", revision),
            ("Accion", "Subsanar"), ("Comentario", "Intento cruzado del visitante B."));
        Assert.Equal(HttpStatusCode.NotFound, intento.StatusCode);
        html = await first.GetStringAsync(location);
        Assert.Contains("status-Observada", html);
        Assert.DoesNotContain("Intento cruzado del visitante B.", html);
    }
}
