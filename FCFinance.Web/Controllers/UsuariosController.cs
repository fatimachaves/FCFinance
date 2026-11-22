using FCFinance.Web.Data;
using FCFinance.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FCFinance.Web.Controllers
{
    // Cadastro de usuários: só o perfil Admin entra aqui.
    // A lista é lida direto do banco; criar e excluir passam pela API (ver ApiUsuariosClient),
    // que aplica as regras (perfil obrigatório, último admin, apagar os dados do usuário excluído).
    [Authorize(Roles = Perfis.Admin)]
    public class UsuariosController : Controller
    {
        private readonly UserManager<AppUser> _users;
        private readonly ApiUsuariosClient _api;

        public UsuariosController(UserManager<AppUser> users, ApiUsuariosClient api)
        {
            _users = users;
            _api = api;
        }

        [HttpGet]
        public async Task<IActionResult> Index() => View(await Montar(new NovoUsuarioViewModel()));

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Criar([Bind(Prefix = "Novo")] NovoUsuarioViewModel novo)
        {
            if (ModelState.IsValid)
            {
                var erro = await _api.CriarAsync(User, novo);
                if (erro == null)
                {
                    TempData["Sucesso"] = $"Usuário \"{novo.Login.Trim()}\" criado.";
                    return RedirectToAction(nameof(Index));
                }
                ModelState.AddModelError(string.Empty, erro);
            }
            novo.Senha = "";
            return View("Index", await Montar(novo));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Excluir(string id)
        {
            var user = await _users.FindByIdAsync(id);
            if (user == null) return RedirectToAction(nameof(Index));

            var erro = await _api.ExcluirAsync(User, id);
            if (erro == null) TempData["Sucesso"] = $"Usuário \"{user.UserName}\" e os dados dele foram excluídos.";
            else TempData["Erro"] = erro;

            return RedirectToAction(nameof(Index));
        }

        private async Task<UsuariosViewModel> Montar(NovoUsuarioViewModel novo)
        {
            var admins = (await _users.GetUsersInRoleAsync(Perfis.Admin)).Select(u => u.Id).ToHashSet();
            var lista = await _users.Users.OrderBy(u => u.Nome).ToListAsync();
            return new UsuariosViewModel
            {
                Novo = novo,
                Usuarios = lista.Select(u => new UsuarioListaItem
                {
                    Id = u.Id, Nome = u.Nome, Login = u.UserName ?? "", Admin = admins.Contains(u.Id)
                }).ToList()
            };
        }
    }
}
