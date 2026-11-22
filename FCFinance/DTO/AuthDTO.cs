using System.ComponentModel.DataAnnotations;

namespace FCFinance.DTO
{
    public class LoginDTO
    {
        [Required] public string Login { get; set; } = "";
        [Required] public string Senha { get; set; } = "";
        // true: token vale 30 dias (o app guarda e entra sozinho). false: vale 12 horas.
        public bool ManterConectado { get; set; }
    }

    public class LoginRespostaDTO
    {
        public string Token { get; set; } = "";
        public DateTime ExpiraEm { get; set; }
        public UsuarioDTO Usuario { get; set; } = new();
    }

    public class UsuarioDTO
    {
        public string Id { get; set; } = "";
        public string Nome { get; set; } = "";
        public string Login { get; set; } = "";
        public bool Admin { get; set; }
    }

    public class AlterarSenhaDTO
    {
        [Required] public string SenhaAtual { get; set; } = "";
        [Required, MinLength(8)] public string NovaSenha { get; set; } = "";
    }

    public class NovoUsuarioDTO
    {
        [Required, StringLength(100)] public string Nome { get; set; } = "";
        [Required, RegularExpression(@"^[a-zA-Z0-9._-]{3,30}$")] public string Login { get; set; } = "";
        [Required, MinLength(8)] public string Senha { get; set; } = "";
        public bool Admin { get; set; }
    }
}
