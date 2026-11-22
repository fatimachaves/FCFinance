using System.Text.Json;
using FCFinance.Auth;
using FCFinance.DTO;
using FCFinance.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FCFinance.Controllers
{
    // Direitos do titular (LGPD) sobre a própria conta: baixar uma cópia dos dados e excluir a conta.
    [ApiController]
    [Route("api/[controller]")]
    public class MinhaContaController : ControllerBase
    {
        private readonly UserManager<AppUser> _users;
        private readonly UserDataService _data;

        public MinhaContaController(UserManager<AppUser> users, UserDataService data)
        {
            _users = users;
            _data = data;
        }

        // GET api/MinhaConta/exportar -> arquivo JSON com o perfil e todos os dados financeiros
        [HttpGet("exportar")]
        public async Task<IActionResult> Exportar()
        {
            var user = await _users.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var export = new
            {
                geradoEm = DateTime.UtcNow,
                perfil = new
                {
                    nome = user.Nome,
                    login = user.UserName,
                    email = user.Email,
                    emailConfirmado = user.EmailConfirmed,
                    verificacaoEmDuasEtapas = user.TwoFactorEnabled,
                    contaCriadaEm = user.CriadoEm,
                    termosAceitosEm = user.TermosAceitosEm,
                    perfis = await _users.GetRolesAsync(user)
                },
                dados = await _data.ExportAsync()
            };

            var json = JsonSerializer.SerializeToUtf8Bytes(export, new JsonSerializerOptions { WriteIndented = true });
            return File(json, "application/json", $"fcfinance-meus-dados-{DateTime.UtcNow:yyyy-MM-dd}.json");
        }

        // POST api/MinhaConta/excluir { "senha": "..." } -> apaga a conta e TODOS os dados do usuário
        [HttpPost("excluir")]
        [EnableRateLimiting(ApiSecurity.LoginPolicy)] // a senha é conferida aqui: limita tentativas
        public async Task<IActionResult> Excluir(ExcluirMinhaContaDTO dto)
        {
            var user = await _users.GetUserAsync(User);
            if (user == null) return Unauthorized();

            if (!await _users.CheckPasswordAsync(user, dto.Senha))
            {
                await _users.AccessFailedAsync(user);
                return BadRequest("Senha incorreta. Sua conta não foi excluída.");
            }
            if (await _users.IsInRoleAsync(user, Perfis.Admin) && (await _users.GetUsersInRoleAsync(Perfis.Admin)).Count <= 1)
                return Conflict("Você é o único administrador. Torne outra pessoa administradora antes de excluir sua conta.");

            // Primeiro os dados, depois o usuário.
            await _data.DeleteAllAsync(user.Id);
            var result = await _users.DeleteAsync(user);
            if (!result.Succeeded) return BadRequest(Erros.Traduzir(result));

            return NoContent();
        }
    }
}
