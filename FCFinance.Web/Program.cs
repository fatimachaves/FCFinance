using FCFinance.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------- Banco do login (mesmo banco da API, tabelas AspNet*) ----------
builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.MigrationsHistoryTable("__AuthMigrationsHistory"))); // histórico separado do da API

// ---------- ASP.NET Core Identity ----------
builder.Services
    .AddIdentity<AppUser, IdentityRole>(options =>
    {
        // Regras de senha
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;

        // Bloqueia o login por 5 minutos depois de 5 senhas erradas seguidas
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);

        options.User.RequireUniqueEmail = false; // login é pelo nome de usuário
    })
    .AddEntityFrameworkStores<AuthDbContext>()
    .AddClaimsPrincipalFactory<AppClaimsFactory>() // coloca o Nome no cookie de login
    .AddDefaultTokenProviders();

// Cookie de login
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Conta/Login";               // quem não está logado é mandado para cá
    options.LogoutPath = "/Conta/Sair";
    options.AccessDeniedPath = "/Conta/AcessoNegado"; // logado, mas sem permissão
    options.Cookie.Name = "FCFinance.Auth";
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);   // vale para "Manter conectado"
    options.SlidingExpiration = true;
});

// Gera o token que o JavaScript das páginas usa para chamar a API.
builder.Services.AddSingleton<ApiTokenService>();

// Todas as páginas exigem login, a não ser que tenham [AllowAnonymous] (ex.: a tela de login).
builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AuthorizeFilter(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())));

var app = builder.Build();

// Cria as tabelas do login, os perfis e os usuários padrão (admin / usuario).
await IdentitySeeder.SeedAsync(app.Services);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication(); // lê o cookie e descobre quem é o usuário
app.UseAuthorization();  // decide se ele pode ver a página

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
