using System.ComponentModel.DataAnnotations;

namespace FCFinance.DTO
{
    public class LoginDTO
    {
        [Required(ErrorMessage = "Informe o usuário.")] public string Login { get; set; } = "";
        [Required(ErrorMessage = "Informe a senha.")] public string Senha { get; set; } = "";
        // true: token vale 30 dias (o app guarda e entra sozinho). false: vale 12 horas.
        public bool ManterConectado { get; set; }

        // Código do app autenticador (ou de recuperação), só para quem ativou a verificação em duas etapas.
        [StringLength(20)] public string? Codigo { get; set; }
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
        [Required(ErrorMessage = "Informe a senha atual.")] public string SenhaAtual { get; set; } = "";
        [Required(ErrorMessage = "Informe a nova senha."), MinLength(8, ErrorMessage = "A senha precisa ter pelo menos 8 caracteres.")]
        public string NovaSenha { get; set; } = "";
    }

    public class ExcluirMinhaContaDTO
    {
        [Required(ErrorMessage = "Digite sua senha para confirmar.")] public string Senha { get; set; } = "";
    }

    public class NovoUsuarioDTO
    {
        [Required(ErrorMessage = "Informe o nome."), StringLength(100, ErrorMessage = "O nome pode ter no máximo {1} caracteres.")]
        public string Nome { get; set; } = "";

        [Required(ErrorMessage = "Informe o login.")]
        [RegularExpression(@"^[a-zA-Z0-9._-]{3,30}$", ErrorMessage = "Use de 3 a 30 letras, números, ponto, hífen ou _ (sem espaços).")]
        public string Login { get; set; } = "";

        [Required(ErrorMessage = "Informe a senha."), MinLength(8, ErrorMessage = "A senha precisa ter pelo menos 8 caracteres.")]
        public string Senha { get; set; } = "";
        public bool Admin { get; set; }
    }
}
