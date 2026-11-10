using Convalidacion.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Convalidacion.Web.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options, IHttpContextAccessor? http = null) : DbContext(options)
{
    // La clave se genera en el servidor y se conserva en la sesión protegida.
    // El ámbito vacío conserva registros anteriores y permite migraciones/sembrado fuera de HTTP.
    public string AmbitoActual => http?.HttpContext?.Session.GetString("AmbitoDemo") ?? "";
    public DbSet<Solicitud> Solicitudes => Set<Solicitud>();
    public DbSet<Asignatura> Asignaturas => Set<Asignatura>();
    public DbSet<Seguimiento> Seguimientos => Set<Seguimiento>();
    public DbSet<AsignaturaSolicitud> Cursos => Set<AsignaturaSolicitud>();
    public DbSet<EvaluacionAsignatura> Evaluaciones => Set<EvaluacionAsignatura>();
    public DbSet<CorreccionDocumento> Correcciones => Set<CorreccionDocumento>();
    public DbSet<DictamenAcademico> Dictamenes => Set<DictamenAcademico>();
    public DbSet<ResolucionDecanal> Resoluciones => Set<ResolucionDecanal>();
    public DbSet<NotificacionExpediente> Notificaciones => Set<NotificacionExpediente>();
    public DbSet<RegistroAcademico> Registros => Set<RegistroAcademico>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Solicitud>().HasIndex(s => s.Estado);
        modelBuilder.Entity<Solicitud>().Property(s => s.VersionProceso).HasDefaultValue(1);
        modelBuilder.Entity<Solicitud>().HasIndex(s => s.AmbitoDemo);
        modelBuilder.Entity<Solicitud>().HasQueryFilter(s => s.AmbitoDemo == AmbitoActual);
        modelBuilder.Entity<Seguimiento>().HasQueryFilter(h => h.Solicitud.AmbitoDemo == AmbitoActual);
        modelBuilder.Entity<Solicitud>().HasOne(s => s.Asignatura).WithMany().OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Solicitud>().HasMany(s => s.Historial).WithOne(h => h.Solicitud).HasForeignKey(h => h.SolicitudId);
        modelBuilder.Entity<Asignatura>().HasIndex(a => a.Codigo).IsUnique();
        modelBuilder.Entity<AsignaturaSolicitud>().HasQueryFilter(c => c.Solicitud.AmbitoDemo == AmbitoActual);
        modelBuilder.Entity<AsignaturaSolicitud>().HasOne(c => c.Asignatura).WithMany().OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<AsignaturaSolicitud>().HasOne(c => c.Solicitud).WithMany(s => s.Cursos).HasForeignKey(c => c.SolicitudId);
        modelBuilder.Entity<EvaluacionAsignatura>().HasQueryFilter(e => e.AsignaturaSolicitud.Solicitud.AmbitoDemo == AmbitoActual);
        modelBuilder.Entity<CorreccionDocumento>().HasQueryFilter(c => c.Solicitud.AmbitoDemo == AmbitoActual);
        modelBuilder.Entity<DictamenAcademico>().HasQueryFilter(d => d.Solicitud.AmbitoDemo == AmbitoActual);
        modelBuilder.Entity<DictamenAcademico>().HasIndex(d => new { d.SolicitudId, d.Version }).IsUnique();
        modelBuilder.Entity<ResolucionDecanal>().HasQueryFilter(r => r.Solicitud.AmbitoDemo == AmbitoActual);
        modelBuilder.Entity<ResolucionDecanal>().HasOne(r => r.Solicitud).WithOne(s => s.Resolucion).HasForeignKey<ResolucionDecanal>(r => r.SolicitudId);
        modelBuilder.Entity<ResolucionDecanal>().HasOne(r => r.DictamenAcademico).WithMany().OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<NotificacionExpediente>().HasQueryFilter(n => n.Solicitud.AmbitoDemo == AmbitoActual);
        modelBuilder.Entity<NotificacionExpediente>().HasIndex(n => new { n.SolicitudId, n.Destinatario }).IsUnique();
        modelBuilder.Entity<RegistroAcademico>().HasQueryFilter(r => r.AsignaturaSolicitud.Solicitud.AmbitoDemo == AmbitoActual);
        modelBuilder.Entity<RegistroAcademico>().HasOne(r => r.AsignaturaSolicitud).WithOne(c => c.Registro).HasForeignKey<RegistroAcademico>(r => r.AsignaturaSolicitudId);
        modelBuilder.Entity<RegistroAcademico>().HasOne(r => r.ResolucionDecanal).WithMany().OnDelete(DeleteBehavior.Restrict);
    }
}
