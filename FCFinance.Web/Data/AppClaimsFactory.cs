using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace FCFinance.Web.Data
{
    // Quando a pessoa faz login, o Identity monta um "crachá" (ClaimsPrincipal) que fica no cookie.
    // Aqui acrescentamos o Nome nesse crachá, para as telas mostrarem "Olá, <nome>!" sem ir ao banco.
    public class AppClaimsFactory : UserClaimsPrincipalFactory<AppUser, IdentityRole>
    {
        public const string NomeClaim = "nome";

        public AppClaimsFactory(UserManager<AppUser> userManager, RoleManager<IdentityRole> roleManager,
            IOptions<IdentityOptions> options) : base(userManager, roleManager, options) { }

        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
        {
            var identity = await base.GenerateClaimsAsync(user);
            identity.AddClaim(new Claim(NomeClaim, string.IsNullOrWhiteSpace(user.Nome) ? user.UserName ?? "" : user.Nome));
            return identity;
        }
    }

    public static class UserExtensions
    {
        // Uso nas views: @User.Nome()
        public static string Nome(this ClaimsPrincipal user) =>
            user.FindFirstValue(AppClaimsFactory.NomeClaim) ?? user.Identity?.Name ?? "";
    }
}
