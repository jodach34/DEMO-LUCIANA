using Convalidacion.Web.Data;
using Convalidacion.Web.Models;
using Convalidacion.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;

namespace Convalidacion.Tests;

public class MigrationTests
{
    [Fact]
    public async Task MigracionAditivaConservaDatosAutoriasYAmbitosAnteriores()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(options);
        var migrator = db.GetService<IMigrator>(); await migrator.MigrateAsync("20261110013226_IsolateDemoSessions");
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO Asignaturas (Id, Codigo, Nombre, Creditos) VALUES (100, 'HIST-100', 'Curso histórico ficticio', 4);
            INSERT INTO Solicitudes (Id, AmbitoDemo, Estudiante, InstitucionOrigen, AsignaturaOrigen, CreditosOrigen,
                Nota, Coincidencia, AsignaturaId, Estado, CreadaUtc, ActualizadaUtc, Revision)
            VALUES (100, '', 'Alumno histórico', 'Institución histórica', 'Origen histórico', 4, 16, 85, 100, 3,
                '2026-11-01 15:00:00', '2026-11-01 17:00:00', '11111111-1111-1111-1111-111111111111'),
                (101, 'otro-ambito', 'Alumno aislado', 'Institución aislada', 'Origen aislado', 3, 15, 60, 100, 4,
                '2026-11-01 15:00:00', '2026-11-01 17:00:00', '22222222-2222-2222-2222-222222222222');
            INSERT INTO Seguimientos (Id, SolicitudId, Estado, Perfil, FechaUtc, Comentario)
            VALUES (100, 100, 1, 1, '2026-11-01 16:00:00', 'Sustento original de Secretaría'),
                (101, 100, 3, 2, '2026-11-01 17:00:00', 'Decisión original de Comisión'),
                (102, 101, 4, 2, '2026-11-01 17:00:00', 'Decisión del otro ámbito');
            """);
        async Task<List<string>> Originales()
        {
            var result = new List<string>();
            foreach(var sql in new[] { "SELECT Id,AmbitoDemo,Estudiante,InstitucionOrigen,AsignaturaOrigen,CreditosOrigen,Nota,Coincidencia,AsignaturaId,Estado,CreadaUtc,ActualizadaUtc,Revision FROM Solicitudes ORDER BY Id",
                "SELECT Id,SolicitudId,Estado,Perfil,FechaUtc,Comentario FROM Seguimientos ORDER BY Id" })
            {
                await using var command = connection.CreateCommand(); command.CommandText = sql;
                await using var reader = await command.ExecuteReaderAsync();
                while(await reader.ReadAsync()) result.Add(string.Join('|', Enumerable.Range(0, reader.FieldCount).Select(reader.GetValue)));
            }
            return result;
        }
        var before = await Originales(); await migrator.MigrateAsync(); Assert.Equal(before, await Originales());
        Assert.False(db.Database.HasPendingModelChanges());
        var s = await db.Solicitudes.Include(s => s.Cursos).Include(s => s.Historial).AsSplitQuery().SingleAsync();
        Assert.Equal(100, s.Id); Assert.Equal(1,s.VersionProceso); Assert.Equal(EstadoSolicitud.Aprobada,s.Estado);
        var curso = Assert.Single(s.Cursos); Assert.Equal(4,curso.CreditosDestino); Assert.Equal(85,curso.Coincidencia); Assert.Equal(ResultadoAcademico.Aprobada,curso.Resultado);
        Assert.Equal(new[] { "Secretaría", "Comisión académica" }, s.Historial.OrderBy(h=>h.Id).Select(h=>h.Responsable));
        Assert.Single(await db.Cursos.ToListAsync()); Assert.Equal(2,await db.Cursos.IgnoreQueryFilters().CountAsync());
        Assert.Empty(SolicitudService.Acciones(s,PerfilDemo.Director));
        var service = new SolicitudService(db,TimeProvider.System,NullLogger<SolicitudService>.Instance);
        await Assert.ThrowsAsync<ReglaProcesoException>(()=>service.EjecutarAsync(new(){Id=s.Id,Revision=s.Revision,Accion=AccionSolicitud.EmitirDictamen,Comentario="Intento sobre expediente histórico."},PerfilDemo.Director));
        await DemoSeeder.SeedAsync(db); Assert.Equal(before,await Originales());
        await using var fk = connection.CreateCommand(); fk.CommandText="PRAGMA foreign_key_check";
        await using var fkReader = await fk.ExecuteReaderAsync(); Assert.False(await fkReader.ReadAsync());
    }

    [Fact]
    public async Task ExpedienteConVariasAsignaturasPersisteEntreContextos()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options; int id;
        await using(var first = new AppDbContext(options))
        {
            await first.Database.MigrateAsync(); await DemoSeeder.SeedAsync(first);
            var service = new SolicitudService(first,TimeProvider.System,NullLogger<SolicitudService>.Instance);
            id = await service.CrearAsync(new() { Estudiante="Alumno persistente ficticio",InstitucionOrigen="Instituto ficticio",CertificadoDocumento="CERT-DEMO-01",
                Cursos=[new(){AsignaturaId=1,AsignaturaOrigen="Curso persistente A",CreditosOrigen=4,Nota=16,SilaboDocumento="SIL-DEMO-01"},
                        new(){AsignaturaId=2,AsignaturaOrigen="Curso persistente B",CreditosOrigen=4,Nota=17,SilaboDocumento="SIL-DEMO-02"}] },PerfilDemo.Estudiante);
        }
        await using var second = new AppDbContext(options);
        await second.Database.MigrateAsync();
        var s = await second.Solicitudes.Include(s=>s.Cursos).Include(s=>s.Historial).AsSplitQuery().SingleAsync(s=>s.Id==id);
        Assert.Equal(2,s.Cursos.Count); Assert.Single(s.Historial); Assert.Equal(2,s.VersionProceso);
        Assert.Equal(new[]{"SIL-DEMO-01","SIL-DEMO-02"},s.Cursos.OrderBy(c=>c.Id).Select(c=>c.SilaboDocumento));
    }
}
