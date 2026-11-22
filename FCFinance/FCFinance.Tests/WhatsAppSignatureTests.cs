using System.Security.Cryptography;
using System.Text;
using FCFinance.Services.WhatsApp;

namespace FCFinance.Tests
{
    public class WhatsAppSignatureTests
    {
        private const string Secret = "app-secret-de-teste";
        private static readonly byte[] Body = Encoding.UTF8.GetBytes("{\"entry\":[]}");

        private static string Sign(byte[] body, string secret) =>
            "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body)).ToLowerInvariant();

        [Fact]
        public void Aceita_mensagem_assinada_pela_Meta() =>
            Assert.True(WhatsAppCloudClient.IsValidSignature(Body, Sign(Body, Secret), Secret));

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("sha256=nao-e-hex")]
        [InlineData("sha1=abc")]
        public void Recusa_assinatura_ausente_ou_mal_formada(string? header) =>
            Assert.False(WhatsAppCloudClient.IsValidSignature(Body, header, Secret));

        [Theory]
        [InlineData("5511912345678", "551112345678")] // celular do Brasil: tira o 9
        [InlineData("551112345678", "551112345678")]  // já sem o 9: não muda
        [InlineData("5511312345678", "5511312345678")] // 13 dígitos sem 9 depois do DDD: não muda
        [InlineData("15551597763", "15551597763")]     // outro país: não muda
        public void Celular_do_Brasil_e_enviado_sem_o_nono_digito(string recebido, string enviado) =>
            Assert.Equal(enviado, WhatsAppCloudClient.ToBrazilWithoutNinthDigit(recebido));

        [Fact]
        public void Recusa_assinatura_com_outra_chave_ou_corpo_alterado()
        {
            Assert.False(WhatsAppCloudClient.IsValidSignature(Body, Sign(Body, "outra-chave"), Secret));
            var alterado = Encoding.UTF8.GetBytes("{\"entry\":[1]}");
            Assert.False(WhatsAppCloudClient.IsValidSignature(alterado, Sign(Body, Secret), Secret));
        }
    }
}
