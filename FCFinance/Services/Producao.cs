using FCFinance.Models;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FCFinance.Services
{
    // Ajustes para rodar num servidor de verdade (hospedagem). Mesmo padrão do site (FCFinance.Web/Data/Producao.cs).
    public static class Producao
    {
        // ---------- Proxy ----------
        // Na hospedagem, um proxy recebe o HTTPS e repassa para a API. Sem isto, todos os clientes
        // aparecem com o IP do proxy e o limite de requisições por IP vale para todos juntos.
        // Só ligue (Proxy:ConfiarCabecalhos = true) quando a API estiver ATRÁS de um proxy confiável:
        // sem proxy, qualquer pessoa poderia falsificar o próprio IP e escapar do limite.
        public static IServiceCollection AddProxyConfiavel(this IServiceCollection services, IConfiguration config)
        {
            if (!config.GetValue<bool>("Proxy:ConfiarCabecalhos")) return services;
            return services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
                options.ForwardLimit = 1;
            });
        }

        public static WebApplication UseProxyConfiavel(this WebApplication app)
        {
            if (app.Configuration.GetValue<bool>("Proxy:ConfiarCabecalhos")) app.UseForwardedHeaders();
            return app;
        }

        // ---------- Saúde ----------
        // GET /health -> "Healthy" ou "Unhealthy" (sem detalhes). A hospedagem usa para saber se a API está viva.
        public static IServiceCollection AddSaude(this IServiceCollection services)
        {
            services.AddHealthChecks().AddCheck<BancoHealthCheck>("banco");
            return services;
        }

        private sealed class BancoHealthCheck(AppDbContext db) : IHealthCheck
        {
            public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default) =>
                await db.Database.CanConnectAsync(ct) ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();
        }
    }
}
