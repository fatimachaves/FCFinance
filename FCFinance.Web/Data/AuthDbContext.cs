using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FCFinance.Web.Data
{
    // Banco do login. Usa o mesmo banco da API (FCFinanceDb), mas só cria as tabelas AspNet*
    // (usuários, perfis, etc.). As migrations deste contexto ficam em Data/Migrations.
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
