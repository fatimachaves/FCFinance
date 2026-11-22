namespace FCFinance.Models
{
    public class Goal
    {
        public int Id { get; set; }

        public string Name { get; set; } = "";

        // Momento da criação, em UTC (definido pela API).
        public DateTime CreatedWhen { get; set; }

        public decimal TargetValue { get; set; }
        public decimal CurrentValue { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsMonthly { get; set; }
        public DateTime? ReferenceDate { get; set; }

        // Meta mais importante do usuário (aparece no dashboard). Só pode existir UMA com true.
        public bool IsPriority { get; set; }

        // Dono da meta (Id do AspNetUsers).
        public string UserId { get; set; } = "";
    }
}
