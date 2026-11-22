namespace FCFinance.Web.Data
{
    // Cabeçalhos de segurança enviados em todas as respostas do site.
    //
    // O mais importante é a CSP (Content-Security-Policy): ela diz ao navegador de onde a página pode
    // carregar scripts, estilos, fontes e para onde pode fazer chamadas. Se um dia alguém conseguir
    // injetar um <script> numa página (XSS), o navegador NÃO executa, e o token da API não é roubado.
    //
    // Regras para continuar funcionando com a CSP:
    //  - JavaScript só em arquivos .js (nada de <script> com código dentro, nem onclick="...").
    //  - Novos domínios externos (CDN, analytics, gateway de pagamento) precisam ser incluídos aqui.
    public class SecurityHeaders
    {
        public const string LoginPolicy = "login"; // nome da regra de limite de tentativas do login

        private readonly RequestDelegate _next;
        private readonly string _csp;

        public SecurityHeaders(RequestDelegate next, IConfiguration config, IWebHostEnvironment env)
        {
            _next = next;

            // A página chama a API em outro endereço (Api:BaseUrl): precisa estar liberado em connect-src.
            var apiOrigin = new Uri(config["Api:BaseUrl"] ?? throw new InvalidOperationException("Configure Api:BaseUrl.")).GetLeftPart(UriPartial.Authority);

            // Em desenvolvimento, o Visual Studio injeta scripts de "Hot Reload"/"Browser Link" (localhost e websockets).
            var devScripts = env.IsDevelopment() ? " http://localhost:* https://localhost:*" : "";
            var devConnect = env.IsDevelopment() ? " ws: wss: http://localhost:* https://localhost:*" : "";

            _csp = string.Join("; ",
                "default-src 'self'",
                $"script-src 'self'{devScripts}",
                // 'unsafe-inline' em estilos: as telas usam style="width:..%" (barras de progresso). Estilo não executa código.
                "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com",
                "font-src 'self' https://fonts.gstatic.com",
                "img-src 'self' data:",
                $"connect-src 'self' {apiOrigin}{devConnect}",
                "object-src 'none'",
                "base-uri 'self'",
                "form-action 'self'",
                "frame-ancestors 'none'");
        }

        public Task InvokeAsync(HttpContext context)
        {
            var h = context.Response.Headers;
            h["Content-Security-Policy"] = _csp;
            h["X-Content-Type-Options"] = "nosniff";                   // não "adivinhar" o tipo dos arquivos
            h["X-Frame-Options"] = "DENY";                             // ninguém coloca o site dentro de um iframe (clickjacking)
            h["Referrer-Policy"] = "strict-origin-when-cross-origin";   // não vaza o endereço completo das páginas
            h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
            return _next(context);
        }
    }
}
