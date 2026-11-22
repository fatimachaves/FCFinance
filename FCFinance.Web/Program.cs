using System.Threading.RateLimiting;
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
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // só trafega em HTTPS
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);   // vale para "Manter conectado"
    options.SlidingExpiration = true;
});

// Confere a cada 1 minuto (em vez de 30) se o login ainda vale: depois de trocar a senha ou
// excluir o usuário, as outras sessões abertas caem em até 1 minuto.
builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.FromMinutes(1));

// Cookie de proteção dos formulários (antiforgery) também só em HTTPS.
builder.Services.AddAntiforgery(options => options.Cookie.SecurePolicy = CookieSecurePolicy.Always);

// Limite de tentativas de login POR IP (além do bloqueio de 5 senhas erradas por usuário):
// evita que alguém teste milhares de senhas ou bloqueie as contas dos outros de propósito.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
        await context.HttpContext.Response.WriteAsync("Muitas tentativas. Aguarde um minuto e tente de novo.", ct);
    };
    options.AddPolicy(SecurityHeaders.LoginPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

// Gera o token que o JavaScript das páginas usa para chamar a API.
builder.Services.AddSingleton<ApiTokenService>();

// Chamadas do servidor do site para a API (ex.: criar/excluir usuários). Endereço em "Api:BaseUrl".
var apiBaseUrl = builder.Configuration["Api:BaseUrl"]
    ?? throw new InvalidOperationException("Configure Api:BaseUrl (endereço da API) no appsettings.");
builder.Services.AddHttpClient<ApiUsuariosClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(15);
});

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

// Cabeçalhos de segurança + CSP (ver Data/SecurityHeaders.cs).
app.UseMiddleware<SecurityHeaders>();

app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication(); // lê o cookie e descobre quem é o usuário
app.UseAuthorization();  // decide se ele pode ver a página

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
