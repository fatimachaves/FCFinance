using System.ComponentModel.DataAnnotations;

namespace FCFinance.DTO
{
    public class WhatsAppStatusDTO
    {
        public bool Connected { get; set; }
        public string? Phone { get; set; }          // mascarado: +55 11 •••••-8888
        public DateTime? LinkedAt { get; set; }
        public bool BotConfigured { get; set; }     // false = WhatsApp real ainda não configurado (só simulador)
    }

    public class WhatsAppLinkCodeDTO
    {
        public string Code { get; set; } = "";
        public DateTime ExpiresAt { get; set; }
    }

    public class ChatSimulateDTO
    {
        [Required(ErrorMessage = "Digite uma mensagem.")]
        [StringLength(500, ErrorMessage = "A mensagem pode ter no máximo {1} caracteres.")]
        public string Text { get; set; } = "";
    }

    public class ChatReplyDTO
    {
        public string Reply { get; set; } = "";
    }
}
