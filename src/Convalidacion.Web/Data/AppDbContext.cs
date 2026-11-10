using Convalidacion.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Convalidacion.Web.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Solicitud> Solicitudes => Set<Solicitud>();
    public DbSet<Asignatura> Asignaturas => Set<Asignatura>();
    public DbSet<Seguimiento> Seguimientos => Set<Seguimiento>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Solicitud>().HasIndex(s => s.Estado);
        modelBuilder.Entity<Solicitud>().HasOne(s => s.Asignatura).WithMany().OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Solicitud>().HasMany(s => s.Historial).WithOne().HasForeignKey(h => h.SolicitudId);
        modelBuilder.Entity<Asignatura>().HasIndex(a => a.Codigo).IsUnique();
    }
}
