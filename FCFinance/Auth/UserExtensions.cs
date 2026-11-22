using System.Security.Claims;

namespace FCFinance.Auth
{
    public static class UserExtensions
    {
        // Id do usuário logado (claim "sub" do token). Todas as rotas de dados exigem login,
        // então se chegar aqui sem o claim é erro de configuração do token.
        public static string GetUserId(this ClaimsPrincipal user) =>
            user.FindFirstValue("sub")
            ?? throw new InvalidOperationException("Token sem o claim 'sub' (id do usuário).");

        public static bool IsAdmin(this ClaimsPrincipal user) => user.IsInRole(Perfis.Admin);
    }
}
