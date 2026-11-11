using Convalidacion.Web.Data;
using Convalidacion.Web.Models;
using Convalidacion.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;

namespace Convalidacion.Tests;

public class ComplementosTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private AppDbContext db = null!;
    private ComplementosService service = null!;
    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.MigrateAsync(); await DemoSeeder.SeedAsync(db);
        service = new(db, new RelojFijo());
    }
    public async Task DisposeAsync() { await db.DisposeAsync(); await connection.DisposeAsync(); }
    private sealed class RelojFijo : TimeProvider
    { public override DateTimeOffset GetUtcNow() => new(2026, 11, 10, 15, 0, 0, TimeSpan.Zero); }

    [Theory]
    [InlineData(80, 4, false, "referencia general")]
    [InlineData(100, 4, false, "referencia general")]
    [InlineData(70, 4, true, "excepción orientativa")]
    [InlineData(79, 4, false, "No alcanza")]
    [InlineData(69, 4, true, "No alcanza")]
    [InlineData(70, 5, true, "No alcanza")]
    [InlineData(90, 3, true, "insuficientes")]
    public async Task CalculadoraSoloOrientaYNoModificaExpedientes(int porcentaje, int creditos, bool afinidad, string esperado)
    {
        var revisiones = await db.Solicitudes.Select(s => s.Revision).ToListAsync();
        var resultado = await service.CalcularAsync(new() { AsignaturaId = 1, AsignaturaOrigen = "Origen ficticio", Coincidencia = porcentaje, CreditosOrigen = creditos, AfinidadDirecta = afinidad });
        Assert.Contains(esperado, resultado.Mensaje); Assert.Equal(4, resultado.Destino.Creditos);
        Assert.Equal(revisiones, await db.Solicitudes.Select(s => s.Revision).ToListAsync());
    }

    [Theory]
    [InlineData(999, 80, 4, "Curso")]
    [InlineData(1, 101, 4, "Curso")]
    [InlineData(1, -1, 4, "Curso")]
    [InlineData(1, 80, 0, "Curso")]
    [InlineData(1, 80, 4, " ")]
    public async Task CalculadoraValidaServidor(int destino, int porcentaje, int creditos, string origen)
    {
        await Assert.ThrowsAsync<ReglaProcesoException>(() => service.CalcularAsync(new() { AsignaturaId = destino, Coincidencia = porcentaje, CreditosOrigen = creditos, AsignaturaOrigen = origen }));
    }

    [Fact]
    public async Task CatalogoFiltraYComparaEvaluacionRealDelExpediente()
    {
        var finalizado = await db.Solicitudes.SingleAsync(s => s.Estado == EstadoSolicitud.Finalizada);
        var model = await service.EquivalenciasAsync("DEMO-101", 4, finalizado.Id);
        Assert.Equal("Algoritmos y programación", Assert.Single(model.Catalogo).Nombre);
        Assert.Equal(ResultadoAcademico.Aprobada, model.Expediente!.Cursos.Single(c => c.AsignaturaId == 1).Resultado);
        Assert.Empty((await service.EquivalenciasAsync("DEMO-101", 3, null)).Catalogo);
        await Assert.ThrowsAsync<ReglaProcesoException>(() => service.EquivalenciasAsync(null, null, 9999));
    }

    [Fact]
    public async Task IndicadoresDeSeisEjemplosSonCalculadosConDenominadoresExplicitos()
    {
        var kpi = await service.IndicadoresAsync();
        Assert.Equal(6, kpi.Total); Assert.Equal(1, kpi.Finalizadas); Assert.Equal(1, kpi.FinalizadasConFechas);
        Assert.Equal(1, kpi.Observadas); Assert.Equal(100m / 6, kpi.TasaObservaciones);
        Assert.Equal(3, kpi.Aprobadas); Assert.Equal(6, kpi.Decididas); Assert.Equal(50m, kpi.TasaConvalidacion);
        Assert.Equal(new[] { 2, 1, 1, 2 }, kpi.PorEtapa.OrderBy(p => p.Key).Select(p => p.Value));
        Assert.All(kpi.PorResponsable.Values, n => Assert.Equal(1, n));
        var finalizada = await db.Solicitudes.Include(s => s.Historial).SingleAsync(s => s.Estado == EstadoSolicitud.Finalizada);
        var fin = finalizada.Historial.Single(h => h.Accion == AccionSolicitud.Finalizar).FechaUtc;
        Assert.Equal((fin - finalizada.CreadaUtc).TotalHours, kpi.PromedioHoras);
    }

    [Fact]
    public async Task ObservacionesRepetidasResueltasYDevolucionesSeCuentanUnaVez()
    {
        var s = await db.Solicitudes.Include(s => s.Historial).SingleAsync(s => s.Estado == EstadoSolicitud.PendienteSubsanacion);
        s.Estado = EstadoSolicitud.Subsanada;
        s.Historial.Add(new() { Estado = EstadoSolicitud.Observada, Accion = AccionSolicitud.ObservarDocumentos, FechaUtc = s.ActualizadaUtc, Comentario = "Otra observación ficticia" });
        s.Historial.Add(new() { Estado = EstadoSolicitud.EvaluacionAcademica, Accion = AccionSolicitud.DevolverDictamen, FechaUtc = s.ActualizadaUtc, Comentario = "Devolución ficticia" });
        await db.SaveChangesAsync();
        Assert.Equal(1, (await service.IndicadoresAsync()).Observadas);
        var curso = await db.Cursos.FirstAsync(c => c.SolicitudId != s.Id);
        curso.Evaluaciones.Add(new() { Resultado = ResultadoAcademico.Observada, FechaUtc = s.ActualizadaUtc });
        await db.SaveChangesAsync();
        Assert.Equal(2, (await service.IndicadoresAsync()).Observadas);
    }

    [Fact]
    public async Task PromedioExcluyeFechasInvalidasYUsaFinalizacionEnVezDeUltimaActualizacion()
    {
        var s = await db.Solicitudes.Include(s => s.Historial).SingleAsync(s => s.Estado == EstadoSolicitud.Finalizada);
        var fin = s.Historial.Single(h => h.Accion == AccionSolicitud.Finalizar);
        fin.FechaUtc = s.CreadaUtc.AddHours(8); s.ActualizadaUtc = s.CreadaUtc.AddDays(2);
        await db.SaveChangesAsync(); Assert.Equal(8d, (await service.IndicadoresAsync()).PromedioHoras);
        fin.FechaUtc = s.CreadaUtc.AddMinutes(-1); await db.SaveChangesAsync(); Assert.Null((await service.IndicadoresAsync()).PromedioHoras);
        fin.FechaUtc = new DateTime(2027, 1, 1); await db.SaveChangesAsync(); Assert.Null((await service.IndicadoresAsync()).PromedioHoras);
        db.Seguimientos.Remove(fin); await db.SaveChangesAsync(); Assert.Null((await service.IndicadoresAsync()).PromedioHoras);
    }

    [Fact]
    public async Task SinExpedientesNiDecisionesNoInventaPorcentajes()
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var vacio = new AppDbContext(opciones, Accesor("sesion-vacia"));
        var kpi = await new ComplementosService(vacio, new RelojFijo()).IndicadoresAsync();
        Assert.Equal(0, kpi.Total); Assert.Null(kpi.TasaObservaciones); Assert.Null(kpi.TasaConvalidacion); Assert.Null(kpi.PromedioHoras);
        var cursos = await db.Cursos.ToListAsync();
        foreach(var c in cursos) c.Resultado = ResultadoAcademico.Observada;
        await db.SaveChangesAsync(); Assert.Null((await service.IndicadoresAsync()).TasaConvalidacion);
    }

    [Fact]
    public async Task EventosTienenClaveUnicaYSeGuardanConLaTransicion()
    {
        var s = await db.Solicitudes.Include(s => s.Notificaciones).SingleAsync(s => s.Estado == EstadoSolicitud.Presentada);
        var antes = s.Notificaciones.Count;
        var proceso = new SolicitudService(db, new RelojFijo(), NullLogger<SolicitudService>.Instance);
        var accion = new AccionSolicitudViewModel { Id = s.Id, Revision = s.Revision, Accion = AccionSolicitud.Recibir, Comentario = "Recepción de prueba ficticia" };
        await proceso.EjecutarAsync(accion, PerfilDemo.Admision);
        Assert.Equal(antes + 1, s.Notificaciones.Count);
        await Assert.ThrowsAsync<ReglaProcesoException>(() => proceso.EjecutarAsync(accion, PerfilDemo.Admision));
        Assert.Equal(antes + 1, await db.Notificaciones.CountAsync(n => n.SolicitudId == s.Id));
        var aviso = s.Notificaciones.Last();
        db.Notificaciones.Add(new() { SolicitudId = s.Id, EventoId = aviso.EventoId, Destinatario = aviso.Destinatario, FechaUtc = aviso.FechaUtc });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task ReinicioEsAtomicoMantieneCatalogoYAislaOtrosAmbitos()
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var sesion = new AppDbContext(opciones, Accesor("mi-sesion"));
        await DemoSeeder.SeedAsync(sesion);
        var original = await sesion.Solicitudes.OrderBy(s => s.Id).FirstAsync(); var id = original.Id;
        original.Estudiante = "Cambio ficticio del visitante"; await sesion.SaveChangesAsync();
        var avisosAj = await db.Notificaciones.AsNoTracking().OrderBy(n => n.Id).Select(n => new { n.Id, n.Mensaje, n.LeidaUtc }).ToListAsync();
        var otros = await db.Solicitudes.AsNoTracking().OrderBy(s => s.Id).Select(s => new { s.Id, s.Revision }).ToListAsync();
        var reinicio = new ComplementosService(sesion, new RelojFijo());
        await reinicio.ReiniciarAsync();
        Assert.Equal(6, await sesion.Solicitudes.CountAsync()); Assert.Equal(4, await sesion.Asignaturas.CountAsync());
        Assert.False(await sesion.Solicitudes.AnyAsync(s => s.Id == id));
        Assert.False(await sesion.Solicitudes.AnyAsync(s => s.Estudiante == "Cambio ficticio del visitante"));
        Assert.Equal(otros, await db.Solicitudes.AsNoTracking().OrderBy(s => s.Id).Select(s => new { s.Id, s.Revision }).ToListAsync());
        Assert.Equal(avisosAj, await db.Notificaciones.AsNoTracking().OrderBy(n => n.Id).Select(n => new { n.Id, n.Mensaje, n.LeidaUtc }).ToListAsync());
        Assert.All(await sesion.Notificaciones.ToListAsync(), n => Assert.Null(n.LeidaUtc));
        Assert.Equal(6, (await reinicio.IndicadoresAsync()).Total);
        await using var fk = connection.CreateCommand(); fk.CommandText = "PRAGMA foreign_key_check";
        await using var reader = await fk.ExecuteReaderAsync(); Assert.False(await reader.ReadAsync());
    }

    [Fact]
    public async Task ErrorDuranteReinicioRevierteTodoElBorrado()
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var sesion = new AppDbContext(opciones, Accesor("sesion-fallo"));
        await DemoSeeder.SeedAsync(sesion);
        // El catálogo insuficiente fuerza un fallo después del borrado, antes del commit.
        // Se conserva el primer destino requerido por los expedientes originales.
        await sesion.Cursos.Where(c => c.AsignaturaId != 1).ExecuteDeleteAsync();
        await db.Cursos.Where(c => c.AsignaturaId != 1).ExecuteDeleteAsync();
        await sesion.Asignaturas.Where(a => a.Id != 1).ExecuteDeleteAsync();
        var anteriores = await sesion.Solicitudes.AsNoTracking().Select(s => s.Id).ToListAsync();
        var avisos = await sesion.Notificaciones.CountAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ComplementosService(sesion, new RelojFijo()).ReiniciarAsync());
        Assert.Equal(anteriores, await sesion.Solicitudes.AsNoTracking().Select(s => s.Id).ToListAsync());
        Assert.Equal(avisos, await sesion.Notificaciones.CountAsync());
        Assert.Equal(2, await sesion.Resoluciones.CountAsync());
    }

    [Fact]
    public async Task ReinicioSinSesionNoPuedeBorrarDatosHistoricos()
    {
        await Assert.ThrowsAsync<ReglaProcesoException>(() => service.ReiniciarAsync());
        Assert.Equal(6, await db.Solicitudes.CountAsync());
    }

    [Fact]
    public async Task MigracionDeAvisosConservaTodosLosDatosDelBloque2()
    {
        await using var anterior = new SqliteConnection("Data Source=:memory:"); await anterior.OpenAsync();
        await using var contexto = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(anterior).Options);
        var migrator = contexto.GetService<IMigrator>();
        await migrator.MigrateAsync("20261110034722_FlujoConvalidacion");
        await contexto.Database.ExecuteSqlRawAsync("""
            INSERT INTO Asignaturas (Id,Codigo,Nombre,Creditos) VALUES (1,'LEGADO','Curso ficticio',4);
            INSERT INTO Solicitudes (Id,AmbitoDemo,Estudiante,InstitucionOrigen,AsignaturaOrigen,CreditosOrigen,Nota,Coincidencia,AsignaturaId,Estado,CreadaUtc,ActualizadaUtc,Revision,CertificadoDocumento,VersionProceso)
            VALUES (1,'','Alumno ficticio','Instituto ficticio','Origen',4,16,85,1,17,'2026-11-01','2026-11-02','11111111-1111-1111-1111-111111111111','CERT-DEMO-01',2);
            INSERT INTO Notificaciones (Id,SolicitudId,Destinatario,FechaUtc,Mensaje) VALUES (1,1,0,'2026-11-02','Aviso original de resolución'),(2,1,4,'2026-11-02','Aviso original para registro');
            """);
        await migrator.MigrateAsync();
        var avisos = await contexto.Notificaciones.OrderBy(n => n.Id).ToListAsync();
        Assert.Equal(new[] { "Aviso original de resolución", "Aviso original para registro" }, avisos.Select(n => n.Mensaje));
        Assert.All(avisos, n => { Assert.Equal(AccionSolicitud.Notificar, n.Tipo); Assert.Null(n.LeidaUtc); Assert.Equal(1, n.SolicitudId); Assert.Equal(new DateTime(2026, 11, 2), n.FechaUtc); });
        Assert.Equal("Alumno ficticio", (await contexto.Solicitudes.SingleAsync()).Estudiante);
        Assert.False(contexto.Database.HasPendingModelChanges());
    }

    private static HttpContextAccessor Accesor(string ambito)
    {
        var session = new SesionPrueba(); session.SetString("AmbitoDemo", ambito);
        return new HttpContextAccessor { HttpContext = new DefaultHttpContext { Session = session } };
    }
    private sealed class SesionPrueba : ISession
    {
        private readonly Dictionary<string, byte[]> values = [];
        public bool IsAvailable => true;
        public string Id => "prueba";
        public IEnumerable<string> Keys => values.Keys;
        public void Clear() => values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => values.Remove(key);
        public void Set(string key, byte[] value) => values[key] = value;
        public bool TryGetValue(string key, out byte[] value) => values.TryGetValue(key, out value!);
    }
}
