using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace FCFinance.Web.Data.Email
{
    // Configuração do e-mail (seção "Email" do appsettings; a senha SMTP fica em User Secrets).
    public class EmailOptions
    {
        // "Pasta": grava os e-mails numa pasta (desenvolvimento, sem enviar nada).
        // "Smtp":  envia de verdade (Brevo: smtp-relay.brevo.com, porta 587).
        public string Modo { get; set; } = "Pasta";
        public string Pasta { get; set; } = "../emails-dev";

        public string SmtpHost { get; set; } = "smtp-relay.brevo.com";
        public int SmtpPorta { get; set; } = 587;
        public string SmtpUsuario { get; set; } = "";
        public string SmtpSenha { get; set; } = "";     // User Secrets: Email:SmtpSenha

        public string Remetente { get; set; } = "nao-responda@fcfinance.local";
        public string NomeRemetente { get; set; } = "FCFinance";
    }

    public interface IEmailSender
    {
        Task EnviarAsync(string para, string assunto, string html, string texto, CancellationToken ct = default);
    }

    public class EmailSender : IEmailSender
    {
        private readonly EmailOptions _options;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<EmailSender> _logger;

        public EmailSender(IOptions<EmailOptions> options, IWebHostEnvironment env, ILogger<EmailSender> logger)
        {
            _options = options.Value;
            _env = env;
            _logger = logger;
        }

        public async Task EnviarAsync(string para, string assunto, string html, string texto, CancellationToken ct = default)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_options.NomeRemetente, _options.Remetente));
            message.To.Add(MailboxAddress.Parse(para));
            message.Subject = assunto;
            message.Body = new BodyBuilder { HtmlBody = html, TextBody = texto }.ToMessageBody();

            if (string.Equals(_options.Modo, "Smtp", StringComparison.OrdinalIgnoreCase))
            {
                using var client = new SmtpClient();
                await client.ConnectAsync(_options.SmtpHost, _options.SmtpPorta, SecureSocketOptions.StartTls, ct);
                await client.AuthenticateAsync(_options.SmtpUsuario, _options.SmtpSenha, ct);
                await client.SendAsync(new MailKit.SendRequest(message), ct); // MailKit 5: envio via SendRequest
                await client.DisconnectAsync(true, ct);
                _logger.LogInformation("E-mail \"{Assunto}\" enviado.", assunto); // sem o destinatário no log (dado pessoal)
                return;
            }

            // Modo "Pasta": grava .eml (abre no Outlook/Mail) e .html (abre no navegador).
            var pasta = Path.GetFullPath(Path.Combine(_env.ContentRootPath, _options.Pasta));
            Directory.CreateDirectory(pasta);
            var nome = $"{DateTime.Now:yyyyMMdd-HHmmss-fff}-{Seguro(assunto)}";
            await using (var eml = File.Create(Path.Combine(pasta, nome + ".eml")))
                await message.WriteToAsync(eml, ct);
            await File.WriteAllTextAsync(Path.Combine(pasta, nome + ".html"), $"<!-- Para: {System.Net.WebUtility.HtmlEncode(para)} -->\n{html}", ct);
            _logger.LogInformation("E-mail \"{Assunto}\" gravado em {Pasta} (modo desenvolvimento).", assunto, pasta);
        }

        private static string Seguro(string texto)
        {
            var invalidos = Path.GetInvalidFileNameChars();
            var limpo = new string(texto.Select(c => invalidos.Contains(c) || c == ' ' ? '-' : c).ToArray());
            return limpo.Length > 40 ? limpo[..40] : limpo;
        }
    }
}
