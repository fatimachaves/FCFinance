using FCFinance.Web.Data;
using FCFinance.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FCFinance.Web.Controllers
{
    // Cadastro de usuários: só o perfil Admin entra aqui.
    [Authorize(Roles = Perfis.Admin)]
    public class UsuariosController : Controller
    {
        private readonly UserManager<AppUser> _users;

        public UsuariosController(UserManager<AppUser> users) => _users = users;

        [HttpGet]
        public async Task<IActionResult> Index() => View(await Montar(new NovoUsuarioViewModel()));

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Criar([Bind(Prefix = "Novo")] NovoUsuarioViewModel novo)
        {
            if (ModelState.IsValid)
            {
                var user = new AppUser { UserName = novo.Login.Trim(), Nome = novo.Nome.Trim(), EmailConfirmed = true };
                var result = await _users.CreateAsync(user, novo.Senha);
                if (result.Succeeded)
                {
                    await _users.AddToRoleAsync(user, novo.Admin ? Perfis.Admin : Perfis.Usuario);
                    TempData["Sucesso"] = $"Usuário \"{user.UserName}\" criado.";
                    return RedirectToAction(nameof(Index));
                }
                foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, ContaController.Traduzir(e));
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

            if (user.Id == _users.GetUserId(User))
                TempData["Erro"] = "Você não pode excluir o seu próprio usuário.";
            else if (await _users.IsInRoleAsync(user, Perfis.Admin) && (await _users.GetUsersInRoleAsync(Perfis.Admin)).Count <= 1)
                TempData["Erro"] = "Não é possível excluir o último administrador.";
            else
            {
                await _users.DeleteAsync(user);
                TempData["Sucesso"] = $"Usuário \"{user.UserName}\" excluído.";
            }
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
