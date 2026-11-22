using FCFinance.Auth;
using FCFinance.DTO;
using FCFinance.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FCFinance.Controllers
{
    // Cadastro de usuários pelo app. Só o perfil Admin pode usar.
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = Perfis.Admin)]
    public class UsuariosController : ControllerBase
    {
        private readonly UserManager<AppUser> _users;
        private readonly RoleManager<IdentityRole> _roles;
        private readonly AppDbContext _context;

        public UsuariosController(UserManager<AppUser> users, RoleManager<IdentityRole> roles, AppDbContext context)
        {
            _users = users;
            _roles = roles;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Listar()
        {
            var admins = (await _users.GetUsersInRoleAsync(Perfis.Admin)).Select(u => u.Id).ToHashSet();
            var lista = await _users.Users.OrderBy(u => u.Nome).ToListAsync();
            return Ok(lista.Select(u => AuthController.ParaDTO(u, admins.Contains(u.Id) ? new[] { Perfis.Admin } : new[] { Perfis.Usuario })));
        }

        [HttpPost]
        public async Task<IActionResult> Criar(NovoUsuarioDTO dto)
        {
            // Garante que o perfil existe antes de criar o usuário (num banco novo pode não existir).
            var perfil = dto.Admin ? Perfis.Admin : Perfis.Usuario;
            if (!await _roles.RoleExistsAsync(perfil))
            {
                var roleResult = await _roles.CreateAsync(new IdentityRole(perfil));
                if (!roleResult.Succeeded) return BadRequest(Erros.Traduzir(roleResult));
            }

            var user = new AppUser { UserName = dto.Login.Trim(), Nome = dto.Nome.Trim(), EmailConfirmed = true };
            var result = await _users.CreateAsync(user, dto.Senha);
            if (!result.Succeeded) return BadRequest(Erros.Traduzir(result));

            // Se não conseguir dar o perfil, desfaz: não deixa um usuário "sem perfil" no banco.
            var addRole = await _users.AddToRoleAsync(user, perfil);
            if (!addRole.Succeeded)
            {
                await _users.DeleteAsync(user);
                return BadRequest(Erros.Traduzir(addRole));
            }

            return Ok(AuthController.ParaDTO(user, new[] { perfil }));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Excluir(string id)
        {
            var user = await _users.FindByIdAsync(id);
            if (user == null) return NotFound("Usuário não encontrado.");
            if (user.Id == _users.GetUserId(User)) return Conflict("Você não pode excluir o seu próprio usuário.");
            if (await _users.IsInRoleAsync(user, Perfis.Admin) && (await _users.GetUsersInRoleAsync(Perfis.Admin)).Count <= 1)
                return Conflict("Não é possível excluir o último administrador.");

            var result = await _users.DeleteAsync(user);
            if (!result.Succeeded) return BadRequest(Erros.Traduzir(result));

            // Apaga os dados financeiros do usuário (lançamentos antes das categorias, por causa da FK).
            // Categorias do sistema (UserId = null) não são afetadas.
            await using var transaction = await _context.Database.BeginTransactionAsync();
            await _context.RegisteredValues.Where(x => x.UserId == id).ExecuteDeleteAsync();
            await _context.Goals.Where(x => x.UserId == id).ExecuteDeleteAsync();
            await _context.Categories.Where(x => x.UserId == id).ExecuteDeleteAsync();
            // WhatsApp: ligação do telefone, códigos pendentes e conversa do simulador.
            var phones = await _context.WhatsAppLinks.Where(x => x.UserId == id).Select(x => x.PhoneNumber).ToListAsync();
            phones.Add($"sim:{id}");
            await _context.ChatSessions.Where(x => phones.Contains(x.PhoneNumber)).ExecuteDeleteAsync();
            await _context.WhatsAppLinks.Where(x => x.UserId == id).ExecuteDeleteAsync();
            await _context.WhatsAppLinkCodes.Where(x => x.UserId == id).ExecuteDeleteAsync();
            await transaction.CommitAsync();

            return NoContent();
        }
    }
}
