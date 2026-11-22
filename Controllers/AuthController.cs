using FCFinance.Auth;
using FCFinance.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FCFinance.Controllers
{
    // Login do app (e de quem mais usar a API). Usa os mesmos usuários do site.
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<AppUser> _users;
        private readonly SignInManager<AppUser> _signIn;
        private readonly TokenService _tokens;

        public AuthController(UserManager<AppUser> users, SignInManager<AppUser> signIn, TokenService tokens)
        {
            _users = users;
            _signIn = signIn;
            _tokens = tokens;
        }

        // POST api/Auth/login  { "login": "admin", "senha": "...", "manterConectado": true }
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDTO dto)
        {
            var user = await _users.FindByNameAsync(dto.Login.Trim());
            if (user == null) return Unauthorized("Usuário ou senha inválidos.");

            // Confere a senha e conta tentativas erradas (bloqueia após 5, igual ao site).
            var result = await _signIn.CheckPasswordSignInAsync(user, dto.Senha, lockoutOnFailure: true);
            if (result.IsLockedOut) return Unauthorized("Muitas tentativas erradas. Aguarde alguns minutos e tente de novo.");
            if (!result.Succeeded) return Unauthorized("Usuário ou senha inválidos.");

            var perfis = await _users.GetRolesAsync(user);
            var (token, expira) = _tokens.Criar(user, perfis,
                dto.ManterConectado ? TimeSpan.FromDays(30) : TimeSpan.FromHours(12));

            return Ok(new LoginRespostaDTO { Token = token, ExpiraEm = expira, Usuario = ParaDTO(user, perfis) });
        }

        // GET api/Auth/eu  -> dados de quem está logado (o app usa para conferir se o token ainda vale)
        [HttpGet("eu")]
        public async Task<IActionResult> Eu()
        {
            var user = await _users.GetUserAsync(User);
            if (user == null) return Unauthorized();
            return Ok(ParaDTO(user, await _users.GetRolesAsync(user)));
        }

        // POST api/Auth/alterar-senha
        [HttpPost("alterar-senha")]
        public async Task<IActionResult> AlterarSenha(AlterarSenhaDTO dto)
        {
            var user = await _users.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var result = await _users.ChangePasswordAsync(user, dto.SenhaAtual, dto.NovaSenha);
            if (!result.Succeeded) return BadRequest(Erros.Traduzir(result));
            return NoContent();
        }

        internal static UsuarioDTO ParaDTO(AppUser u, IList<string> perfis) => new()
        {
            Id = u.Id,
            Nome = string.IsNullOrWhiteSpace(u.Nome) ? u.UserName ?? "" : u.Nome,
            Login = u.UserName ?? "",
            Admin = perfis.Contains(Perfis.Admin)
        };
    }

    // Mensagens do Identity em português (mesmas do site).
    internal static class Erros
    {
        public static string Traduzir(IdentityResult r) => string.Join(" ", r.Errors.Select(e => e.Code switch
        {
            "PasswordMismatch" => "A senha atual está incorreta.",
            "PasswordTooShort" => "A senha precisa ter pelo menos 8 caracteres.",
            "PasswordRequiresDigit" => "A senha precisa ter pelo menos um número.",
            "PasswordRequiresLower" => "A senha precisa ter pelo menos uma letra minúscula.",
            "PasswordRequiresUpper" => "A senha precisa ter pelo menos uma letra maiúscula.",
            "DuplicateUserName" => "Já existe um usuário com esse login.",
            "InvalidUserName" => "Login inválido.",
            _ => e.Description
        }));
    }
}
