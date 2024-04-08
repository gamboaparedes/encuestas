using Microsoft.EntityFrameworkCore;

namespace ProyectoLogin.Models
{
    public class UsuarioContext : DbContext
    {
        public UsuarioContext(DbContextOptions<UsuarioContext> options) : base(options)
        {
        }

        public DbSet<Usuario> Usuarios { get; set; }

        public DbSet<votacion> votacion { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Ignorar las propiedades que no deseas mapear a la base de datos
            modelBuilder.Entity<votacion>()
                .Ignore(v => v.Count)
                .Ignore(v => v.CandidatoNombre)
                .Ignore(v => v.CandidatoImagen)
                .Ignore(v => v.PartidoImagen);

            base.OnModelCreating(modelBuilder);
        }
    }
}
