using System.Text;
using FCFinance.Web.Data;
using FCFinance.Web.Data.Email;
using FCFinance.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using QRCoder;

namespace FCFinance.Web.Controllers
{
    // Tudo sobre a conta do usuário: login (com 2FA), cadastro, confirmação de e-mail, esqueci a senha,
    // minha conta (nome, e-mail, senha, 2FA) e exclusão da conta (LGPD).
    //
    // Regras de segurança seguidas aqui:
    //  - As telas públicas NUNCA revelam se um e-mail está cadastrado (a resposta é sempre a mesma).
    //  - Ações sensíveis têm limite de tentativas por IP (SecurityHeaders.LoginPolicy).
    //  - Mudanças importantes (senha, 2FA, exclusão) geram um e-mail de aviso.
    public class ContaController : Controller
    {
        private readonly SignInManager<AppUser> _signIn;
        private readonly UserManager<AppUser> _users;
        private readonly IEmailSender _email;
        private readonly ApiUsuariosClient _api;
        private readonly ILogger<ContaController> _logger;

        public ContaController(SignInManager<AppUser> signIn, UserManager<AppUser> users, IEmailSender email,
            ApiUsuariosClient api, ILogger<ContaController> logger)
        {
            _signIn = signIn;
            _users = users;
            _email = email;
            _api = api;
            _logger = logger;
        }

        // =====================================================================
        // LOGIN
        // =====================================================================

        [AllowAnonymous, HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true) return RedirectToLocal(returnUrl);
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        // POST /Conta/Login  (máx. 10 tentativas por minuto por IP)
        [AllowAnonymous, HttpPost, ValidateAntiForgeryToken, EnableRateLimiting(SecurityHeaders.LoginPolicy)]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await AcharPorLoginOuEmailAsync(model.Login);
            if (user == null) return LoginInvalido(model);

            // isPersistent = "Manter conectado": o cookie continua salvo mesmo fechando o navegador (até 14 dias sem uso).
            var result = await _signIn.PasswordSignInAsync(user, model.Senha, model.ManterConectado, lockoutOnFailure: true);

            if (result.Succeeded) return RedirectToLocal(model.ReturnUrl);
            if (result.RequiresTwoFactor)
                return RedirectToAction(nameof(LoginDoisFatores), new { returnUrl = model.ReturnUrl, manterConectado = model.ManterConectado });
            if (result.IsLockedOut)
            {
                ModelState.AddModelError(string.Empty, "Muitas tentativas erradas. Aguarde alguns minutos e tente de novo.");
                model.Senha = "";
                return View(model);
            }
            // NotAllowed = e-mail ainda não confirmado. Só avisa se a senha estiver certa (senão revelaria que a conta existe).
            if (result.IsNotAllowed && await _users.CheckPasswordAsync(user, model.Senha))
            {
                ModelState.AddModelError(string.Empty, "Confirme seu e-mail antes de entrar. Procure o link na sua caixa de entrada (e no spam).");
                ViewBag.ReenviarPara = user.Email;
                model.Senha = "";
                return View(model);
            }
            return LoginInvalido(model);
        }

        private IActionResult LoginInvalido(LoginViewModel model)
        {
            ModelState.AddModelError(string.Empty, "E-mail/usuário ou senha inválidos.");
            model.Senha = "";
            return View("Login", model);
        }

        // Segunda etapa do login (quem ativou a verificação em duas etapas).
        [AllowAnonymous, HttpGet]
        public async Task<IActionResult> LoginDoisFatores(string? returnUrl = null, bool manterConectado = false)
        {
            if (await _signIn.GetTwoFactorAuthenticationUserAsync() == null) return RedirectToAction(nameof(Login));
            return View(new LoginDoisFatoresViewModel { ReturnUrl = returnUrl, ManterConectado = manterConectado });
        }

