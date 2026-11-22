using FCFinance.Web.Data;
using FCFinance.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FCFinance.Web.Controllers
{
    // Login, logout e troca de senha.
    public class ContaController : Controller
    {
        private readonly SignInManager<AppUser> _signIn;
        private readonly UserManager<AppUser> _users;

        public ContaController(SignInManager<AppUser> signIn, UserManager<AppUser> users)
        {
            _signIn = signIn;
            _users = users;
        }

        // GET /Conta/Login
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true) return RedirectToLocal(returnUrl);
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        // POST /Conta/Login
        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            // isPersistent = "Manter conectado":
            //   true  -> o cookie continua salvo mesmo fechando o navegador (até 14 dias sem uso);
            //   false -> o login acaba quando o navegador é fechado.
            var result = await _signIn.PasswordSignInAsync(model.Login.Trim(), model.Senha,
                isPersistent: model.ManterConectado, lockoutOnFailure: true);

            if (result.Succeeded) return RedirectToLocal(model.ReturnUrl);

            ModelState.AddModelError(string.Empty, result.IsLockedOut
                ? "Muitas tentativas erradas. Aguarde alguns minutos e tente de novo."
                : "Usuário ou senha inválidos.");
            model.Senha = "";
            return View(model);
        }

        // POST /Conta/Sair
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sair()
        {
            await _signIn.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        // GET /Conta/AcessoNegado
        [HttpGet]
        public IActionResult AcessoNegado() => View();

        // GET /Conta/AlterarSenha
        [HttpGet]
        public IActionResult AlterarSenha() => View(new AlterarSenhaViewModel());

        // POST /Conta/AlterarSenha
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AlterarSenha(AlterarSenhaViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _users.GetUserAsync(User);
            if (user == null) return RedirectToAction(nameof(Login));

            var result = await _users.ChangePasswordAsync(user, model.SenhaAtual, model.NovaSenha);
            if (!result.Succeeded)
            {
                foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, Traduzir(e));
                return View(new AlterarSenhaViewModel());
            }

            await _signIn.RefreshSignInAsync(user);
            TempData["Sucesso"] = "Senha alterada com sucesso.";
            return RedirectToAction(nameof(AlterarSenha));
        }

        private IActionResult RedirectToLocal(string? returnUrl) =>
            // Url.IsLocalUrl evita que um link malicioso mande a pessoa para outro site depois do login.
            !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? Redirect(returnUrl)
                : RedirectToAction("Index", "Home");

        // Traduz as mensagens de erro mais comuns do Identity.
        internal static string Traduzir(IdentityError e) => e.Code switch
        {
            "PasswordMismatch" => "A senha atual está incorreta.",
            "PasswordTooShort" => "A senha precisa ter pelo menos 8 caracteres.",
            "PasswordRequiresDigit" => "A senha precisa ter pelo menos um número.",
            "PasswordRequiresLower" => "A senha precisa ter pelo menos uma letra minúscula.",
            "PasswordRequiresUpper" => "A senha precisa ter pelo menos uma letra maiúscula.",
            "DuplicateUserName" => "Já existe um usuário com esse login.",
            "InvalidUserName" => "Login inválido.",
            _ => e.Description
        };
    }
}
