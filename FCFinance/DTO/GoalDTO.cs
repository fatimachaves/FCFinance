using System.ComponentModel.DataAnnotations;
using FCFinance.Models;

namespace FCFinance.DTO
{
    // Entrada (POST/PUT) de meta. O dono (UserId) vem sempre do token, nunca do cliente.
    public class GoalDTO
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Dê um nome para a meta.")]
        [StringLength(Limites.NomeMeta, ErrorMessage = "O nome pode ter no máximo {1} caracteres.")]
        public string Name { get; set; } = "";

        [Range(0.01, 999_999_999_999, ErrorMessage = "Informe um valor de meta maior que zero.")]
        public decimal TargetValue { get; set; }

        [Range(0, 999_999_999_999, ErrorMessage = "O valor economizado não pode ser negativo.")]
        public decimal CurrentValue { get; set; }

        public DateTime? EndDate { get; set; }
        public bool IsMonthly { get; set; }
        public DateTime? ReferenceDate { get; set; }
        public bool IsPriority { get; set; }
    }
}
