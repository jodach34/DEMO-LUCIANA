using Convalidacion.Web.Data;
using Convalidacion.Web.Models;
using Convalidacion.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Convalidacion.Tests;

public class SolicitudServiceTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private AppDbContext db = null!;
    private SolicitudService service = null!;

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.MigrateAsync();
        await DemoSeeder.SeedAsync(db);
        service = new(db, TimeProvider.System, NullLogger<SolicitudService>.Instance);
    }

    public async Task DisposeAsync() { await db.DisposeAsync(); await connection.DisposeAsync(); }

    private static CrearSolicitudViewModel Valida() => new()
    {
        Estudiante = "Estudiante Demo Prueba", InstitucionOrigen = "Instituto Ficticio", AsignaturaOrigen = "Curso de prueba",
        AsignaturaId = 1, CreditosOrigen = 4, Nota = 15, Coincidencia = 85, Certificado = true, Silabo = true
    };

    private async Task Aplicar(int id, AccionSolicitud accion, PerfilDemo perfil)
    {
        var solicitud = await db.Solicitudes.AsNoTracking().SingleAsync(s => s.Id == id);
        await service.EjecutarAsync(new() { Id = id, Accion = accion, Revision = solicitud.Revision,
            Comentario = "Sustento ficticio para la prueba." }, perfil);
    }

    [Fact]
    public async Task RegistroRevisionAprobacionConservaHistorial()
    {
        var id = await service.CrearAsync(Valida(), PerfilDemo.Estudiante);
        await Aplicar(id, AccionSolicitud.Revisar, PerfilDemo.Secretaria);
        await Aplicar(id, AccionSolicitud.Aprobar, PerfilDemo.Comision);
        var s = await db.Solicitudes.Include(s => s.Historial).SingleAsync(s => s.Id == id);
        Assert.Equal(EstadoSolicitud.Aprobada, s.Estado);
        Assert.Equal(3, s.Historial.Count);
        Assert.Equal(new[] { EstadoSolicitud.Recibida, EstadoSolicitud.EnRevision, EstadoSolicitud.Aprobada },
            s.Historial.OrderBy(h => h.Id).Select(h => h.Estado));
    }

    [Fact]
    public async Task ObservacionYSubsanacionDevuelveASecretaria()
    {
        var id = await service.CrearAsync(Valida(), PerfilDemo.Estudiante);
        await Aplicar(id, AccionSolicitud.Observar, PerfilDemo.Secretaria);
        await Aplicar(id, AccionSolicitud.Subsanar, PerfilDemo.Estudiante);
        Assert.Equal(EstadoSolicitud.Recibida, (await db.Solicitudes.FindAsync(id))!.Estado);
        await Aplicar(id, AccionSolicitud.Revisar, PerfilDemo.Secretaria);
        await Aplicar(id, AccionSolicitud.Observar, PerfilDemo.Comision);
        Assert.Equal(EstadoSolicitud.Observada, (await db.Solicitudes.FindAsync(id))!.Estado);
    }

    [Theory]
    [InlineData(10, 90, 4)]
    [InlineData(15, 79, 4)]
    [InlineData(15, 90, 3)]
    public async Task AprobacionNoPermiteCriteriosInsuficientes(int nota, int coincidencia, int creditos)
    {
        var model = Valida(); model.Nota = nota; model.Coincidencia = coincidencia; model.CreditosOrigen = creditos;
        var id = await service.CrearAsync(model, PerfilDemo.Estudiante);
        await Aplicar(id, AccionSolicitud.Revisar, PerfilDemo.Secretaria);
        await Assert.ThrowsAsync<ReglaProcesoException>(() => Aplicar(id, AccionSolicitud.Aprobar, PerfilDemo.Comision));
        Assert.Equal(2, await db.Seguimientos.CountAsync(h => h.SolicitudId == id));
    }

    [Fact]
    public async Task LimitesMinimosPermitenAprobacion()
    {
        var model = Valida(); model.Nota = 11; model.Coincidencia = 80;
        var id = await service.CrearAsync(model, PerfilDemo.Estudiante);
        await Aplicar(id, AccionSolicitud.Revisar, PerfilDemo.Secretaria);
        await Aplicar(id, AccionSolicitud.Aprobar, PerfilDemo.Comision);
        Assert.Equal(EstadoSolicitud.Aprobada, (await db.Solicitudes.FindAsync(id))!.Estado);
    }

    [Theory]
    [InlineData(PerfilDemo.Estudiante, AccionSolicitud.Aprobar)]
    [InlineData(PerfilDemo.Comision, AccionSolicitud.Aprobar)]
    [InlineData(PerfilDemo.Estudiante, AccionSolicitud.Revisar)]
    public async Task ImpideSaltarEtapasOCambiarDesdePerfilIncorrecto(PerfilDemo perfil, AccionSolicitud accion)
    {
        var id = await service.CrearAsync(Valida(), PerfilDemo.Estudiante);
        await Assert.ThrowsAsync<ReglaProcesoException>(() => Aplicar(id, accion, perfil));
        Assert.Equal(EstadoSolicitud.Recibida, (await db.Solicitudes.FindAsync(id))!.Estado);
    }

    [Theory]
    [InlineData(AccionSolicitud.Aprobar)]
    [InlineData(AccionSolicitud.Rechazar)]
    public async Task DecisionFinalNoSeReabre(AccionSolicitud accion)
    {
        var id = await service.CrearAsync(Valida(), PerfilDemo.Estudiante);
        await Aplicar(id, AccionSolicitud.Revisar, PerfilDemo.Secretaria);
        await Aplicar(id, accion, PerfilDemo.Comision);
        await Assert.ThrowsAsync<ReglaProcesoException>(() => Aplicar(id, AccionSolicitud.Observar, PerfilDemo.Comision));
    }

    [Fact]
    public async Task RevisionAntiguaNoModificaElExpediente()
    {
        var id = await service.CrearAsync(Valida(), PerfilDemo.Estudiante);
        var antigua = (await db.Solicitudes.FindAsync(id))!.Revision;
        await Aplicar(id, AccionSolicitud.Revisar, PerfilDemo.Secretaria);
        await Assert.ThrowsAsync<ReglaProcesoException>(() => service.EjecutarAsync(new()
        { Id = id, Revision = antigua, Accion = AccionSolicitud.Aprobar, Comentario = "Intento desactualizado." }, PerfilDemo.Comision));
        Assert.Equal(EstadoSolicitud.EnRevision, (await db.Solicitudes.FindAsync(id))!.Estado);
    }

    [Fact]
    public async Task ConcurrenciaRealDeDosContextosNoSobrescribe()
    {
        var id = await service.CrearAsync(Valida(), PerfilDemo.Estudiante);
        await using var other = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        var antigua = await other.Solicitudes.FindAsync(id);
        await Aplicar(id, AccionSolicitud.Revisar, PerfilDemo.Secretaria);
        antigua!.Estado = EstadoSolicitud.Observada;
        antigua.Revision = Guid.NewGuid();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => other.SaveChangesAsync());
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task NoRegistraSinDocumentosSimulados(bool certificado, bool silabo)
    {
        var model = Valida(); model.Certificado = certificado; model.Silabo = silabo;
        await Assert.ThrowsAsync<ReglaProcesoException>(() => service.CrearAsync(model, PerfilDemo.Estudiante));
    }

    [Fact]
    public async Task NoPermiteAsignaturaInexistenteNiPerfilDeSecretaria()
    {
        var model = Valida(); model.AsignaturaId = 999;
        await Assert.ThrowsAsync<ReglaProcesoException>(() => service.CrearAsync(model, PerfilDemo.Estudiante));
        await Assert.ThrowsAsync<ReglaProcesoException>(() => service.CrearAsync(Valida(), PerfilDemo.Secretaria));
    }

    [Fact]
    public async Task SeedEsIdempotenteYConservaSolicitudesNuevas()
    {
        var id = await service.CrearAsync(Valida(), PerfilDemo.Estudiante);
        await DemoSeeder.SeedAsync(db);
        Assert.Equal(6, await db.Solicitudes.CountAsync());
        Assert.Equal(4, await db.Asignaturas.CountAsync());
        Assert.NotNull(await db.Solicitudes.FindAsync(id));
        Assert.Equal(2, (await db.Database.GetAppliedMigrationsAsync()).Count());
    }
}
