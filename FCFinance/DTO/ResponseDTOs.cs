using FCFinance.Models;

namespace FCFinance.DTO
{
    // DTOs de SAÍDA: o que a API devolve. Assim as entidades (e campos internos como UserId)
    // não vazam para o cliente, e não há risco de ciclos na serialização.

    public class ValueTypeDTO
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public class CategorySummaryDTO
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public class RegisteredValueResponseDTO
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public decimal Value { get; set; }
        public DateTime CreateWhen { get; set; }
        public int CategoryId { get; set; }
        public int ValueTypeId { get; set; }
        public CategorySummaryDTO? Category { get; set; }
        public ValueTypeDTO? ValueType { get; set; }
    }

    public class GoalResponseDTO
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public DateTime CreatedWhen { get; set; }
        public decimal TargetValue { get; set; }
        public decimal CurrentValue { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsMonthly { get; set; }
        public DateTime? ReferenceDate { get; set; }
        public bool IsPriority { get; set; }

        public static GoalResponseDTO From(Goal g) => new()
        {
            Id = g.Id,
            Name = g.Name,
            CreatedWhen = g.CreatedWhen,
            TargetValue = g.TargetValue,
            CurrentValue = g.CurrentValue,
            EndDate = g.EndDate,
            IsMonthly = g.IsMonthly,
            ReferenceDate = g.ReferenceDate,
            IsPriority = g.IsPriority
        };
    }
}
