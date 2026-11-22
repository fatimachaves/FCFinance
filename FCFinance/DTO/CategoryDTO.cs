using System.ComponentModel.DataAnnotations;
using FCFinance.Models;

namespace FCFinance.DTO
{
    // Entrada (POST/PUT) de categoria.
    public class CategoryDTO
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Informe o nome da categoria.")]
        [StringLength(Limites.NomeCategoria, ErrorMessage = "O nome pode ter no máximo {1} caracteres.")]
        public string Name { get; set; } = "";

        public int CategoryTypeId { get; set; }

        // Só o Admin pode criar categoria do sistema (visível para todos). Para os demais é ignorado.
        public bool IsSystem { get; set; }
    }

    // Saída (GET) de categoria.
    public class CategoryResponseDTO
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int TypeId { get; set; }

        // true = categoria base do sistema.
        public bool IsSystem { get; set; }

        // true = o usuário logado pode editar/excluir (dono, ou Admin numa categoria do sistema).
        public bool CanEdit { get; set; }
    }
}
