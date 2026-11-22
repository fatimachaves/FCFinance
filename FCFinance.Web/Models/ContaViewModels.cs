using System.ComponentModel.DataAnnotations;

namespace FCFinance.Web.Models
{
    // Dados que vêm do formulário de login.
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Informe seu e-mail ou usuário.")]
        [Display(Name = "E-mail ou usuário")]
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

    // ---------- Cadastro self-service ----------
    public class CadastroViewModel
    {
        [Required(ErrorMessage = "Informe seu nome.")]
        [StringLength(100, ErrorMessage = "O nome pode ter no máximo {1} caracteres.")]
        public string Nome { get; set; } = "";

        [Required(ErrorMessage = "Informe seu e-mail.")]
        [EmailAddress(ErrorMessage = "E-mail inválido.")]
        [StringLength(256)]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Crie uma senha.")]
        [DataType(DataType.Password)]
        [MinLength(8, ErrorMessage = "A senha precisa ter pelo menos 8 caracteres.")]
        public string Senha { get; set; } = "";

        [Required(ErrorMessage = "Confirme a senha.")]
        [DataType(DataType.Password)]
        [Compare(nameof(Senha), ErrorMessage = "As senhas não conferem.")]
        public string ConfirmarSenha { get; set; } = "";

        [Range(typeof(bool), "true", "true", ErrorMessage = "Para criar a conta, aceite os Termos de Uso e a Política de Privacidade.")]
        public bool AceitoTermos { get; set; }
    }

    public class EmailViewModel
    {
        [Required(ErrorMessage = "Informe seu e-mail.")]
        [EmailAddress(ErrorMessage = "E-mail inválido.")]
        public string Email { get; set; } = "";
    }

    public class RedefinirSenhaViewModel
    {
        [Required] public string UserId { get; set; } = "";
        [Required] public string Codigo { get; set; } = "";

        [Required(ErrorMessage = "Informe a nova senha.")]
        [DataType(DataType.Password)]
        [MinLength(8, ErrorMessage = "A senha precisa ter pelo menos 8 caracteres.")]
        public string NovaSenha { get; set; } = "";

        [Required(ErrorMessage = "Confirme a nova senha.")]
        [DataType(DataType.Password)]
        [Compare(nameof(NovaSenha), ErrorMessage = "As senhas não conferem.")]
        public string ConfirmarSenha { get; set; } = "";
    }

    // ---------- Minha conta ----------
    public class MinhaContaViewModel
    {
        public string Login { get; set; } = "";
        public string? EmailAtual { get; set; }
        public bool EmailConfirmado { get; set; }
        public bool DoisFatoresAtivo { get; set; }
        public int CodigosRecuperacaoRestantes { get; set; }
        public bool UltimoAdmin { get; set; }

        [Required(ErrorMessage = "Informe seu nome.")]
        [StringLength(100, ErrorMessage = "O nome pode ter no máximo {1} caracteres.")]
        public string Nome { get; set; } = "";
    }

    public class NovoEmailViewModel
    {
        [Required(ErrorMessage = "Informe o novo e-mail.")]
        [EmailAddress(ErrorMessage = "E-mail inválido.")]
        [StringLength(256)]
        public string NovoEmail { get; set; } = "";
    }

    public class ExcluirContaViewModel
    {
        [Required(ErrorMessage = "Digite sua senha para confirmar.")]
        [DataType(DataType.Password)]
        public string Senha { get; set; } = "";
    }

    // ---------- Verificação em duas etapas (2FA) ----------
    public class LoginDoisFatoresViewModel
    {
        [Required(ErrorMessage = "Digite o código.")]
        [StringLength(20)]
        public string Codigo { get; set; } = "";

        public bool LembrarEsteNavegador { get; set; }
        public bool ManterConectado { get; set; }
        public bool UsarCodigoRecuperacao { get; set; }
        public string? ReturnUrl { get; set; }
    }

    public class AtivarDoisFatoresViewModel
    {
        public string ChaveFormatada { get; set; } = "";
        public string QrCodeDataUri { get; set; } = "";

        [Required(ErrorMessage = "Digite o código de 6 números do app.")]
        [StringLength(7, MinimumLength = 6, ErrorMessage = "O código tem 6 números.")]
        public string Codigo { get; set; } = "";
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
