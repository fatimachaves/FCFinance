using System.Threading.RateLimiting;
using FCFinance.Web.Data;
using FCFinance.Web.Data.Email;
using Microsoft.AspNetCore.DataProtection;
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

        // E-mail único é conferido no cadastro (ContaController). Não ligar RequireUniqueEmail aqui:
        // ele recusa qualquer alteração em usuários antigos que ainda não têm e-mail.
        options.User.RequireUniqueEmail = false;

        // Só entra quem confirmou o e-mail (os usuários antigos já estão marcados como confirmados).
        options.SignIn.RequireConfirmedEmail = true;
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

// Chaves de criptografia (cookie de login, formulários, links de e-mail) guardadas no banco,
// para sobreviverem a reinícios e atualizações do servidor. Ver Data/AuthDbContext.cs.
builder.Services.AddDataProtection()
    .SetApplicationName("FCFinance.Web")
    .PersistKeysToDbContext<AuthDbContext>();

// Hospedagem: proxy (HTTPS/IP reais) e verificação de saúde em /health. Ver Data/Producao.cs.
builder.Services.AddProxyConfiavel(builder.Configuration);
builder.Services.AddSaude();

// Confere a cada 1 minuto (em vez de 30) se o login ainda vale: depois de trocar a senha ou
// excluir o usuário, as outras sessões abertas caem em até 1 minuto.
builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.FromMinutes(1));

// Links enviados por e-mail (confirmar e-mail, redefinir senha, trocar e-mail) valem por 3 horas.
builder.Services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromHours(3));

// E-mails: em desenvolvimento são gravados numa pasta; em produção vão pela Brevo (SMTP). Ver Data/Email.
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddSingleton<IEmailSender, EmailSender>();

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

// Os arquivos de produção vêm com "TROCAR-..." onde falta o endereço real: não deixa publicar assim.
foreach (var chave in new[] { "Api:BaseUrl", "Email:Remetente" })
    if (builder.Configuration[chave]?.Contains("TROCAR", StringComparison.OrdinalIgnoreCase) == true)
        throw new InvalidOperationException($"Configure \"{chave}\" com o valor real (appsettings.Production.json ou variável de ambiente).");
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

// Primeiro de tudo: descobrir o HTTPS e o IP reais quando há um proxy na frente (hospedagem).
app.UseProxyConfiavel();

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
app.MapHealthChecks("/health");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
