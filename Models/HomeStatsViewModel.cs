namespace PesticideShop.Models
{
    public class HomeStatsViewModel
    {
        public int TotalCustomers { get; set; }
        public int TotalProducts { get; set; }
        public int TotalInvoices { get; set; }
        public int TotalOrders { get; set; }
        public decimal TotalSales { get; set; }
        public int CustomerSatisfaction { get; set; }
    }
}
