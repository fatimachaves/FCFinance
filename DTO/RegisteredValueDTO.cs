using FCFinance.Models;

namespace FCFinance.DTO
{
    public class RegisteredValueDTO
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public float Value { get; set; }

        public DateTime CreateWhen { get; set; }

        public int CategoryId { get; set; }

        public int ValueTypeId { get; set; }

    }
}
