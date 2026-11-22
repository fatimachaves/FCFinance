using Microsoft.AspNetCore.Identity;

namespace FCFinance.Web.Data
{
    // Usuário do sistema. Herda do IdentityUser (login, senha criptografada, e-mail, bloqueio etc.)
    // e acrescenta o nome que aparece em "Olá, <nome>!".
    public class AppUser : IdentityUser
    {
        public string Nome { get; set; } = "";

        // Quando aceitou os Termos de Uso e a Política de Privacidade (prova de consentimento, LGPD).
        // null = usuário antigo, criado antes do cadastro self-service.
        public DateTime? TermosAceitosEm { get; set; }

        // Quando a conta foi criada (UTC).
        public DateTime? CriadoEm { get; set; }
    }

    // Nomes dos perfis (roles). Use estas constantes em vez de escrever o texto solto no código.
    public static class Perfis
    {
        public const string Admin = "Admin";
        public const string Usuario = "Usuario";
    }
}
