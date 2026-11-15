namespace FCFinance.Models
{
    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public int CategoryTypeId { get; set; }

        public CategoryType CategoryType { get; set; }
        public ICollection<RegisteredValue> registeredValues { get; set; } = new List<RegisteredValue>();
    }
}
