using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FCFinance.Web.Data
{
    // Roda quando o site inicia:
    // 1) aplica as migrations do login (cria as tabelas AspNet* se faltarem);
    // 2) cria os perfis Admin e Usuario;
    // 3) cria os usuários padrão "admin" e "usuario", SE ainda não existirem.
    //
    // As senhas iniciais NÃO ficam no código: vêm da configuração "UsuariosPadrao"
    // (no seu PC, ficam no User Secrets: dotnet user-secrets list). Depois de criado,
    // trocar a senha na configuração não muda a senha do usuário: use a tela "Alterar senha".
    public static class IdentitySeeder
    {
        public static async Task SeedAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var sp = scope.ServiceProvider;
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeeder");
            var config = sp.GetRequiredService<IConfiguration>();

            await sp.GetRequiredService<AuthDbContext>().Database.MigrateAsync();

            var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var perfil in new[] { Perfis.Admin, Perfis.Usuario })
                if (!await roles.RoleExistsAsync(perfil))
                    await roles.CreateAsync(new IdentityRole(perfil));

            var users = sp.GetRequiredService<UserManager<AppUser>>();
            await CriarUsuario(users, config, logger, "Admin", Perfis.Admin);
            await CriarUsuario(users, config, logger, "Usuario", Perfis.Usuario);
        }

        private static async Task CriarUsuario(UserManager<AppUser> users, IConfiguration config, ILogger logger,
            string secao, string perfil)
        {
            var cfg = config.GetSection($"UsuariosPadrao:{secao}");
            var login = cfg["Login"];
            var senha = cfg["Senha"];
            if (string.IsNullOrWhiteSpace(login)) return;
            if (await users.FindByNameAsync(login) != null) return; // já existe: não mexe

            if (string.IsNullOrWhiteSpace(senha))
            {
                logger.LogWarning("Usuário padrão '{Login}' não foi criado: defina UsuariosPadrao:{Secao}:Senha (User Secrets).", login, secao);
                return;
            }

            var user = new AppUser { UserName = login, Nome = cfg["Nome"] ?? login, EmailConfirmed = true };
            var result = await users.CreateAsync(user, senha);
            if (!result.Succeeded)
            {
                logger.LogError("Não foi possível criar '{Login}': {Erros}", login, string.Join("; ", result.Errors.Select(e => e.Description)));
                return;
            }
            await users.AddToRoleAsync(user, perfil);
            logger.LogInformation("Usuário padrão '{Login}' criado com o perfil {Perfil}.", login, perfil);
        }
    }
}
