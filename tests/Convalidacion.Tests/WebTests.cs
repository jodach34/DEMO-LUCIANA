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
        var match = Regex.Match(html, "name=\"" + Regex.Escape(name) + "\"[^>]*value=\"([^\"]*)\"");
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
    [InlineData("/js/site.js")]
    [InlineData("/images/usmp-logo.jpg")]
    [InlineData("/images/icons.svg")]
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
            ("InstitucionOrigen", "Instituto de Prueba Ficticio"), ("CertificadoDocumento", "CERT-DEMO-01"),
            ("Cursos[0].AsignaturaOrigen", "Programación ficticia"), ("Cursos[0].AsignaturaId", "1"), ("Cursos[0].CreditosOrigen", "4"), ("Cursos[0].Nota", "16"), ("Cursos[0].SilaboDocumento", "SIL-DEMO-01"),
            ("Cursos[1].AsignaturaOrigen", "Datos ficticios"), ("Cursos[1].AsignaturaId", "2"), ("Cursos[1].CreditosOrigen", "4"), ("Cursos[1].Nota", "16"), ("Cursos[1].SilaboDocumento", "SIL-DEMO-01"));
        Assert.Equal(HttpStatusCode.Redirect, crear.StatusCode);
        var location = crear.Headers.Location!.ToString();
        foreach (var step in new[] { ("Admision", "Recibir"), ("Admision", "DerivarDepartamento") })
        {
            html = await client.GetStringAsync(location);
            Assert.Equal(HttpStatusCode.Redirect, (await Post(client, "/Perfiles/Cambiar", html, ("perfil", step.Item1))).StatusCode);
            html = await client.GetStringAsync(location);
            var ejecutar = await Post(client, "/Solicitudes/Ejecutar", html, ("Id", Input(html, "Id")),
                ("Revision", Input(html, "Revision")), ("Accion", step.Item2), ("Comentario", "Sustento académico ficticio de prueba."), ("DocumentosVerificados", "true"));
            Assert.Equal(HttpStatusCode.Redirect, ejecutar.StatusCode);
        }
        html = await client.GetStringAsync(location);
        await Post(client, "/Perfiles/Cambiar", html, ("perfil", "Director"));
        html = await client.GetStringAsync(location);
        var cursos = Regex.Matches(html, "name=\"AsignaturaSolicitudId\"[^>]*value=\"([0-9]+)\"").Select(m => m.Groups[1].Value).ToArray();
        Assert.Equal(2, cursos.Length);
        for(var i = 0; i < cursos.Length; i++)
        {
            html = await client.GetStringAsync(location);
            var evaluar = await Post(client, "/Solicitudes/Evaluar", html, ("Id", Input(html, "Id")), ("Revision", Input(html, "Revision")),
                ("AsignaturaSolicitudId", cursos[i]), ("Coincidencia", i == 0 ? "85" : "60"), ("Resultado", i == 0 ? "Aprobada" : "Rechazada"), ("Justificacion", "Evaluación individual ficticia desde HTTP."));
            Assert.Equal(HttpStatusCode.Redirect, evaluar.StatusCode);
        }
        foreach(var step in new[] { ("Director","EmitirDictamen"), ("Director","RemitirDecano"), ("Decano","AprobarDictamen"),
            ("Decano","EmitirResolucion"), ("Decano","Notificar"), ("Decano","RemitirRegistro"), ("RegistrosAcademicos","RegistrarAsignaturas"), ("RegistrosAcademicos","Finalizar") })
        {
            html = await client.GetStringAsync(location);
            var cambio = await Post(client, "/Perfiles/Cambiar", html, ("perfil",step.Item1)); Assert.Equal("/", cambio.Headers.Location!.ToString());
            html = await client.GetStringAsync(location);
            var response = await Post(client,"/Solicitudes/Ejecutar",html,("Id",Input(html,"Id")),("Revision",Input(html,"Revision")),("Accion",step.Item2),
                ("Comentario","Sustento del flujo completo ficticio por HTTP."),("NumeroResolucion","DEMO-RD-HTTP-001"),("FechaResolucion","2026-11-09"));
            Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);
        }
        html = await client.GetStringAsync(location);
        Assert.Contains("status-Finalizada", html);
        var id = location.Split('/').Last();
        var resolucion = await client.GetStringAsync("/Solicitudes/Resolucion/" + id);
        Assert.Contains("DOCUMENTO ACAD", resolucion); Assert.Contains("SIN VALIDEZ OFICIAL", resolucion); Assert.Contains("DEMO-RD-HTTP-001", resolucion);
        Assert.Contains("result-Aprobada",resolucion); Assert.Contains("result-Rechazada",resolucion);
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/Solicitudes/Dictamen/" + id)).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var solicitud = await db.Solicitudes.IgnoreQueryFilters().Include(s => s.Historial).SingleAsync(s => s.Estudiante == "Estudiante Demo HTTP");
        Assert.Equal(EstadoSolicitud.Finalizada, solicitud.Estado);
        Assert.Equal(13, solicitud.Historial.Count);
        Assert.Single(await db.Registros.IgnoreQueryFilters().Where(r=>r.AsignaturaSolicitud.SolicitudId==solicitud.Id).ToListAsync());
        Assert.Equal(2,await db.Notificaciones.IgnoreQueryFilters().CountAsync(n=>n.SolicitudId==solicitud.Id));
    }

    [Fact]
    public async Task ServidorValidaFormularioAunqueNoHayaJavascript()
    {
        using var factory = new DemoFactory(); using var client = Client(factory);
        var html = await client.GetStringAsync("/Solicitudes/Crear");
        var response = await Post(client, "/Solicitudes/Crear", html, ("Cursos[0].Nota", "99"), ("Cursos[0].AsignaturaId", "0"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("validation-summary-errors", await response.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        Assert.Equal(6, await scope.ServiceProvider.GetRequiredService<AppDbContext>().Solicitudes.CountAsync());
    }

    [Fact]
    public async Task PeticionSinAntiforgerySeRechaza()
    {
        using var factory = new DemoFactory(); using var client = Client(factory);
        var response = await client.PostAsync("/Perfiles/Cambiar", new FormUrlEncodedContent(new Dictionary<string, string> { ["perfil"] = "Director" }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PerfilesNoPermitenRedireccionExternaNiEnumInvalido()
    {
        using var factory = new DemoFactory(); using var client = Client(factory);
        var html = await client.GetStringAsync("/");
        var response = await Post(client, "/Perfiles/Cambiar", html, ("perfil", "Admision"), ("returnUrl", "https://example.com"));
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
        var html = await client.GetStringAsync("/Solicitudes?estado=Finalizada");
        Assert.Contains("status-Finalizada", html);
        Assert.DoesNotContain("status-Observada", html);
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
            ("InstitucionOrigen", "Instituto Ficticio A"), ("CertificadoDocumento", "CERT-DEMO-01"),
            ("Cursos[0].AsignaturaOrigen", "Curso ficticio A"), ("Cursos[0].AsignaturaId", "1"), ("Cursos[0].CreditosOrigen", "4"), ("Cursos[0].Nota", "16"), ("Cursos[0].SilaboDocumento", "SIL-DEMO-01"));
        Assert.Equal(HttpStatusCode.Redirect, crear.StatusCode);
        var location = crear.Headers.Location!.ToString();
        html = await first.GetStringAsync(location);
        await Post(first, "/Perfiles/Cambiar", html, ("perfil", "Admision"));
        html = await first.GetStringAsync(location);
        await Post(first, "/Solicitudes/Ejecutar", html, ("Id", Input(html, "Id")), ("Revision", Input(html, "Revision")), ("Accion", "Recibir"), ("Comentario", "Recepción documental ficticia del visitante A."));
        html = await first.GetStringAsync(location);
        await Post(first, "/Solicitudes/Ejecutar", html, ("Id", Input(html, "Id")), ("Revision", Input(html, "Revision")),
            ("Accion", "ObservarDocumentos"), ("Comentario", "Comentario exclusivo del visitante A."));
        html = await first.GetStringAsync(location);
        Assert.Contains("Comentario exclusivo del visitante A.", html);
        await Post(first, "/Solicitudes/Ejecutar", html, ("Id", Input(html, "Id")), ("Revision", Input(html, "Revision")), ("Accion", "DevolverEstudiante"), ("Comentario", "Se devuelve el expediente ficticio para corregir documentos."));
        html = await first.GetStringAsync(location);
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
        Assert.Contains("status-PendienteSubsanacion", html);
        Assert.DoesNotContain("Intento cruzado del visitante B.", html);
    }

    [Fact]
    public async Task CincoPerfilesAbrenSuPanelYConservanExpedienteActivo()
    {
        using var factory = new DemoFactory(); using var client = Client(factory);
        var lista = await client.GetStringAsync("/Solicitudes"); var location=DetalleUrl(lista);
        var html=await client.GetStringAsync(location); var id=location.Split('/').Last();
        Assert.Equal(5,Enum.GetValues<PerfilDemo>().Length);
        foreach(var perfil in Enum.GetValues<PerfilDemo>())
        {
            var response=await Post(client,"/Perfiles/Cambiar",html,("perfil",perfil.ToString()),("returnUrl",location));
            Assert.Equal("/",response.Headers.Location!.ToString());
            html=await client.GetStringAsync("/");
            Assert.Contains("value=\""+perfil+"\" selected=\"selected\"",html);
            Assert.Contains("href=\"/Solicitudes/Detalle/"+id+"\"",html);
            Assert.DoesNotContain(">Cambiar</button>",html);
            Assert.Equal(HttpStatusCode.OK,(await client.GetAsync(location)).StatusCode);
        }
    }

    [Fact]
    public async Task DictamenResolucionYNotificacionesNoSeCompartenEntreSesiones()
    {
        using var factory = new DemoFactory(); using var first=Client(factory); using var second=Client(factory);
        var propios=await first.GetStringAsync("/Solicitudes?conResolucion=true"); var url=DetalleUrl(propios); var id=url.Split('/').Last();
        var resolucion=await first.GetStringAsync("/Solicitudes/Resolucion/"+id);
        Assert.Equal(HttpStatusCode.OK,(await first.GetAsync("/Solicitudes/Dictamen/"+id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await second.GetAsync("/Solicitudes/Resolucion/"+id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await second.GetAsync("/Solicitudes/Dictamen/"+id)).StatusCode);
        var numero=Regex.Match(resolucion,@"DEMO-RD-[0-9]+").Value; Assert.NotEmpty(numero);
        var otro=await second.GetStringAsync("/"); Assert.DoesNotContain(numero,otro);
        var html=await first.GetStringAsync(url);
        var cambio=await Post(first,"/Perfiles/Cambiar",html,("perfil","RegistrosAcademicos")); Assert.Equal(HttpStatusCode.Redirect,cambio.StatusCode);
        html=await first.GetStringAsync("/"); Assert.Contains(numero,html);
    }
}
