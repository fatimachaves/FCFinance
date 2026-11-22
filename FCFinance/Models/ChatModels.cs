namespace FCFinance.Models
{
    // Telefone do WhatsApp ligado a um usuário do FCFinance (1 telefone por usuário).
    public class WhatsAppLink
    {
        public int Id { get; set; }

        // Número como o WhatsApp envia (só dígitos, com DDI). Ex.: 5511999998888
        public string PhoneNumber { get; set; } = "";

        public string UserId { get; set; } = "";
        public DateTime LinkedAt { get; set; }
    }

    // Código de 6 números gerado no site para ligar o WhatsApp à conta. Vale por poucos minutos.
    public class WhatsAppLinkCode
    {
        public int Id { get; set; }
        public string Code { get; set; } = "";
        public string UserId { get; set; } = "";
        public DateTime ExpiresAt { get; set; }
    }

    // Em que passo está a conversa de cada telefone (apagada quando o lançamento é salvo ou cancelado).
    public class ChatSession
    {
        // Telefone (ou "sim:<userId>" no simulador do site).
        public string PhoneNumber { get; set; } = "";

        public ChatStep Step { get; set; }
        public decimal? Amount { get; set; }
        public int? ValueTypeId { get; set; }
        public int? CategoryId { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Proteção contra quem tenta adivinhar códigos: tentativas erradas na janela atual.
        public int FailedCodeAttempts { get; set; }
        public DateTime? FailedCodeWindowStart { get; set; }
    }

    public enum ChatStep
    {
        Idle = 0,             // esperando um valor
        AskType = 1,          // perguntou: receita ou despesa?
        AskCategory = 2,      // perguntou: qual categoria?
        AskDescription = 3    // perguntou: quer descrição?
    }

    // Ids de mensagens do WhatsApp já processadas (o WhatsApp pode reenviar a mesma mensagem).
    public class ProcessedChatMessage
    {
        public string MessageId { get; set; } = "";
        public DateTime ReceivedAt { get; set; }
    }
}
