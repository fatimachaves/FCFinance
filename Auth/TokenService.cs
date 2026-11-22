using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FCFinance.Auth
{
    // Configuração do token (vem da seção "Jwt" do appsettings).
    public class JwtOptions
    {
        public string Issuer { get; set; } = "FCFinance";
        public string Audience { get; set; } = "FCFinance";
        public string Key { get; set; } = "";
    }

    // Cria o "token" (JWT): um texto assinado que diz quem é o usuário e qual o perfil dele.
    // O app e o site mandam esse token em toda chamada: Authorization: Bearer <token>.
    public class TokenService
    {
        private readonly JwtOptions _options;

        public TokenService(Microsoft.Extensions.Options.IOptions<JwtOptions> options) => _options = options.Value;

        public (string Token, DateTime ExpiraEm) Criar(AppUser user, IEnumerable<string> perfis, TimeSpan validade)
        {
            var claims = new List<Claim>
            {
                new("sub", user.Id),
                new("name", user.UserName ?? ""),
                new("nome", string.IsNullOrWhiteSpace(user.Nome) ? user.UserName ?? "" : user.Nome)
            };
            claims.AddRange(perfis.Select(p => new Claim("role", p)));

            var expira = DateTime.UtcNow.Add(validade);
            var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
            {
                Issuer = _options.Issuer,
                Audience = _options.Audience,
                Subject = new ClaimsIdentity(claims),
                Expires = expira,
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key)), SecurityAlgorithms.HmacSha256)
            });
            return (token, expira);
        }
    }
}
