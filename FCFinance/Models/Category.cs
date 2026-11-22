namespace FCFinance.Models
{
    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";

        public int TypeId { get; set; }
        public ValueType Type { get; set; } = null!;

        // Dono da categoria. null = categoria do sistema (visível para todos os usuários,
        // só o Admin altera). Preenchido = categoria criada por aquele usuário.
        public string? UserId { get; set; }

        public ICollection<RegisteredValue> registeredValues { get; set; } = new List<RegisteredValue>();
    }
}
