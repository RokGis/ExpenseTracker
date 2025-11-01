using System;

namespace ExpenceTracker
{
    public class Expense
    {
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string BudgetBucket { get; set; } = string.Empty;
    }
}