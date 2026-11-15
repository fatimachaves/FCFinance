namespace FCFinance.Models
{
    public class Goal
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public DateTime CreatedWhen { get; set; } = DateTime.Now;

        public decimal TargetValue { get; set; }
        public decimal CurrentValue { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsMonthly { get; set; }
        public DateTime? ReferenceDate { get; set; }
    }
}
