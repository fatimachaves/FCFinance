namespace FCFinance.Models
{
    public class ValueType
    {
        // Ids fixos (criados pela migration SeedValueTypes).
        public const int Receita = 1;
        public const int Despesa = 2;

        public int Id { get; set; }
        public string Name { get; set; } = "";

        public ICollection<RegisteredValue> RegisteredValues { get; set; } = new List<RegisteredValue>();
        public ICollection<Category> Categories { get; set; } = new List<Category>();
    }
}
