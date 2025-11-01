using System;
using System.Collections.Generic;

namespace ExpenceTracker.Models
{
    public class AppState
    {
        public List<Expense> Expenses { get; set; } = new();
        public Dictionary<string, decimal> MonthlyIncomes { get; set; } = new();

        public decimal MonthlyIncome { get; set; } = 0m;
        public DateTime IncomeMonth { get; set; } = DateTime.MinValue;

        public decimal Bucket50 { get; set; } = 0.50m;
        public decimal Bucket30 { get; set; } = 0.30m;
        public decimal Bucket20 { get; set; } = 0.20m;
    }
}