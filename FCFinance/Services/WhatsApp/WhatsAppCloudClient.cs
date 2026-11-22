using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace FCFinance.Services.WhatsApp
{
    // Configuração do WhatsApp (Meta Cloud API). Fica em User Secrets / variáveis de ambiente,
    // NUNCA no appsettings (são senhas). Veja o passo a passo em WHATSAPP.md.
    public class WhatsAppOptions
    {
        // Texto que você inventa e cola também no painel da Meta (verificação do webhook).
        public string VerifyToken { get; set; } = "";

        // "Chave secreta do aplicativo" (App Secret) da Meta: confere se a mensagem veio mesmo do WhatsApp.
        public string AppSecret { get; set; } = "";

        // Token de acesso e Id do número do bot (painel da Meta > WhatsApp > Configuração da API).
        public string AccessToken { get; set; } = "";
        public string PhoneNumberId { get; set; } = "";

        public string GraphApiVersion { get; set; } = "v21.0";

        // Celulares do Brasil: a Meta manda o número COM o 9 (55 11 9xxxx-xxxx), mas em muitas contas
        // só entrega a resposta se ela for enviada SEM o 9 (55 11 xxxx-xxxx). true = responde sem o 9.
        // (No modo de teste, cadastre na lista "Para" da Meta o número sem o 9.)
        public bool BrazilSendWithoutNinthDigit { get; set; } = true;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(VerifyToken) && !string.IsNullOrWhiteSpace(AppSecret)
            && !string.IsNullOrWhiteSpace(AccessToken) && !string.IsNullOrWhiteSpace(PhoneNumberId);
    }

    public interface IWhatsAppSender
    {
        Task SendTextAsync(string to, string text, CancellationToken ct = default);
    }

    // Envia mensagens de texto pela Meta Cloud API.
    public class WhatsAppCloudClient : IWhatsAppSender
    {
        private readonly HttpClient _http;
        private readonly WhatsAppOptions _options;
        private readonly ILogger<WhatsAppCloudClient> _logger;

        public WhatsAppCloudClient(HttpClient http, IOptions<WhatsAppOptions> options, ILogger<WhatsAppCloudClient> logger)
        {
            _http = http;
            _options = options.Value;
            _logger = logger;
        }

        // 5511912345678 -> 551112345678 (só celulares do Brasil: 55 + DDD + 9 + 8 dígitos).
        public static string ToBrazilWithoutNinthDigit(string phone) =>
            phone.Length == 13 && phone.StartsWith("55") && phone[4] == '9' && phone.All(char.IsDigit)
                ? phone[..4] + phone[5..]
                : phone;

        public async Task SendTextAsync(string to, string text, CancellationToken ct = default)
        {
            if (_options.BrazilSendWithoutNinthDigit) to = ToBrazilWithoutNinthDigit(to);

            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"https://graph.facebook.com/{_options.GraphApiVersion}/{_options.PhoneNumberId}/messages")
            {
                Content = JsonContent.Create(new
                {
                    messaging_product = "whatsapp",
                    to,
                    type = "text",
                    text = new { body = text, preview_url = false }
                })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);

            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                // Não loga o texto da mensagem (dado do usuário), só o erro da Meta.
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("WhatsApp: falha ao enviar mensagem ({Status}): {Body}", (int)response.StatusCode, body);
            }
        }

        // Confere o cabeçalho X-Hub-Signature-256 ("sha256=<hmac>") que a Meta envia em cada webhook.
        public static bool IsValidSignature(byte[] body, string? signatureHeader, string appSecret)
        {
            const string prefix = "sha256=";
            if (string.IsNullOrEmpty(signatureHeader) || !signatureHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;

            byte[] received;
            try { received = Convert.FromHexString(signatureHeader[prefix.Length..]); }
            catch (FormatException) { return false; }

            var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(appSecret), body);
            return CryptographicOperations.FixedTimeEquals(expected, received);
        }
    }
}