        [AllowAnonymous, HttpPost, ValidateAntiForgeryToken, EnableRateLimiting(SecurityHeaders.LoginPolicy)]
        public async Task<IActionResult> LoginDoisFatores(LoginDoisFatoresViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            if (await _signIn.GetTwoFactorAuthenticationUserAsync() == null) return RedirectToAction(nameof(Login));

            var codigo = model.Codigo.Replace(" ", "").Replace("-", "");
            var result = model.UsarCodigoRecuperacao
                ? await _signIn.TwoFactorRecoveryCodeSignInAsync(codigo)
                : await _signIn.TwoFactorAuthenticatorSignInAsync(codigo, model.ManterConectado, model.LembrarEsteNavegador);

            if (result.Succeeded) return RedirectToLocal(model.ReturnUrl);
            ModelState.AddModelError(string.Empty, result.IsLockedOut
                ? "Muitas tentativas erradas. Aguarde alguns minutos e tente de novo."
                : "Código inválido. Confira o app autenticador (o código muda a cada 30 segundos).");
            model.Codigo = "";
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Sair()
        {
            await _signIn.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        public IActionResult AcessoNegado() => View();

        // =====================================================================
        // CADASTRO + CONFIRMAÇÃO DE E-MAIL
        // =====================================================================

        [AllowAnonymous, HttpGet]
        public IActionResult Cadastrar()
        {
            if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
            return View(new CadastroViewModel());
        }

        [AllowAnonymous, HttpPost, ValidateAntiForgeryToken, EnableRateLimiting(SecurityHeaders.LoginPolicy)]
        public async Task<IActionResult> Cadastrar(CadastroViewModel model)
        {
            if (!ModelState.IsValid) return View(LimparSenhas(model));

            // Valida a senha ANTES de olhar o e-mail: assim a resposta não revela se o e-mail já existe.
            foreach (var validator in _users.PasswordValidators)
            {
                var r = await validator.ValidateAsync(_users, new AppUser(), model.Senha);
                foreach (var e in r.Errors) ModelState.AddModelError(string.Empty, ContaTextos.Traduzir(e));
            }
            if (!ModelState.IsValid) return View(LimparSenhas(model));

            var email = model.Email.Trim();
            var existente = await _users.FindByEmailAsync(email) ?? await _users.FindByNameAsync(email);
            if (existente != null)
            {
                // Já tem conta: avisa por e-mail (só o dono do e-mail fica sabendo) e mostra a mesma tela de sempre.
                var aviso = EmailTemplates.ContaJaExiste(existente.Nome, LinkPara(nameof(Login)), LinkPara(nameof(EsqueciSenha)));
                await EnviarEmailAsync(email, aviso);
                return IrParaVerifiqueSeuEmail(email);
            }

            var agora = DateTime.UtcNow;
            var user = new AppUser
            {
                UserName = email,
                Email = email,
                Nome = model.Nome.Trim(),
                EmailConfirmed = false,
                TermosAceitosEm = agora,
                CriadoEm = agora
            };
            var created = await _users.CreateAsync(user, model.Senha);
            if (!created.Succeeded)
            {
                foreach (var e in created.Errors) ModelState.AddModelError(string.Empty, ContaTextos.Traduzir(e));
                return View(LimparSenhas(model));
            }
            var role = await _users.AddToRoleAsync(user, Perfis.Usuario);
            if (!role.Succeeded)
            {
                await _users.DeleteAsync(user);
                ModelState.AddModelError(string.Empty, "Não foi possível criar a conta agora. Tente de novo em instantes.");
                return View(LimparSenhas(model));
            }

            await EnviarConfirmacaoAsync(user);
            return IrParaVerifiqueSeuEmail(email);
        }

        [AllowAnonymous, HttpGet]
        public IActionResult VerifiqueSeuEmail() => View(new EmailViewModel { Email = TempData["EmailEnviado"] as string ?? "" });

        // Reenvia o link de confirmação. Resposta sempre igual (não revela se o e-mail existe).
        [AllowAnonymous, HttpPost, ValidateAntiForgeryToken, EnableRateLimiting(SecurityHeaders.LoginPolicy)]
        public async Task<IActionResult> ReenviarConfirmacao(EmailViewModel model)
        {
            if (!ModelState.IsValid) return View(nameof(VerifiqueSeuEmail), model);
            var user = await _users.FindByEmailAsync(model.Email.Trim());
            if (user != null && !user.EmailConfirmed) await EnviarConfirmacaoAsync(user);
            return IrParaVerifiqueSeuEmail(model.Email.Trim());
        }

        [AllowAnonymous, HttpGet]
        public async Task<IActionResult> ConfirmarEmail(string? userId, string? code)
        {
            var user = userId == null ? null : await _users.FindByIdAsync(userId);
            var token = Decodificar(code);
            var ok = user != null && token != null && (await _users.ConfirmEmailAsync(user, token)).Succeeded;
            return View(ok);
        }

        // =====================================================================
        // ESQUECI MINHA SENHA
        // =====================================================================

        [AllowAnonymous, HttpGet]
        public IActionResult EsqueciSenha() => View(new EmailViewModel());

        [AllowAnonymous, HttpPost, ValidateAntiForgeryToken, EnableRateLimiting(SecurityHeaders.LoginPolicy)]
        public async Task<IActionResult> EsqueciSenha(EmailViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _users.FindByEmailAsync(model.Email.Trim());
            if (user != null && user.EmailConfirmed)
            {
                var token = await _users.GeneratePasswordResetTokenAsync(user);
                var link = LinkPara(nameof(RedefinirSenha), new { userId = user.Id, code = Codificar(token) });
                await EnviarEmailAsync(user.Email!, EmailTemplates.RedefinirSenha(user.Nome, link));
            }
            // Mesma resposta sempre: não revela se o e-mail está cadastrado.
            TempData["Info"] = "Se houver uma conta com esse e-mail, enviamos um link para criar uma nova senha. Confira também o spam.";
            return RedirectToAction(nameof(EsqueciSenha));
        }

        [AllowAnonymous, HttpGet]
        public IActionResult RedefinirSenha(string? userId, string? code)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(code)) return RedirectToAction(nameof(EsqueciSenha));
            return View(new RedefinirSenhaViewModel { UserId = userId, Codigo = code });
        }

