using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using FCFinance.Web.Models;

namespace FCFinance.Web.Data
{
    // Cria e exclui usuários chamando a API (api/Usuarios), em vez de mexer direto no banco.
    // Assim as regras ficam num lugar só: ao excluir, a API também apaga os lançamentos,
    // metas e categorias do usuário; ao criar, garante que ele recebeu um perfil.
    // A chamada usa um token do admin logado (mesmo token que o JavaScript usa).
    public class ApiUsuariosClient
    {
        private readonly HttpClient _http;
        private readonly ApiTokenService _tokens;

        public ApiUsuariosClient(HttpClient http, ApiTokenService tokens)
        {
            _http = http;
            _tokens = tokens;
        }

        // Retorna null se deu certo, ou a mensagem de erro para mostrar na tela.
        public Task<string?> CriarAsync(ClaimsPrincipal admin, NovoUsuarioViewModel novo) =>
            EnviarAsync(admin, HttpMethod.Post, "api/Usuarios", JsonContent.Create(new
            {
                nome = novo.Nome.Trim(),
                login = novo.Login.Trim(),
                senha = novo.Senha,
                admin = novo.Admin
            }));

        public Task<string?> ExcluirAsync(ClaimsPrincipal admin, string id) =>
            EnviarAsync(admin, HttpMethod.Delete, $"api/Usuarios/{Uri.EscapeDataString(id)}", null);

        private async Task<string?> EnviarAsync(ClaimsPrincipal admin, HttpMethod method, string url, HttpContent? body)
        {
            var token = _tokens.CriarPara(admin);
            if (token == null) return "Não foi possível gerar o acesso à API (verifique a configuração Jwt:Key).";

            using var request = new HttpRequestMessage(method, url) { Content = body };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            try
            {
                using var response = await _http.SendAsync(request);
                if (response.IsSuccessStatusCode) return null;

                // A API devolve mensagens curtas em texto (ex.: "Não é possível excluir o último administrador.").
                var text = (await response.Content.ReadAsStringAsync()).Trim().Trim('"');
                return !string.IsNullOrWhiteSpace(text) && !text.StartsWith('{') && text.Length < 300
                    ? text
                    : $"A API recusou a operação (erro {(int)response.StatusCode}).";
            }
            catch (HttpRequestException)
            {
                return "Não foi possível falar com a API. Confirme se ela está rodando.";
            }
            catch (TaskCanceledException)
            {
                return "A API demorou demais para responder. Tente de novo.";
            }
        }
    }
}
