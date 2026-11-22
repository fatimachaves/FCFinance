using System.Text.Json;
using FCFinance.Auth;
using FCFinance.DTO;
using FCFinance.Models;
using FCFinance.Services.ChatBot;
using FCFinance.Services.WhatsApp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FCFinance.Controllers
{
    // Bot do WhatsApp.
    //  - webhook: a Meta chama aqui a cada mensagem recebida (sem login, protegido pela assinatura da Meta);
    //  - status / link-code / link: tela "WhatsApp" do site (conectar e desconectar o telefone);
    //  - simulate: simulador de conversa do site (testa o bot sem WhatsApp).
    [ApiController]
    [Route("api/[controller]")]
    public class WhatsAppController : ControllerBase
    {
        private readonly ChatBotService _bot;
        private readonly AppDbContext _db;
        private readonly IWhatsAppSender _sender;
        private readonly WhatsAppOptions _options;
        private readonly TimeProvider _clock;
        private readonly ILogger<WhatsAppController> _logger;

        public WhatsAppController(ChatBotService bot, AppDbContext db, IWhatsAppSender sender,
            IOptions<WhatsAppOptions> options, TimeProvider clock, ILogger<WhatsAppController> logger)
        {
            _bot = bot;
            _db = db;
            _sender = sender;
            _options = options.Value;
            _clock = clock;
            _logger = logger;
        }

        // ================== Tela do site ==================

        // GET api/WhatsApp/status
        [HttpGet("status")]
        public async Task<IActionResult> Status()
        {
            var link = await _bot.GetLinkAsync(User.GetUserId());
            return Ok(new WhatsAppStatusDTO
            {
                Connected = link != null,
                Phone = link == null ? null : MaskPhone(link.PhoneNumber),
                LinkedAt = link?.LinkedAt,
                BotConfigured = _options.IsConfigured
            });
        }

        // POST api/WhatsApp/link-code -> código de 6 números para enviar ao bot
        [HttpPost("link-code")]
        public async Task<IActionResult> CreateLinkCode()
        {
            var code = await _bot.CreateLinkCodeAsync(User.GetUserId());
            return Ok(new WhatsAppLinkCodeDTO { Code = code.Code, ExpiresAt = code.ExpiresAt });
        }

        // DELETE api/WhatsApp/link -> desconecta o telefone da conta
        [HttpDelete("link")]
        public async Task<IActionResult> Unlink()
        {
            await _bot.UnlinkAsync(User.GetUserId());
            return NoContent();
        }

        // POST api/WhatsApp/simulate { "text": "45,90" } -> resposta do bot (sem WhatsApp)
        [HttpPost("simulate")]
        public async Task<IActionResult> Simulate(ChatSimulateDTO dto)
        {
            var userId = User.GetUserId();
            var reply = await _bot.HandleAsync($"sim:{userId}", dto.Text, simulatedUserId: userId);
            return Ok(new ChatReplyDTO { Reply = reply });
        }

        // ================== Webhook da Meta ==================

        // GET api/WhatsApp/webhook -> a Meta confere o endereço uma vez, ao configurar o webhook.
        [AllowAnonymous]
        [HttpGet("webhook")]
        public IActionResult VerifyWebhook(
            [FromQuery(Name = "hub.mode")] string? mode,
            [FromQuery(Name = "hub.verify_token")] string? token,
            [FromQuery(Name = "hub.challenge")] string? challenge)
        {
            if (!_options.IsConfigured) return NotFound();
            if (mode == "subscribe" && token == _options.VerifyToken && challenge != null)
                return Content(challenge, "text/plain");
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        // POST api/WhatsApp/webhook -> mensagens recebidas
        [AllowAnonymous]
        [HttpPost("webhook")]
        public async Task<IActionResult> ReceiveWebhook(CancellationToken ct)
        {
            if (!_options.IsConfigured) return NotFound();

            // Lê o corpo "cru" para conferir a assinatura (prova de que veio da Meta).
            using var buffer = new MemoryStream();
            await Request.Body.CopyToAsync(buffer, ct);
            var body = buffer.ToArray();
            if (!WhatsAppCloudClient.IsValidSignature(body, Request.Headers["X-Hub-Signature-256"], _options.AppSecret))
            {
                _logger.LogWarning("WhatsApp: webhook com assinatura inválida recusado.");
                return Unauthorized();
            }

            // A Meta também avisa o resultado da entrega das respostas do bot (enviada, entregue, lida, falhou).
            // Só as falhas interessam: ficam no log com o motivo (ex.: número fora da lista de teste).
            foreach (var failure in ReadDeliveryFailures(body))
                _logger.LogWarning("WhatsApp: entrega FALHOU para ...{Final}: {Erro}", failure.RecipientEnd, failure.Reason);

            foreach (var message in ReadMessages(body))
            {
                try
                {
                    await HandleIncomingAsync(message, ct);
                }
                catch (Exception ex)
                {
                    // Responde 200 mesmo assim: se não, a Meta reenvia a mesma mensagem várias vezes.
                    _logger.LogError(ex, "WhatsApp: erro ao processar a mensagem {MessageId}.", message.Id);
                }
            }
            return Ok();
        }

        private async Task HandleIncomingAsync(IncomingMessage message, CancellationToken ct)
        {
            // A Meta pode entregar a mesma mensagem mais de uma vez: processa só a primeira.
            if (await _db.ProcessedChatMessages.AnyAsync(m => m.MessageId == message.Id, ct)) return;
            var now = _clock.GetUtcNow().UtcDateTime;
            _db.ProcessedChatMessages.Add(new ProcessedChatMessage { MessageId = message.Id, ReceivedAt = now });
            await _db.SaveChangesAsync(ct);
            await _db.ProcessedChatMessages.Where(m => m.ReceivedAt < now.AddDays(-3)).ExecuteDeleteAsync(ct);

            var reply = message.Text == null
                ? "Por enquanto eu só entendo mensagens de *texto*. ✍️"
                : await _bot.HandleAsync(message.From, message.Text);

            await _sender.SendTextAsync(message.From, reply, ct);
        }

        private record IncomingMessage(string Id, string From, string? Text);

        // Formato da Meta: entry[].changes[].value.messages[] { id, from, type, text: { body } }
        private static IEnumerable<IncomingMessage> ReadMessages(byte[] body)
        {
            var result = new List<IncomingMessage>();
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (!doc.RootElement.TryGetProperty("entry", out var entries)) return result;
                foreach (var entry in entries.EnumerateArray())
                {
                    if (!entry.TryGetProperty("changes", out var changes)) continue;
                    foreach (var change in changes.EnumerateArray())
                    {
                        if (!change.TryGetProperty("value", out var value) || !value.TryGetProperty("messages", out var messages)) continue;
                        foreach (var m in messages.EnumerateArray())
                        {
                            var id = m.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                            var from = m.TryGetProperty("from", out var fromEl) ? fromEl.GetString() : null;
                            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(from)) continue;

                            string? text = null;
                            if (m.TryGetProperty("type", out var type) && type.GetString() == "text"
                                && m.TryGetProperty("text", out var textEl) && textEl.TryGetProperty("body", out var bodyEl))
                                text = bodyEl.GetString();

                            result.Add(new IncomingMessage(id, from, text));
                        }
                    }
                }
            }
            catch (JsonException) { /* corpo inválido: ignora */ }
            return result;
        }

        private record DeliveryFailure(string RecipientEnd, string Reason);

        // Formato da Meta: entry[].changes[].value.statuses[] { status: "failed", recipient_id, errors: [{ code, title, error_data: { details } }] }
        private static IEnumerable<DeliveryFailure> ReadDeliveryFailures(byte[] body)
        {
            var result = new List<DeliveryFailure>();
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (!doc.RootElement.TryGetProperty("entry", out var entries)) return result;
                foreach (var entry in entries.EnumerateArray())
                {
                    if (!entry.TryGetProperty("changes", out var changes)) continue;
                    foreach (var change in changes.EnumerateArray())
                    {
                        if (!change.TryGetProperty("value", out var value) || !value.TryGetProperty("statuses", out var statuses)) continue;
                        foreach (var s in statuses.EnumerateArray())
                        {
                            if (!s.TryGetProperty("status", out var st) || st.GetString() != "failed") continue;
                            var recipient = s.TryGetProperty("recipient_id", out var r) ? r.GetString() ?? "" : "";
                            var reasons = new List<string>();
                            if (s.TryGetProperty("errors", out var errors))
                                foreach (var e in errors.EnumerateArray())
                                {
                                    var code = e.TryGetProperty("code", out var c) ? c.ToString() : "?";
                                    var title = e.TryGetProperty("title", out var t) ? t.GetString() : null;
                                    var details = e.TryGetProperty("error_data", out var d) && d.TryGetProperty("details", out var dt) ? dt.GetString() : null;
                                    reasons.Add($"#{code} {title} {details}".Trim());
                                }
                            result.Add(new DeliveryFailure(recipient.Length > 4 ? recipient[^4..] : recipient,
                                reasons.Count > 0 ? string.Join(" | ", reasons) : "sem detalhes"));
                        }
                    }
                }
            }
            catch (JsonException) { }
            return result;
        }

        // 5511999998888 -> +55 ••• •••• 8888
        private static string MaskPhone(string phone) =>
            phone.Length <= 4 ? "••••" : $"+{phone[..2]} ••• •••• {phone[^4..]}";
    }
}
