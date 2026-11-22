using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FCFinance.Web.Data
{
    // Ajustes para rodar num servidor de verdade (hospedagem).
    public static class Producao
    {
        // ---------- Proxy ----------
        // Na hospedagem, um "proxy" (ex.: Azure App Service, Nginx) recebe o HTTPS e repassa para o site.
        // Sem isto, o site acha que todo acesso é HTTP e vem do IP do proxy: os links dos e-mails sairiam
        // com "http://" e o limite de tentativas por IP valeria para todos os clientes juntos.
        //
        // Só ligue (Proxy:ConfiarCabecalhos = true) quando o site estiver ATRÁS de um proxy confiável.
        // Ligado sem proxy, qualquer pessoa poderia falsificar o próprio IP e escapar do limite de tentativas.
        public static IServiceCollection AddProxyConfiavel(this IServiceCollection services, IConfiguration config)
        {
            if (!config.GetValue<bool>("Proxy:ConfiarCabecalhos")) return services;
            return services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.KnownIPNetworks.Clear(); // o proxy da hospedagem não tem IP fixo conhecido
                options.KnownProxies.Clear();
                options.ForwardLimit = 1;        // confia só no último salto (o proxy da hospedagem)
            });
        }

        public static WebApplication UseProxyConfiavel(this WebApplication app)
        {
            if (app.Configuration.GetValue<bool>("Proxy:ConfiarCabecalhos")) app.UseForwardedHeaders();
            return app;
        }

        // ---------- Saúde ----------
        // GET /health -> "Healthy" ou "Unhealthy". A hospedagem consulta para saber se o site está vivo
        // e consegue falar com o banco. Não mostra detalhes (nada de mensagens de erro para quem chamar).
        public static IServiceCollection AddSaude(this IServiceCollection services)
        {
            services.AddHealthChecks().AddCheck<BancoHealthCheck>("banco");
            return services;
        }

        private sealed class BancoHealthCheck(AuthDbContext db) : IHealthCheck
        {
            public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default) =>
                await db.Database.CanConnectAsync(ct) ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();
        }
    }
}
