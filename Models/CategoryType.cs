namespace FCFinance.Models
{
    public class CategoryType
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public ICollection<Category> categories { get; set; } = new List<Category>();
    }
}
