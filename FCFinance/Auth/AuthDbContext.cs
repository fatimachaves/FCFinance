using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FCFinance.Auth
{
    // Usuário do login. É o MESMO formato do AppUser do site (FCFinance.Web/Data/AppUser.cs),
    // porque os dois leem as mesmas tabelas AspNet* do banco FCFinanceDb.
    // Atenção: quem cria/altera essas tabelas é o site (migrations em FCFinance.Web/Data/Migrations).
    // Se mudar um campo aqui, mude lá também e crie a migration no projeto Web.
    public class AppUser : IdentityUser
    {
        public string Nome { get; set; } = "";
        public DateTime? TermosAceitosEm { get; set; }
        public DateTime? CriadoEm { get; set; }
    }

    public static class Perfis
    {
        public const string Admin = "Admin";
        public const string Usuario = "Usuario";
    }

    // Contexto só para ler/gravar usuários. NÃO gera migrations neste projeto.
    public class AuthDbContext : IdentityDbContext<AppUser, IdentityRole, string>
    {
        public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Entity<AppUser>().Property(u => u.Nome).HasMaxLength(100);
        }
    }
}