        [AllowAnonymous, HttpPost, ValidateAntiForgeryToken, EnableRateLimiting(SecurityHeaders.LoginPolicy)]
        public async Task<IActionResult> RedefinirSenha(RedefinirSenhaViewModel model)
        {
            if (!ModelState.IsValid) return View(LimparSenhas(model));

            var user = await _users.FindByIdAsync(model.UserId);
            var token = Decodificar(model.Codigo);
            if (user == null || token == null)
            {
                ModelState.AddModelError(string.Empty, "Link inválido ou vencido. Peça um novo em \"Esqueci minha senha\".");
                return View(LimparSenhas(model));
            }

            var result = await _users.ResetPasswordAsync(user, token, model.NovaSenha); // também troca o carimbo: derruba as outras sessões
            if (!result.Succeeded)
            {
                foreach (var e in result.Errors)
                    ModelState.AddModelError(string.Empty, e.Code == "InvalidToken"
                        ? "Link inválido ou vencido. Peça um novo em \"Esqueci minha senha\"."
                        : ContaTextos.Traduzir(e));
                return View(LimparSenhas(model));
            }

            // Quem estava bloqueado por errar a senha pode entrar com a nova.
            await _users.SetLockoutEndDateAsync(user, null);
            await _users.ResetAccessFailedCountAsync(user);

            await EnviarEmailAsync(user.Email!, EmailTemplates.SenhaAlterada(user.Nome, LinkPara(nameof(EsqueciSenha))));
            TempData["Sucesso"] = "Senha alterada! Entre com a nova senha.";
            return RedirectToAction(nameof(Login));
        }

        // =====================================================================
        // MINHA CONTA (logado)
        // =====================================================================

