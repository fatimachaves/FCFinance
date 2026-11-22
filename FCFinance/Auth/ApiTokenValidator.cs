using System.Security.Claims;
using Microsoft.AspNetCore.Identity;

namespace FCFinance.Auth
{
    // Confere, a CADA chamada à API, se o token ainda deve valer.
    //
    // Um token JWT é "autossuficiente": depois de emitido, ele vale até vencer. Para conseguir
    // cancelar acessos (troca de senha, usuário excluído, perfil retirado), todo token carrega o
    // "carimbo de segurança" (SecurityStamp) do usuário. O Identity troca esse carimbo sempre que
    // a senha muda; se o carimbo do token for diferente do que está no banco, o token é recusado.
    // Os perfis (Admin/Usuario) também são relidos do banco, então tirar o Admin vale na hora.
    public class ApiTokenValidator
    {
        // Nome do claim do carimbo dentro do token (o site usa o mesmo nome, ver FCFinance.Web/Data/ApiTokenService.cs).
        public const string SecurityStampClaim = "sstamp";

        private readonly UserManager<AppUser> _users;

        public ApiTokenValidator(UserManager<AppUser> users) => _users = users;

        // Retorna null se o token continua valendo; senão, o motivo da recusa.
        public async Task<string?> ValidateAsync(ClaimsPrincipal principal)
        {
            var userId = principal.FindFirstValue("sub");
            var stamp = principal.FindFirstValue(SecurityStampClaim);
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(stamp))
                return "Token sem identificação do usuário. Faça login de novo.";

            var user = await _users.FindByIdAsync(userId);
            if (user == null)
                return "Usuário não existe mais.";

            if (!string.Equals(user.SecurityStamp, stamp, StringComparison.Ordinal))
                return "Sessão encerrada: a senha foi alterada ou o acesso foi revogado. Faça login de novo.";

            // Perfis atuais do banco substituem os que vieram no token.
            if (principal.Identity is ClaimsIdentity identity)
            {
                foreach (var old in identity.FindAll(identity.RoleClaimType).ToList())
                    identity.RemoveClaim(old);
                foreach (var role in await _users.GetRolesAsync(user))
                    identity.AddClaim(new Claim(identity.RoleClaimType, role));
            }
            return null;
        }
    }
}
