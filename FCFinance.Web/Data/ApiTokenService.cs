using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FCFinance.Web.Data
{
    // A API agora exige login (token JWT). O JavaScript das páginas chama a API direto do navegador,
    // então o site gera um token para quem está logado e coloca na página (ver _Layout.cshtml).
    // Usa a mesma chave/emissor da API (seção "Jwt": Issuer/Audience no appsettings.Development.json, Key em User Secrets nos dois projetos).
    public class ApiTokenService
    {
        private readonly IConfiguration _config;

        public ApiTokenService(IConfiguration config) => _config = config;

        public string? CriarPara(ClaimsPrincipal user)
        {
            var key = _config["Jwt:Key"];
            if (user.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(key)) return null;

            var claims = new List<Claim>
            {
                new("sub", user.FindFirstValue(ClaimTypes.NameIdentifier) ?? ""),
                new("name", user.Identity.Name ?? ""),
                new("nome", user.Nome())
            };
            claims.AddRange(user.FindAll(ClaimTypes.Role).Select(r => new Claim("role", r.Value)));

            return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
            {
                Issuer = _config["Jwt:Issuer"] ?? "FCFinance",
                Audience = _config["Jwt:Audience"] ?? "FCFinance",
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(8), // ao recarregar a página, um novo token é gerado
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256)
            });
        }
    }
}
