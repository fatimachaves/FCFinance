using Microsoft.EntityFrameworkCore;

namespace FCFinance.Models
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {

        }
        //Nenhum relacionamento terá exclusão em cascata configurada pelo EF Core.
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            foreach (var relationship in modelBuilder.Model
                .GetEntityTypes()
                .SelectMany(e => e.GetForeignKeys()))
            {
                relationship.DeleteBehavior = DeleteBehavior.NoAction;
            }

            // UserId guarda o Id do AspNetUsers. Sem FK: essas tabelas pertencem ao projeto Web.
            modelBuilder.Entity<Category>().Property(c => c.UserId).HasMaxLength(450);
            modelBuilder.Entity<Category>().HasIndex(c => c.UserId);
            modelBuilder.Entity<RegisteredValue>().Property(r => r.UserId).HasMaxLength(450);
            modelBuilder.Entity<RegisteredValue>().HasIndex(r => r.UserId);
            modelBuilder.Entity<Goal>().Property(g => g.UserId).HasMaxLength(450);

            // Tamanho máximo dos textos (os mesmos limites dos DTOs e dos campos do site).
            modelBuilder.Entity<Category>().Property(c => c.Name).HasMaxLength(Limites.NomeCategoria);
            modelBuilder.Entity<RegisteredValue>().Property(r => r.Name).HasMaxLength(Limites.NomeLancamento);
            modelBuilder.Entity<Goal>().Property(g => g.Name).HasMaxLength(Limites.NomeMeta);
            modelBuilder.Entity<ValueType>().Property(t => t.Name).HasMaxLength(50);

            // Valores em dinheiro: decimal(18,2).
            modelBuilder.Entity<RegisteredValue>().Property(r => r.Value).HasPrecision(18, 2);
            modelBuilder.Entity<Goal>().Property(g => g.TargetValue).HasPrecision(18, 2);
            modelBuilder.Entity<Goal>().Property(g => g.CurrentValue).HasPrecision(18, 2);

            // Tipos fixos do sistema. Os Ids 1 e 2 são usados pelo front (TYPE_IDS), não mude.
            modelBuilder.Entity<ValueType>().HasData(
                new ValueType { Id = ValueType.Receita, Name = "Receita" },
                new ValueType { Id = ValueType.Despesa, Name = "Despesa" });

            // Garante no banco que cada usuário tem no máximo UMA meta prioritária.
            modelBuilder.Entity<Goal>()
                .HasIndex(g => new { g.UserId, g.IsPriority })
                .IsUnique()
                .HasFilter("[IsPriority] = 1");
        }

        public DbSet<Category> Categories => Set<Category>();
        public DbSet<RegisteredValue> RegisteredValues => Set<RegisteredValue>();

        public DbSet<ValueType> ValueTypes => Set<ValueType>();
        public DbSet<Goal> Goals => Set<Goal>();
    }
}
