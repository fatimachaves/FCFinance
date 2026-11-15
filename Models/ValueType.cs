namespace FCFinance.Models
{
    public class ValueType
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public ICollection<RegisteredValue> RegisteredValues { get; set; } = new List<RegisteredValue>();
    }
}
