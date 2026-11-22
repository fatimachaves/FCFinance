using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace FCFinance.Auth
{
    // Proteções gerais da API: limite de requisições e cabeçalhos de segurança.
    public static class ApiSecurity
    {
        public const string LoginPolicy = "login";

        // Limites:
        //  - login: 10 tentativas por minuto por IP (além do bloqueio de 5 senhas erradas por usuário);
        //  - geral: 300 chamadas por minuto por usuário (ou por IP, sem login). Evita abuso e robôs.
        public static IServiceCollection AddApiRateLimits(this IServiceCollection services) =>
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.OnRejected = async (context, ct) =>
                {
                    context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
                    await context.HttpContext.Response.WriteAsync("Muitas requisições. Aguarde um minuto e tente de novo.", ct);
                };

                options.AddPolicy(LoginPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
                    ClientIp(http),
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http => RateLimitPartition.GetFixedWindowLimiter(
                    http.User.FindFirst("sub")?.Value is { } userId ? $"user:{userId}" : $"ip:{ClientIp(http)}",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = 300, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            });

        // IP de quem chamou. Atenção ao publicar atrás de um proxy/balanceador: configure
        // UseForwardedHeaders, senão todos os clientes aparecem com o IP do proxy.
        private static string ClientIp(HttpContext http) => http.Connection.RemoteIpAddress?.ToString() ?? "desconhecido";

        // Cabeçalhos de segurança. A API só devolve dados (JSON/texto), então a política é a mais fechada possível.
        // O Swagger (só em desenvolvimento) precisa carregar scripts, por isso fica de fora.
        public static IApplicationBuilder UseApiSecurityHeaders(this IApplicationBuilder app) =>
            app.Use(async (context, next) =>
            {
                var h = context.Response.Headers;
                h["X-Content-Type-Options"] = "nosniff";
                h["X-Frame-Options"] = "DENY";
                h["Referrer-Policy"] = "no-referrer";
                if (!context.Request.Path.StartsWithSegments("/swagger"))
                {
                    h["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
                    // Dados financeiros: navegadores e proxies não devem guardar cópia das respostas.
                    h["Cache-Control"] = "no-store";
                }
                await next();
            });
    }
}
