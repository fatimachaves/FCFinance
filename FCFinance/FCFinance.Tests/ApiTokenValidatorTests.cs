using System.Security.Claims;
using FCFinance.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FCFinance.Tests
{
    // Prova que um token deixa de valer quando: a senha muda, o usuário é excluído ou perde um perfil.
    public sealed class ApiTokenValidatorTests : IDisposable
    {
        private const string Senha = "SenhaForte123";
        private readonly SqliteConnection _connection = new("DataSource=:memory:");
        private readonly ServiceProvider _services;

        public ApiTokenValidatorTests()
        {
            _connection.Open();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<AuthDbContext>(o => o.UseSqlite(_connection));
            services.AddIdentityCore<AppUser>(o =>
                {
                    o.Password.RequireNonAlphanumeric = false; // mesmas regras de senha da API
                    o.ClaimsIdentity.UserIdClaimType = "sub";
                    o.ClaimsIdentity.RoleClaimType = "role";
                })
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<AuthDbContext>();
            services.AddScoped<ApiTokenValidator>();
            _services = services.BuildServiceProvider();

            using var scope = _services.CreateScope();
            scope.ServiceProvider.GetRequiredService<AuthDbContext>().Database.EnsureCreated();
        }

        public void Dispose()
        {
            _services.Dispose();
            _connection.Dispose();
        }

        // Cada passo usa um "scope" novo, como uma requisição nova na API.
        private async Task<T> Run<T>(Func<IServiceProvider, Task<T>> action)
        {
            using var scope = _services.CreateScope();
            return await action(scope.ServiceProvider);
        }

        private Task<AppUser> CreateUserAsync(params string[] roles) => Run(async sp =>
        {
            var users = sp.GetRequiredService<UserManager<AppUser>>();
            var rolesManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
            var user = new AppUser { UserName = "ana", Nome = "Ana" };
            Assert.True((await users.CreateAsync(user, Senha)).Succeeded);
            foreach (var role in roles)
            {
                if (!await rolesManager.RoleExistsAsync(role)) await rolesManager.CreateAsync(new IdentityRole(role));
                await users.AddToRoleAsync(user, role);
            }
            return user;
        });

        // Mesmo conteúdo que o TokenService coloca no token.
        private static ClaimsPrincipal TokenOf(AppUser user, params string[] roles)
        {
            var claims = new List<Claim> { new("sub", user.Id), new("name", user.UserName!), new(ApiTokenValidator.SecurityStampClaim, user.SecurityStamp ?? "") };
            claims.AddRange(roles.Select(r => new Claim("role", r)));
            return new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer", "name", "role"));
        }

        private Task<string?> Validate(ClaimsPrincipal token) =>
            Run(sp => sp.GetRequiredService<ApiTokenValidator>().ValidateAsync(token));

        [Fact]
        public async Task Token_valido_continua_valendo()
        {
            var user = await CreateUserAsync(Perfis.Usuario);
            Assert.Null(await Validate(TokenOf(user, Perfis.Usuario)));
        }

        [Fact]
        public async Task Trocar_a_senha_derruba_os_tokens_antigos()
        {
            var user = await CreateUserAsync();
            var tokenAntigo = TokenOf(user);

            await Run(async sp =>
            {
                var users = sp.GetRequiredService<UserManager<AppUser>>();
                return await users.ChangePasswordAsync((await users.FindByIdAsync(user.Id))!, Senha, "OutraSenha456");
            });

            Assert.Contains("Sessão encerrada", await Validate(tokenAntigo));
        }

        [Fact]
        public async Task Usuario_excluido_perde_o_acesso()
        {
            var user = await CreateUserAsync();
            var token = TokenOf(user);

            await Run(async sp =>
            {
                var users = sp.GetRequiredService<UserManager<AppUser>>();
                return await users.DeleteAsync((await users.FindByIdAsync(user.Id))!);
            });

            Assert.Contains("não existe", await Validate(token));
        }

        [Fact]
        public async Task Perfil_retirado_vale_na_hora()
        {
            var user = await CreateUserAsync(Perfis.Admin);
            var token = TokenOf(user, Perfis.Admin);

            await Run(async sp =>
            {
                var users = sp.GetRequiredService<UserManager<AppUser>>();
                return await users.RemoveFromRoleAsync((await users.FindByIdAsync(user.Id))!, Perfis.Admin);
            });

            // O token ainda é aceito (a senha não mudou), mas sem o perfil Admin.
            Assert.Null(await Validate(token));
            Assert.False(token.IsInRole(Perfis.Admin));
        }

        [Fact]
        public async Task Token_sem_carimbo_e_recusado()
        {
            var user = await CreateUserAsync();
            var semCarimbo = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", user.Id)], "Bearer", "name", "role"));
            Assert.NotNull(await Validate(semCarimbo));
        }
    }
}
