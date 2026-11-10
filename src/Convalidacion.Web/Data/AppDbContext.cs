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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Solicitud>().HasIndex(s => s.Estado);
        modelBuilder.Entity<Solicitud>().HasIndex(s => s.AmbitoDemo);
        modelBuilder.Entity<Solicitud>().HasQueryFilter(s => s.AmbitoDemo == AmbitoActual);
        modelBuilder.Entity<Seguimiento>().HasQueryFilter(h => h.Solicitud.AmbitoDemo == AmbitoActual);
        modelBuilder.Entity<Solicitud>().HasOne(s => s.Asignatura).WithMany().OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Solicitud>().HasMany(s => s.Historial).WithOne(h => h.Solicitud).HasForeignKey(h => h.SolicitudId);
        modelBuilder.Entity<Asignatura>().HasIndex(a => a.Codigo).IsUnique();
    }
}
