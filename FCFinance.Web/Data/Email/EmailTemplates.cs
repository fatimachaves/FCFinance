using System.Net;

namespace FCFinance.Web.Data.Email
{
    // Textos dos e-mails do FCFinance. Cada método devolve assunto, HTML e texto simples.
    // E-mails não aceitam CSS em arquivo: os estilos ficam "inline" (dentro do HTML), de propósito.
    public static class EmailTemplates
    {
        public record Email(string Assunto, string Html, string Texto);

        public static Email ConfirmarEmail(string nome, string link) => Montar(
            "Confirme seu e-mail no FCFinance",
            nome,
            "Falta pouco! Confirme seu e-mail para ativar sua conta no FCFinance.",
            "Confirmar meu e-mail", link,
            "O link vale por 3 horas. Se você não criou uma conta no FCFinance, ignore este e-mail.");

        public static Email ContaJaExiste(string nome, string linkLogin, string linkEsqueci) => Montar(
            "Você já tem uma conta no FCFinance",
            nome,
            "Alguém (provavelmente você) tentou criar uma conta com este e-mail, mas ele já está cadastrado. " +
            $"Se esqueceu a senha, crie uma nova por aqui: {Link(linkEsqueci, "redefinir senha")}.",
            "Entrar no FCFinance", linkLogin,
            "Se não foi você, pode ignorar este e-mail: nada foi alterado na sua conta.");

        public static Email RedefinirSenha(string nome, string link) => Montar(
            "Redefinição de senha do FCFinance",
            nome,
            "Recebemos um pedido para criar uma nova senha para a sua conta.",
            "Criar nova senha", link,
            "O link vale por 3 horas e só pode ser usado uma vez. Se você não pediu, ignore este e-mail: sua senha continua a mesma.");

        public static Email SenhaAlterada(string nome, string linkEsqueci) => Montar(
            "Sua senha do FCFinance foi alterada",
            nome,
            $"A senha da sua conta foi alterada em {Agora()}. Por segurança, as outras sessões abertas foram encerradas.",
            null, null,
            $"Não foi você? Crie uma nova senha imediatamente: {Link(linkEsqueci, "redefinir senha")}.");

        public static Email ConfirmarNovoEmail(string nome, string link) => Montar(
            "Confirme o novo e-mail da sua conta FCFinance",
            nome,
            "Você pediu para usar este endereço como o novo e-mail da sua conta no FCFinance.",
            "Confirmar novo e-mail", link,
            "O link vale por 3 horas. Se você não pediu esta troca, ignore este e-mail.");

        public static Email DoisFatoresAlterado(string nome, bool ativado) => Montar(
            ativado ? "Verificação em duas etapas ativada" : "Verificação em duas etapas desativada",
            nome,
            ativado
                ? $"A verificação em duas etapas foi ativada na sua conta em {Agora()}. A partir de agora, o login pede o código do app autenticador."
                : $"A verificação em duas etapas foi DESATIVADA na sua conta em {Agora()}.",
            null, null,
            "Não foi você? Troque sua senha imediatamente e reative a verificação em duas etapas.");

        public static Email ContaExcluida(string nome) => Montar(
            "Sua conta do FCFinance foi excluída",
            nome,
            $"Conforme seu pedido, sua conta e todos os seus dados (lançamentos, metas e categorias) foram excluídos em {Agora()}.",
            null, null,
            "Obrigado por ter usado o FCFinance. Se mudar de ideia, é só criar uma nova conta.");

        // ---------------------------------------------------------------

        private static string Agora() =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Fuso).ToString("dd/MM/yyyy 'às' HH:mm") + " (horário de Brasília)";

        private static readonly TimeZoneInfo Fuso = CarregarFuso();
        private static TimeZoneInfo CarregarFuso()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.Local; }
        }

        private static string Link(string url, string texto) =>
            $"<a href=\"{WebUtility.HtmlEncode(url)}\" style=\"color:#0b8a52\">{WebUtility.HtmlEncode(texto)}</a>";

        // paragrafoHtml e rodapeHtml podem conter links montados por Link(); o resto é codificado.
        private static Email Montar(string assunto, string nome, string paragrafoHtml, string? botao, string? link, string rodapeHtml)
        {
            var nomeHtml = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(nome) ? "olá" : nome);
            var botaoHtml = botao == null || link == null ? "" : $"""
                <p style="margin:28px 0;text-align:center">
                  <a href="{WebUtility.HtmlEncode(link)}" style="display:inline-block;padding:14px 26px;border-radius:10px;background:#12a862;color:#ffffff;font-weight:700;text-decoration:none">{WebUtility.HtmlEncode(botao)}</a>
                </p>
                <p style="font-size:12px;color:#6b7785;word-break:break-all">Se o botão não funcionar, copie e cole no navegador:<br>{WebUtility.HtmlEncode(link)}</p>
                """;

            var html = $"""
                <!DOCTYPE html>
                <html lang="pt-BR"><head><meta charset="utf-8"><title>{WebUtility.HtmlEncode(assunto)}</title></head>
                <body style="margin:0;padding:24px;background:#f2f5f4;font-family:Arial,Helvetica,sans-serif;color:#1d2731">
                  <div style="max-width:520px;margin:0 auto;background:#ffffff;border-radius:14px;overflow:hidden;border:1px solid #e1e7e4">
                    <div style="padding:18px 24px;background:#0d2119;color:#43ed98;font-size:20px;font-weight:800">↗ FC<span style="color:#ffffff">Finance</span></div>
                    <div style="padding:24px;font-size:15px;line-height:1.6">
                      <p style="margin-top:0">Olá, <b>{nomeHtml}</b>!</p>
                      <p>{paragrafoHtml}</p>
                      {botaoHtml}
                      <p style="font-size:13px;color:#56626f">{rodapeHtml}</p>
                    </div>
                    <div style="padding:14px 24px;background:#f7f9f8;font-size:11px;color:#8a96a3">Este é um e-mail automático do FCFinance. Não responda.</div>
                  </div>
                </body></html>
                """;

            var texto = $"Olá, {nome}!\n\n{SemHtml(paragrafoHtml)}\n\n" +
                        (botao != null && link != null ? $"{botao}: {link}\n\n" : "") +
                        $"{SemHtml(rodapeHtml)}\n\n-- FCFinance (e-mail automático, não responda)";
            return new Email(assunto, html, texto);
        }

        // Para a versão em texto: troca <a href="x">texto</a> por "texto (x)" e remove o resto das tags.
        private static string SemHtml(string html)
        {
            var s = System.Text.RegularExpressions.Regex.Replace(html, "<a href=\"([^\"]*)\"[^>]*>([^<]*)</a>", m => $"{m.Groups[2].Value} ({WebUtility.HtmlDecode(m.Groups[1].Value)})");
            return WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(s, "<[^>]+>", ""));
        }
    }
}
