using System.Text;
using System.Text.Json.Serialization;
using FCFinance.Auth;
using FCFinance.Models;
using FCFinance.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);
var connection = builder.Configuration.GetConnectionString("DefaultConnection");

// Todas as rotas exigem login (token), a não ser as marcadas com [AllowAnonymous] (ex.: api/Auth/login).
builder.Services.AddControllers(options =>
        options.Filters.Add(new AuthorizeFilter(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())))
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        // Erros de validação dos DTOs ([Required], [StringLength]...) voltam como texto simples,
        // no mesmo formato das outras mensagens da API, para o site mostrar direto ao usuário.
        options.InvalidModelStateResponseFactory = context =>
            new BadRequestObjectResult(string.Join(" ", context.ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Distinct()));
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Botão "Authorize" no Swagger: cole o token do api/Auth/login para testar as rotas protegidas.
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Cole aqui o token retornado por POST api/Auth/login (sem a palavra Bearer)."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

// Dados financeiros
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connection));
builder.Services.AddScoped<RegisteredValueService>();
builder.Services.AddScoped<UserDataService>();

// Usuário da requisição: usado pelos filtros globais do AppDbContext (cada um só vê os próprios dados).
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

// ---------- Login (mesmas tabelas AspNet* que o site usa) ----------
builder.Services.AddDbContext<AuthDbContext>(options => options.UseSqlServer(connection));
builder.Services
    .AddIdentityCore<AppUser>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.SignIn.RequireConfirmedEmail = true; // igual ao site: só entra quem confirmou o e-mail
        // Nomes das informações dentro do token
        options.ClaimsIdentity.UserIdClaimType = "sub";
        options.ClaimsIdentity.UserNameClaimType = "name";
        options.ClaimsIdentity.RoleClaimType = "role";
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AuthDbContext>()
    // Confere os códigos do app autenticador (verificação em duas etapas, ativada pelo site).
    .AddTokenProvider<AuthenticatorTokenProvider<AppUser>>(TokenOptions.DefaultAuthenticatorProvider)
    .AddSignInManager();

// ---------- Token JWT ----------
// A chave (Jwt:Key) NÃO fica no appsettings: em desenvolvimento ela vem de User Secrets e,
// em produção, de variável de ambiente (Jwt__Key). Tem que ser a MESMA no projeto Web.
// Para configurar numa máquina nova (rodar nas pastas dos dois projetos, com a mesma chave):
//   dotnet user-secrets set "Jwt:Key" "<chave com 32+ caracteres>"
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
    throw new InvalidOperationException("Configure Jwt:Key (mínimo 32 caracteres) via User Secrets ou variável de ambiente Jwt__Key.");
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddSingleton<TokenService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // mantém os nomes curtos: sub, name, nome, role
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            NameClaimType = "name",
            RoleClaimType = "role",
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        // Depois de conferir a assinatura do token, confere no banco se ele ainda deve valer
        // (senha trocada, usuário excluído, perfil retirado). Ver Auth/ApiTokenValidator.cs.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var validator = context.HttpContext.RequestServices.GetRequiredService<ApiTokenValidator>();
                var error = await validator.ValidateAsync(context.Principal!);
                if (error != null) context.Fail(error);
            }
        };
    });
builder.Services.AddScoped<ApiTokenValidator>();

// Limite de requisições (login por IP + limite geral por usuário). Ver Auth/ApiSecurity.cs.
builder.Services.AddApiRateLimits();

// Origens liberadas no CORS vêm do appsettings (seção "Cors:Origins").
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
if (corsOrigins.Length == 0)
    throw new InvalidOperationException("Configure Cors:Origins (lista de endereços do site) no appsettings.");

builder.Services.AddCors(options =>
{
    options.AddPolicy("WebFCFinance", policy =>
    {
        policy
            .WithOrigins(corsOrigins)
            .AllowAnyHeader()   // inclui o cabeçalho Authorization (token)
            .AllowAnyMethod()
            .WithExposedHeaders("X-Total-Count"); // total de itens na listagem paginada
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts(); // navegador passa a usar só HTTPS com a API
}

app.UseHttpsRedirection();
app.UseApiSecurityHeaders();

// A ordem importa: CORS antes do login, para o navegador conseguir fazer a "pré-consulta" (OPTIONS).
app.UseCors("WebFCFinance");
app.UseAuthentication(); // lê o token (e confere o carimbo de segurança)
app.UseRateLimiter();    // depois do login: o limite geral é por usuário
app.UseAuthorization();  // decide se pode acessar

app.MapControllers();

app.Run();
