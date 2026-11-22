using System.ComponentModel.DataAnnotations;
using FCFinance.Models;

namespace FCFinance.DTO
{
    public class RegisteredValueDTO
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Informe a descrição.")]
        [StringLength(Limites.NomeLancamento, ErrorMessage = "A descrição pode ter no máximo {1} caracteres.")]
        public string Name { get; set; } = "";

        public decimal Value { get; set; }

        public DateTime CreateWhen { get; set; }

        public int CategoryId { get; set; }

        public int ValueTypeId { get; set; }

    }
}
