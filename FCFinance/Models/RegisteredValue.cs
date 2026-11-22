namespace FCFinance.Models
{
    public class RegisteredValue
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";

        // Dinheiro sempre em decimal (float perde centavos em somas).
        public decimal Value { get; set; }

        public DateTime CreateWhen { get; set; }

        public int CategoryId { get; set; }

        public Category Category { get; set; } = null!;

        public int ValueTypeId { get; set; }

        public ValueType ValueType { get; set; } = null!;

        // Dono do lançamento (Id do AspNetUsers).
        public string UserId { get; set; } = "";
    }
}
