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
        await connection.OpenAsync(); db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.MigrateAsync(); await DemoSeeder.SeedAsync(db);
        service = new(db, TimeProvider.System, NullLogger<SolicitudService>.Instance);
    }
    public async Task DisposeAsync() { await db.DisposeAsync(); await connection.DisposeAsync(); }
    private static CrearSolicitudViewModel Valida() => new()
    {
        Estudiante = "Estudiante Demo Prueba", InstitucionOrigen = "Instituto Ficticio", CertificadoDocumento = "CERT-DEMO-01",
        Cursos = [new() { AsignaturaOrigen = "Curso de prueba A", AsignaturaId = 1, CreditosOrigen = 4, Nota = 15, SilaboDocumento = "SIL-DEMO-01" },
                  new() { AsignaturaOrigen = "Curso de prueba B", AsignaturaId = 2, CreditosOrigen = 4, Nota = 16, SilaboDocumento = "SIL-DEMO-01" }]
    };
    private async Task<Solicitud> S(int id) => await db.Solicitudes.Include(s => s.Cursos).ThenInclude(c => c.Evaluaciones)
        .Include(s => s.Cursos).ThenInclude(c => c.Registro).Include(s => s.Historial).Include(s => s.Dictamenes)
        .Include(s => s.Resolucion).Include(s => s.Correcciones).Include(s => s.Notificaciones).AsSplitQuery().SingleAsync(s => s.Id == id);
    private async Task Aplicar(int id, AccionSolicitud accion, PerfilDemo perfil, bool verificados = true)
    {
        var s = await S(id); await service.EjecutarAsync(new() { Id = id, Revision = s.Revision, Accion = accion,
            Comentario = "Sustento ficticio para la prueba.", DocumentosVerificados = verificados,
            NumeroResolucion = $"DEMO-RD-PRUEBA-{id}", FechaResolucion = new DateOnly(2026, 11, 9) }, perfil);
    }
    private async Task Evaluar(int id, int indice, ResultadoAcademico resultado = ResultadoAcademico.Aprobada, int porcentaje = 85, bool excepcion = false, string? sustento = null)
    {
        var s = await S(id); await service.EvaluarAsync(new() { Id = id, Revision = s.Revision,
            AsignaturaSolicitudId = s.Cursos.OrderBy(c => c.Id).ElementAt(indice).Id, Coincidencia = porcentaje, Resultado = resultado,
            Justificacion = "Evaluación manual ficticia de la asignatura.", ExcepcionAfinidad = excepcion, JustificacionExcepcion = sustento }, PerfilDemo.Director);
    }
    private async Task<int> HastaEvaluacion(CrearSolicitudViewModel? model = null)
    {
        var id = await service.CrearAsync(model ?? Valida(), PerfilDemo.Estudiante);
        await Aplicar(id, AccionSolicitud.Recibir, PerfilDemo.Admision); await Aplicar(id, AccionSolicitud.DerivarDepartamento, PerfilDemo.Admision); return id;
    }
    private async Task<int> HastaRevision(bool todasRechazadas = false)
    {
        var id = await HastaEvaluacion(); await Evaluar(id, 0, todasRechazadas ? ResultadoAcademico.Rechazada : ResultadoAcademico.Aprobada);
        await Evaluar(id, 1, ResultadoAcademico.Rechazada, 60); await Aplicar(id, AccionSolicitud.EmitirDictamen, PerfilDemo.Director);
        await Aplicar(id, AccionSolicitud.RemitirDecano, PerfilDemo.Director); return id;
    }
    private async Task HastaRegistro(int id)
    {
        await Aplicar(id, AccionSolicitud.AprobarDictamen, PerfilDemo.Decano); await Aplicar(id, AccionSolicitud.EmitirResolucion, PerfilDemo.Decano);
        await Aplicar(id, AccionSolicitud.Notificar, PerfilDemo.Decano); await Aplicar(id, AccionSolicitud.RemitirRegistro, PerfilDemo.Decano);
    }

    [Fact]
    public async Task RegistroRevisionAprobacionConservaHistorial()
    {
        var id = await HastaRevision(); await HastaRegistro(id);
        await Aplicar(id, AccionSolicitud.RegistrarAsignaturas, PerfilDemo.RegistrosAcademicos); await Aplicar(id, AccionSolicitud.Finalizar, PerfilDemo.RegistrosAcademicos);
        var s = await S(id); Assert.Equal(EstadoSolicitud.Finalizada, s.Estado); Assert.Equal(2, s.Cursos.Count);
        Assert.Equal(13, s.Historial.Count); Assert.Single(s.Cursos, c => c.Registro is not null);
        Assert.Equal("Convalidación parcial", s.Resolucion!.Decision);
        var eventos = s.Historial.OrderBy(h => h.Id).ToList(); Assert.Null(eventos[0].EstadoAnterior);
        for(var i = 1; i < eventos.Count; i++) Assert.Equal(eventos[i-1].Estado, eventos[i].EstadoAnterior);
        Assert.All(eventos, h => { Assert.NotNull(h.Accion); Assert.Null(h.PerfilHistorico); Assert.True(h.Comentario.Length >= 5); });
        Assert.Equal(5, eventos.Select(h => h.Perfil).Distinct().Count());
    }

    [Fact]
    public async Task ObservacionYSubsanacionDevuelveAAdmision()
    {
        var id = await service.CrearAsync(Valida(), PerfilDemo.Estudiante);
        await Aplicar(id, AccionSolicitud.Recibir, PerfilDemo.Admision); await Aplicar(id, AccionSolicitud.ObservarDocumentos, PerfilDemo.Admision);
        Assert.Equal(EstadoSolicitud.Observada, (await S(id)).Estado);
        await Aplicar(id, AccionSolicitud.DevolverEstudiante, PerfilDemo.Admision);
        var s = await S(id); var modelo = new SubsanarSolicitudViewModel { Id = id, Revision = s.Revision, CertificadoDocumento = "CERT-DEMO-CORREGIDO",
            Comentario = "Se corrige el certificado ficticio observado.", Cursos = s.Cursos.Select(c => new CorreccionSilaboViewModel { AsignaturaSolicitudId = c.Id, SilaboDocumento = c.SilaboDocumento }).ToList() };
        await service.SubsanarAsync(modelo, PerfilDemo.Estudiante); Assert.Equal(EstadoSolicitud.Subsanada, s.Estado);
        Assert.Single(s.Correcciones); Assert.Equal("CERT-DEMO-01", s.Correcciones[0].Anterior);
        await Aplicar(id, AccionSolicitud.VerificarCorreccion, PerfilDemo.Admision); await Aplicar(id, AccionSolicitud.DerivarDepartamento, PerfilDemo.Admision);
        Assert.Equal(EstadoSolicitud.EvaluacionAcademica, s.Estado);
    }

    [Theory]
    [InlineData(10, 90, 4)] [InlineData(15, 79, 4)] [InlineData(15, 90, 3)]
    public async Task AprobacionNoPermiteCriteriosInsuficientes(int nota, int coincidencia, int creditos)
    {
        var model = Valida(); model.Cursos[0].Nota = nota; model.Cursos[0].CreditosOrigen = creditos;
        var id = await HastaEvaluacion(model); var antes = (await S(id)).Historial.Count;
        await Assert.ThrowsAsync<ReglaProcesoException>(() => Evaluar(id, 0, porcentaje: coincidencia));
        Assert.Equal(antes, (await S(id)).Historial.Count); Assert.Empty((await S(id)).Cursos[0].Evaluaciones);
    }

    [Fact]
    public async Task LimitesMinimosPermitenAprobacionManual()
    {
        var model = Valida(); model.Cursos[0].Nota = 11; var id = await HastaEvaluacion(model);
        await Evaluar(id, 0, porcentaje:80); Assert.Equal(ResultadoAcademico.Aprobada, (await S(id)).Cursos[0].Resultado);
    }

    [Theory]
    [InlineData(PerfilDemo.Estudiante, AccionSolicitud.AprobarDictamen)]
    [InlineData(PerfilDemo.Director, AccionSolicitud.EmitirDictamen)]
    [InlineData(PerfilDemo.Estudiante, AccionSolicitud.Recibir)]
    [InlineData(PerfilDemo.RegistrosAcademicos, AccionSolicitud.Finalizar)]
    public async Task ImpideSaltarEtapasOCambiarDesdePerfilIncorrecto(PerfilDemo perfil, AccionSolicitud accion)
    {
        var id = await service.CrearAsync(Valida(), PerfilDemo.Estudiante);
        await Assert.ThrowsAsync<ReglaProcesoException>(() => Aplicar(id, accion, perfil));
        Assert.Equal(EstadoSolicitud.Presentada, (await S(id)).Estado);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task DecisionFinalNoSeReabre(bool todasRechazadas)
    {
        var id = await HastaRevision(todasRechazadas); await HastaRegistro(id);
        await Aplicar(id, AccionSolicitud.RegistrarAsignaturas, PerfilDemo.RegistrosAcademicos); await Aplicar(id, AccionSolicitud.Finalizar, PerfilDemo.RegistrosAcademicos);
        await Assert.ThrowsAsync<ReglaProcesoException>(() => Aplicar(id, AccionSolicitud.DevolverDictamen, PerfilDemo.Decano));
        Assert.Equal(todasRechazadas ? 0 : 1, (await S(id)).Cursos.Count(c => c.Registro is not null));
    }

    [Fact]
    public async Task RevisionAntiguaNoModificaElExpediente()
    {
        var id = await service.CrearAsync(Valida(), PerfilDemo.Estudiante); var antigua = (await S(id)).Revision;
        await Aplicar(id, AccionSolicitud.Recibir, PerfilDemo.Admision);
        await Assert.ThrowsAsync<ReglaProcesoException>(() => service.EjecutarAsync(new() { Id = id, Revision = antigua,
            Accion = AccionSolicitud.DerivarDepartamento, DocumentosVerificados = true, Comentario = "Intento desactualizado." }, PerfilDemo.Admision));
        Assert.Equal(EstadoSolicitud.VerificacionDocumental, (await S(id)).Estado);
    }

    [Fact]
    public async Task ConcurrenciaRealDeDosContextosNoSobrescribe()
    {
        var id = await service.CrearAsync(Valida(), PerfilDemo.Estudiante);
        await using var other = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        var antigua = await other.Solicitudes.FindAsync(id); await Aplicar(id, AccionSolicitud.Recibir, PerfilDemo.Admision);
        antigua!.Estado = EstadoSolicitud.Observada; antigua.Revision = Guid.NewGuid();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => other.SaveChangesAsync());
    }

    [Theory] [InlineData(false, true)] [InlineData(true, false)]
    public async Task NoRegistraSinDocumentosSimulados(bool certificado, bool silabo)
    {
        var model = Valida(); if (!certificado) model.CertificadoDocumento = ""; if (!silabo) model.Cursos[0].SilaboDocumento = "";
        await Assert.ThrowsAsync<ReglaProcesoException>(() => service.CrearAsync(model, PerfilDemo.Estudiante));
    }

    [Fact]
    public async Task NoPermiteAsignaturaInexistenteNiPerfilDeAdmision()
    {
        var model = Valida(); model.Cursos[0].AsignaturaId = 999;
        await Assert.ThrowsAsync<ReglaProcesoException>(() => service.CrearAsync(model, PerfilDemo.Estudiante));
        await Assert.ThrowsAsync<ReglaProcesoException>(() => service.CrearAsync(Valida(), PerfilDemo.Admision));
    }

    [Fact]
    public async Task SeedEsIdempotenteYConservaSolicitudesNuevas()
    {
        var id = await service.CrearAsync(Valida(), PerfilDemo.Estudiante); await DemoSeeder.SeedAsync(db);
        Assert.Equal(7, await db.Solicitudes.CountAsync()); Assert.Equal(4, await db.Asignaturas.CountAsync());
        Assert.NotNull(await db.Solicitudes.FindAsync(id)); Assert.Equal(3, (await db.Database.GetAppliedMigrationsAsync()).Count());
        Assert.All(await db.Seguimientos.ToListAsync(), h => Assert.Null(h.PerfilHistorico));
    }

    [Theory] [InlineData(70)] [InlineData(79)]
    public async Task ExcepcionPorAfinidadExigeJustificacionYCreditosEquivalentes(int porcentaje)
    {
        var id = await HastaEvaluacion(); await Assert.ThrowsAsync<ReglaProcesoException>(() => Evaluar(id, 0, porcentaje:porcentaje, excepcion:true));
        await Evaluar(id, 0, porcentaje:porcentaje, excepcion:true, sustento:"Afinidad temática directa ficticia con créditos equivalentes.");
        var e = Assert.Single((await S(id)).Cursos[0].Evaluaciones); Assert.True(e.ExcepcionAfinidad); Assert.Equal(porcentaje, e.Coincidencia);
    }

    [Theory] [InlineData(69, 4)] [InlineData(75, 5)] [InlineData(75, 3)]
    public async Task ExcepcionNoPermitePorcentajeInsuficienteNiCreditosDistintos(int porcentaje, int creditos)
    {
        var model = Valida(); model.Cursos[0].CreditosOrigen = creditos; var id = await HastaEvaluacion(model);
        await Assert.ThrowsAsync<ReglaProcesoException>(() => Evaluar(id, 0, porcentaje:porcentaje, excepcion:true, sustento:"Afinidad temática ficticia debidamente justificada."));
    }

    [Fact]
    public async Task PorcentajeAltoNoApruebaAutomaticamenteNiEmiteDictamenIncompleto()
    {
        var id = await HastaEvaluacion(); await Evaluar(id, 0, ResultadoAcademico.Rechazada, 95);
        Assert.Equal(ResultadoAcademico.Rechazada, (await S(id)).Cursos[0].Resultado);
        await Assert.ThrowsAsync<ReglaProcesoException>(() => Aplicar(id, AccionSolicitud.EmitirDictamen, PerfilDemo.Director));
        await Evaluar(id, 1, ResultadoAcademico.Observada, 90);
        await Assert.ThrowsAsync<ReglaProcesoException>(() => Aplicar(id, AccionSolicitud.EmitirDictamen, PerfilDemo.Director));
    }

    [Fact]
    public async Task DevolucionDecanalConservaDictamenAnteriorYRequiereNuevaEvaluacion()
    {
        var id = await HastaRevision(); var s = await S(id); var json = s.Dictamenes[0].DetallesJson;
        await Aplicar(id, AccionSolicitud.DevolverDictamen, PerfilDemo.Decano);
        Assert.Equal(SituacionDictamen.Devuelto, s.Dictamenes[0].Situacion); Assert.All(s.Cursos, c => Assert.Equal(ResultadoAcademico.Pendiente, c.Resultado));
        await Assert.ThrowsAsync<ReglaProcesoException>(() => Aplicar(id, AccionSolicitud.EmitirDictamen, PerfilDemo.Director));
        await Evaluar(id, 0); await Evaluar(id, 1); await Aplicar(id, AccionSolicitud.EmitirDictamen, PerfilDemo.Director);
        Assert.Equal(2, s.Dictamenes.Count); Assert.Equal(json, s.Dictamenes.Single(d => d.Version == 1).DetallesJson);
        Assert.Equal(2, s.Cursos[0].Evaluaciones.Count); Assert.All(s.Dictamenes.Single(d => d.Version == 2).Detalles, d => Assert.Equal(ResultadoAcademico.Aprobada, d.Resultado));
    }

    [Fact]
    public async Task CorreccionAcademicaTieneRutaCompletaEInvalidaSoloLaEvaluacionCorregida()
    {
        var id = await HastaEvaluacion(); await Evaluar(id, 0, ResultadoAcademico.Observada); await Evaluar(id, 1);
        await Aplicar(id, AccionSolicitud.SolicitarCorreccionAcademica, PerfilDemo.Director); await Aplicar(id, AccionSolicitud.DevolverEstudiante, PerfilDemo.Admision);
        var s = await S(id); var cursos = s.Cursos.OrderBy(c => c.Id).ToList();
        await service.SubsanarAsync(new() { Id = id, Revision = s.Revision, Comentario = "Se corrige el contenido del sílabo observado.", CertificadoDocumento = s.CertificadoDocumento,
            Cursos = cursos.Select((c, i) => new CorreccionSilaboViewModel { AsignaturaSolicitudId = c.Id, SilaboDocumento = i == 0 ? "SIL-DEMO-CORREGIDO" : c.SilaboDocumento }).ToList() }, PerfilDemo.Estudiante);
        Assert.Equal(2, cursos[0].VersionDocumento); Assert.Equal(ResultadoAcademico.Pendiente, cursos[0].Resultado);
        Assert.Single(cursos[0].Evaluaciones); Assert.Equal(ResultadoAcademico.Aprobada, cursos[1].Resultado);
        await Aplicar(id, AccionSolicitud.VerificarCorreccion, PerfilDemo.Admision); await Aplicar(id, AccionSolicitud.DerivarDepartamento, PerfilDemo.Admision);
        await Evaluar(id, 0); await Aplicar(id, AccionSolicitud.EmitirDictamen, PerfilDemo.Director);
        Assert.Equal(2, cursos[0].Evaluaciones.Count);
    }

    [Fact]
    public async Task AdmisionNoDerivaSinConfirmacionDocumental()
    {
        var id = await service.CrearAsync(Valida(), PerfilDemo.Estudiante); await Aplicar(id, AccionSolicitud.Recibir, PerfilDemo.Admision);
        await Assert.ThrowsAsync<ReglaProcesoException>(() => Aplicar(id, AccionSolicitud.DerivarDepartamento, PerfilDemo.Admision, false));
    }

    [Fact]
    public async Task ResolucionRequiereAprobacionNumeroYFechaYNotificaSoloUnaVez()
    {
        var id = await HastaRevision(); await Assert.ThrowsAsync<ReglaProcesoException>(() => Aplicar(id, AccionSolicitud.EmitirResolucion, PerfilDemo.Decano));
        await Aplicar(id, AccionSolicitud.AprobarDictamen, PerfilDemo.Decano); var s = await S(id);
        await Assert.ThrowsAsync<ReglaProcesoException>(() => service.EjecutarAsync(new() { Id = id, Revision = s.Revision,
            Accion = AccionSolicitud.EmitirResolucion, Comentario = "Resolución sin datos obligatorios." }, PerfilDemo.Decano));
        await Aplicar(id, AccionSolicitud.EmitirResolucion, PerfilDemo.Decano); Assert.Equal(s.Dictamenes[0].DetallesJson, s.Resolucion!.DetallesJson);
        await Aplicar(id, AccionSolicitud.Notificar, PerfilDemo.Decano); Assert.Equal(2, s.Notificaciones.Count);
        Assert.Equal(new[] { PerfilDemo.Estudiante, PerfilDemo.RegistrosAcademicos }, s.Notificaciones.Select(n => n.Destinatario));
        await Assert.ThrowsAsync<ReglaProcesoException>(() => Aplicar(id, AccionSolicitud.Notificar, PerfilDemo.Decano));
    }

    [Fact]
    public async Task RegistroNoFinalizaAntesDeCargarNiDuplicaAsignaturas()
    {
        var id = await HastaRevision(); await HastaRegistro(id);
        await Assert.ThrowsAsync<ReglaProcesoException>(() => Aplicar(id, AccionSolicitud.Finalizar, PerfilDemo.RegistrosAcademicos));
        await Aplicar(id, AccionSolicitud.RegistrarAsignaturas, PerfilDemo.RegistrosAcademicos);
        await Assert.ThrowsAsync<ReglaProcesoException>(() => Aplicar(id, AccionSolicitud.RegistrarAsignaturas, PerfilDemo.RegistrosAcademicos));
        var registro = Assert.Single((await S(id)).Cursos, c => c.Registro is not null);
        Assert.Equal(ResultadoAcademico.Aprobada, registro.Resultado); Assert.Equal(registro.CreditosDestino, registro.Registro!.Creditos);
    }

    [Fact]
    public async Task ValidaColeccionCatalogoYReferenciasDocumentales()
    {
        foreach(var model in new[] { Valida(), Valida(), Valida(), Valida() }.Select((m, i) => { if(i==0) m.Cursos=[]; if(i==1) m.Cursos[1].AsignaturaId=1; if(i==2) m.CertificadoDocumento="archivo-real.pdf"; if(i==3) m.Cursos[0].SilaboDocumento="inventado"; return m; }))
            await Assert.ThrowsAsync<ReglaProcesoException>(() => service.CrearAsync(model, PerfilDemo.Estudiante));
    }
}