        [HttpGet]
        public async Task<IActionResult> MinhaConta()
        {
            var user = await _users.GetUserAsync(User);
            if (user == null) return RedirectToAction(nameof(Login));
            return View(await MontarMinhaContaAsync(user));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AlterarNome(MinhaContaViewModel model)
        {
            var user = await _users.GetUserAsync(User);
            if (user == null) return RedirectToAction(nameof(Login));
            if (!ModelState.IsValid)
            {
                var vm = await MontarMinhaContaAsync(user);
                vm.Nome = model.Nome;
                return View(nameof(MinhaConta), vm);
            }

            user.Nome = model.Nome.Trim();
            await _users.UpdateAsync(user);
            await _signIn.RefreshSignInAsync(user); // atualiza o "Olá, <nome>" do cookie
            TempData["Sucesso"] = "Nome atualizado.";
            return RedirectToAction(nameof(MinhaConta));
        }

        // Troca (ou cadastra) o e-mail: o link de confirmação vai para o NOVO endereço.
        [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting(SecurityHeaders.LoginPolicy)]
        public async Task<IActionResult> AlterarEmail(NovoEmailViewModel dados)
        {
            var user = await _users.GetUserAsync(User);
            if (user == null) return RedirectToAction(nameof(Login));

            var novo = dados.NovoEmail?.Trim() ?? "";
            if (!ModelState.IsValid)
                TempData["Erro"] = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage ?? "E-mail inválido.";
            else if (string.Equals(novo, user.Email, StringComparison.OrdinalIgnoreCase) && user.EmailConfirmed)
                TempData["Erro"] = "Esse já é o seu e-mail.";
            else if (await EmailEmUsoPorOutroAsync(novo, user.Id))
                TempData["Erro"] = "Esse e-mail já está em uso por outra conta.";
            else
            {
                var token = await _users.GenerateChangeEmailTokenAsync(user, novo);
                var link = LinkPara(nameof(ConfirmarNovoEmail), new { userId = user.Id, email = novo, code = Codificar(token) });
                await EnviarEmailAsync(novo, EmailTemplates.ConfirmarNovoEmail(user.Nome, link));
                TempData["Sucesso"] = $"Enviamos um link de confirmação para {novo}. O e-mail só muda depois que você clicar nele.";
            }
            return RedirectToAction(nameof(MinhaConta));
        }

        [AllowAnonymous, HttpGet]
        public async Task<IActionResult> ConfirmarNovoEmail(string? userId, string? email, string? code)
        {
            var user = userId == null ? null : await _users.FindByIdAsync(userId);
            var token = Decodificar(code);
            var ok = false;
            if (user != null && token != null && !string.IsNullOrWhiteSpace(email) && !await EmailEmUsoPorOutroAsync(email, user.Id))
            {
                var emailAntigo = user.Email;
                ok = (await _users.ChangeEmailAsync(user, email, token)).Succeeded; // confirma o e-mail e troca o carimbo
                // Quem se cadastrou pelo e-mail usa ele como login: o login acompanha a troca.
                if (ok && emailAntigo != null && string.Equals(user.UserName, emailAntigo, StringComparison.OrdinalIgnoreCase))
                    await _users.SetUserNameAsync(user, email);
                if (ok && _users.GetUserId(User) == user.Id) await _signIn.RefreshSignInAsync(user);
            }
            return View(ok);
        }

        [HttpGet]
        public IActionResult AlterarSenha() => View(new AlterarSenhaViewModel());

        [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting(SecurityHeaders.LoginPolicy)]
        public async Task<IActionResult> AlterarSenha(AlterarSenhaViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _users.GetUserAsync(User);
            if (user == null) return RedirectToAction(nameof(Login));

            var result = await _users.ChangePasswordAsync(user, model.SenhaAtual, model.NovaSenha);
            if (!result.Succeeded)
            {
                foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, ContaTextos.Traduzir(e));
                return View(new AlterarSenhaViewModel());
            }

            await _signIn.RefreshSignInAsync(user);
            if (user.EmailConfirmed && user.Email != null)
                await EnviarEmailAsync(user.Email, EmailTemplates.SenhaAlterada(user.Nome, LinkPara(nameof(EsqueciSenha))));
            TempData["Sucesso"] = "Senha alterada com sucesso. As outras sessões abertas foram encerradas.";
            return RedirectToAction(nameof(AlterarSenha));
        }

        // =====================================================================
        // VERIFICAÇÃO EM DUAS ETAPAS (app autenticador)
        // =====================================================================

