using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FCFinance.Web.Data
{
    // Banco do login. Usa o mesmo banco da API (FCFinanceDb), mas só cria as tabelas AspNet*
    // (usuários, perfis, etc.) e a de chaves de criptografia. As migrations ficam em Data/Migrations.
    //
    // DataProtectionKeys: chaves que protegem o cookie de login, os formulários (antiforgery) e os links
    // enviados por e-mail. Ficam no banco para sobreviver a reinícios e atualizações do servidor
    // (sem isso, todo mundo seria deslogado e os links de e-mail parariam de funcionar a cada publicação).
    public class AuthDbContext : IdentityDbContext<AppUser, IdentityRole, string>, IDataProtectionKeyContext
    {
        public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options) { }

        public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Entity<AppUser>().Property(u => u.Nome).HasMaxLength(100);
        }
    }
}
