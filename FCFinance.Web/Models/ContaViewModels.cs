using System.ComponentModel.DataAnnotations;

namespace FCFinance.Web.Models
{
    // Dados que vêm do formulário de login.
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Informe o usuário.")]
        [Display(Name = "Usuário")]
        public string Login { get; set; } = "";

        [Required(ErrorMessage = "Informe a senha.")]
        [DataType(DataType.Password)]
        public string Senha { get; set; } = "";

        [Display(Name = "Manter conectado")]
        public bool ManterConectado { get; set; }

        // Página que a pessoa tentou abrir antes de ser mandada para o login.
        public string? ReturnUrl { get; set; }
    }

    public class AlterarSenhaViewModel
    {
        [Required(ErrorMessage = "Informe a senha atual.")]
        [DataType(DataType.Password)]
        public string SenhaAtual { get; set; } = "";

        [Required(ErrorMessage = "Informe a nova senha.")]
        [DataType(DataType.Password)]
        [MinLength(8, ErrorMessage = "A senha precisa ter pelo menos 8 caracteres.")]
        public string NovaSenha { get; set; } = "";

        [Required(ErrorMessage = "Confirme a nova senha.")]
        [DataType(DataType.Password)]
        [Compare(nameof(NovaSenha), ErrorMessage = "As senhas não conferem.")]
        public string ConfirmarSenha { get; set; } = "";
    }

    public class NovoUsuarioViewModel
    {
        [Required(ErrorMessage = "Informe o nome.")]
        [StringLength(100)]
        public string Nome { get; set; } = "";

        [Required(ErrorMessage = "Informe o login.")]
        [RegularExpression(@"^[a-zA-Z0-9._-]{3,30}$", ErrorMessage = "Use de 3 a 30 letras, números, ponto, hífen ou _ (sem espaços).")]
        public string Login { get; set; } = "";

        [Required(ErrorMessage = "Informe a senha.")]
        [DataType(DataType.Password)]
        [MinLength(8, ErrorMessage = "A senha precisa ter pelo menos 8 caracteres.")]
        public string Senha { get; set; } = "";

        public bool Admin { get; set; }
    }

    public class UsuarioListaItem
    {
        public string Id { get; set; } = "";
        public string Nome { get; set; } = "";
        public string Login { get; set; } = "";
        public bool Admin { get; set; }
    }

    public class UsuariosViewModel
    {
        public List<UsuarioListaItem> Usuarios { get; set; } = new();
        public NovoUsuarioViewModel Novo { get; set; } = new();
    }
}