        [HttpGet, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)] // a página mostra a chave secreta
        public async Task<IActionResult> AtivarDoisFatores()
        {
            var user = await _users.GetUserAsync(User);
            if (user == null) return RedirectToAction(nameof(Login));
            if (await _users.GetTwoFactorEnabledAsync(user)) return RedirectToAction(nameof(MinhaConta));
            return View(await MontarAtivacaoAsync(user));
        }

        [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting(SecurityHeaders.LoginPolicy)]
        public async Task<IActionResult> AtivarDoisFatores(AtivarDoisFatoresViewModel model)
        {
            var user = await _users.GetUserAsync(User);
            if (user == null) return RedirectToAction(nameof(Login));

            var codigo = (model.Codigo ?? "").Replace(" ", "").Replace("-", "");
            var valido = ModelState.IsValid && await _users.VerifyTwoFactorTokenAsync(
                user, _users.Options.Tokens.AuthenticatorTokenProvider, codigo);
            if (!valido)
            {
                ModelState.AddModelError(string.Empty, "Código inválido. Confira o horário do celular e digite o código atual do app.");
                return View(await MontarAtivacaoAsync(user));
            }

            await _users.SetTwoFactorEnabledAsync(user, true);
            var codigos = await _users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
            await _signIn.RefreshSignInAsync(user);
            if (user.EmailConfirmed && user.Email != null)
                await EnviarEmailAsync(user.Email, EmailTemplates.DoisFatoresAlterado(user.Nome, ativado: true));

            TempData["Codigos"] = string.Join(" ", codigos ?? []);
            return RedirectToAction(nameof(CodigosRecuperacao));
        }

        // Mostra os códigos de recuperação UMA vez (logo depois de gerados). NoStore: o navegador não guarda cópia.
        [HttpGet, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult CodigosRecuperacao()
        {
            if (TempData["Codigos"] is not string codigos) return RedirectToAction(nameof(MinhaConta));
            return View(codigos.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> GerarNovosCodigos()
        {
            var user = await _users.GetUserAsync(User);
            if (user == null) return RedirectToAction(nameof(Login));
            if (!await _users.GetTwoFactorEnabledAsync(user)) return RedirectToAction(nameof(MinhaConta));

            var codigos = await _users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
            TempData["Codigos"] = string.Join(" ", codigos ?? []);
            return RedirectToAction(nameof(CodigosRecuperacao));
        }

        // Desativar pede a senha: quem pegar o computador destravado não consegue tirar a proteção.
        [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting(SecurityHeaders.LoginPolicy)]
        public async Task<IActionResult> DesativarDoisFatores([Bind(Prefix = "Confirmar")] ExcluirContaViewModel model)
        {
            var user = await _users.GetUserAsync(User);
            if (user == null) return RedirectToAction(nameof(Login));
            if (!ModelState.IsValid || !await _users.CheckPasswordAsync(user, model.Senha))
            {
                TempData["Erro"] = "Senha incorreta. A verificação em duas etapas continua ativa.";
                return RedirectToAction(nameof(MinhaConta));
            }

            await _users.SetTwoFactorEnabledAsync(user, false);
            await _users.ResetAuthenticatorKeyAsync(user);
            await _signIn.ForgetTwoFactorClientAsync();
            await _signIn.RefreshSignInAsync(user);
            if (user.EmailConfirmed && user.Email != null)
                await EnviarEmailAsync(user.Email, EmailTemplates.DoisFatoresAlterado(user.Nome, ativado: false));
            TempData["Sucesso"] = "Verificação em duas etapas desativada.";
            return RedirectToAction(nameof(MinhaConta));
        }

        // =====================================================================
        // EXCLUIR MINHA CONTA (LGPD)
        // =====================================================================

        [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting(SecurityHeaders.LoginPolicy)]
        public async Task<IActionResult> ExcluirConta([Bind(Prefix = "Excluir")] ExcluirContaViewModel model)
        {
            var user = await _users.GetUserAsync(User);
            if (user == null) return RedirectToAction(nameof(Login));
            if (!ModelState.IsValid)
            {
                TempData["Erro"] = "Digite sua senha para confirmar a exclusão.";
                return RedirectToAction(nameof(MinhaConta));
            }

            var (email, nome, confirmado) = (user.Email, user.Nome, user.EmailConfirmed);
            var erro = await _api.ExcluirMinhaContaAsync(User, model.Senha); // a API confere a senha e apaga tudo
            if (erro != null)
            {
                TempData["Erro"] = erro;
                return RedirectToAction(nameof(MinhaConta));
            }

            await _signIn.SignOutAsync();
            if (confirmado && email != null) await EnviarEmailAsync(email, EmailTemplates.ContaExcluida(nome));
            TempData["Sucesso"] = "Sua conta e todos os seus dados foram excluídos.";
            return RedirectToAction(nameof(Login));
        }

        // =====================================================================
        // Ajudantes
        // =====================================================================

        private async Task<MinhaContaViewModel> MontarMinhaContaAsync(AppUser user)
        {
            var dois = await _users.GetTwoFactorEnabledAsync(user);
            var isAdmin = await _users.IsInRoleAsync(user, Perfis.Admin);
            return new MinhaContaViewModel
            {
                Nome = user.Nome,
                Login = user.UserName ?? "",
                EmailAtual = user.Email,
                EmailConfirmado = user.EmailConfirmed && user.Email != null,
                DoisFatoresAtivo = dois,
                CodigosRecuperacaoRestantes = dois ? await _users.CountRecoveryCodesAsync(user) : 0,
                UltimoAdmin = isAdmin && (await _users.GetUsersInRoleAsync(Perfis.Admin)).Count <= 1
            };
        }

        private async Task<AtivarDoisFatoresViewModel> MontarAtivacaoAsync(AppUser user)
        {
            var chave = await _users.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrEmpty(chave))
            {
                await _users.ResetAuthenticatorKeyAsync(user);
                chave = await _users.GetAuthenticatorKeyAsync(user);
            }

            // Formato que os apps autenticadores entendem (Google/Microsoft Authenticator, Authy...).
            var conta = Uri.EscapeDataString(user.Email ?? user.UserName ?? "conta");
            var uri = $"otpauth://totp/FCFinance:{conta}?secret={chave}&issuer=FCFinance&digits=6";
            using var qr = new QRCodeGenerator().CreateQrCode(uri, QRCodeGenerator.ECCLevel.Q);
            var png = new PngByteQRCode(qr).GetGraphic(6);

            return new AtivarDoisFatoresViewModel
            {
                ChaveFormatada = string.Join(" ", Enumerable.Range(0, (chave!.Length + 3) / 4)
                    .Select(i => chave.Substring(i * 4, Math.Min(4, chave.Length - i * 4)))).ToLowerInvariant(),
                QrCodeDataUri = "data:image/png;base64," + Convert.ToBase64String(png)
            };
        }

        private async Task<AppUser?> AcharPorLoginOuEmailAsync(string login)
        {
            login = login.Trim();
            return login.Contains('@')
                ? await _users.FindByEmailAsync(login) ?? await _users.FindByNameAsync(login)
                : await _users.FindByNameAsync(login);
        }

        private async Task<bool> EmailEmUsoPorOutroAsync(string email, string meuId)
        {
            var dono = await _users.FindByEmailAsync(email) ?? await _users.FindByNameAsync(email);
            return dono != null && dono.Id != meuId;
        }

        private async Task EnviarConfirmacaoAsync(AppUser user)
        {
            var token = await _users.GenerateEmailConfirmationTokenAsync(user);
            var link = LinkPara(nameof(ConfirmarEmail), new { userId = user.Id, code = Codificar(token) });
            await EnviarEmailAsync(user.Email!, EmailTemplates.ConfirmarEmail(user.Nome, link));
        }

        // Falha no envio não pode derrubar a tela (nem revelar nada): registra no log e segue.
        private async Task EnviarEmailAsync(string para, EmailTemplates.Email email)
        {
            try { await _email.EnviarAsync(para, email.Assunto, email.Html, email.Texto, HttpContext.RequestAborted); }
            catch (Exception ex) { _logger.LogError(ex, "Falha ao enviar o e-mail \"{Assunto}\".", email.Assunto); }
        }

        private IActionResult IrParaVerifiqueSeuEmail(string email)
        {
            TempData["EmailEnviado"] = email;
            return RedirectToAction(nameof(VerifiqueSeuEmail));
        }

        private string LinkPara(string action, object? values = null) =>
            Url.Action(action, "Conta", values, Request.Scheme)!;

        // Os códigos do Identity têm caracteres que quebram links: viajam em Base64Url.
        private static string Codificar(string token) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        private static string? Decodificar(string? code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            try { return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code)); }
            catch (FormatException) { return null; }
        }

        private static CadastroViewModel LimparSenhas(CadastroViewModel m) { m.Senha = ""; m.ConfirmarSenha = ""; return m; }
        private static RedefinirSenhaViewModel LimparSenhas(RedefinirSenhaViewModel m) { m.NovaSenha = ""; m.ConfirmarSenha = ""; return m; }

        private IActionResult RedirectToLocal(string? returnUrl) =>
            // Url.IsLocalUrl evita que um link malicioso mande a pessoa para outro site depois do login.
            !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? Redirect(returnUrl)
                : RedirectToAction("Index", "Home");
    }

    // Mensagens de erro do Identity em português (usadas também na tela Usuários).
    public static class ContaTextos
    {
        public static string Traduzir(IdentityError e) => e.Code switch
        {
            "PasswordMismatch" => "A senha atual está incorreta.",
            "PasswordTooShort" => "A senha precisa ter pelo menos 8 caracteres.",
            "PasswordRequiresDigit" => "A senha precisa ter pelo menos um número.",
            "PasswordRequiresLower" => "A senha precisa ter pelo menos uma letra minúscula.",
            "PasswordRequiresUpper" => "A senha precisa ter pelo menos uma letra maiúscula.",
            "DuplicateUserName" => "Já existe uma conta com esse login/e-mail.",
            "InvalidUserName" => "Login inválido.",
            "InvalidEmail" => "E-mail inválido.",
            _ => e.Description
        };
    }
}
