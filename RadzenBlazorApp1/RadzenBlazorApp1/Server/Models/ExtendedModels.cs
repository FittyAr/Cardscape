using Radzen;

namespace RadzenBlazorApp1.Server.Models
{
    public class MonthlyStats
    {
        public DateTime Month { get; set; }
        public decimal Revenue { get; set; }
        public int Opportunities { get; set; }
        public decimal AverageDealSize { get; set; }
        public double Ratio { get; set; }
    }

    public class RevenueByCompany
    {
        public string Company { get; set; }
        public decimal Revenue { get; set; }
    }

    public class RevenueByEmployee
    {
        public string Employee { get; set; }
        public decimal Revenue { get; set; }
    }

    public class RevenueByMonth
    {
        public DateTime Month { get; set; }
        public decimal Revenue { get; set; }
    }
}