using PesticideShop.Models;

namespace PesticideShop.Services
{
    public interface IDailyInventoryService
    {
        Task<DailyInventory> GetOrCreateTodayInventoryAsync();
        Task<DailyInventory> GetOrCreateInventoryAsync(DateTime date);
        Task<DailyInventory?> GetInventoryByDateAsync(DateTime date);
        Task<List<DailyInventory>> GetInventoriesInRangeAsync(DateTime startDate, DateTime endDate);
        Task ProcessTransactionAsync(CustomerTransaction transaction);
        Task<bool> CloseDailyInventoryAsync(DateTime date, string userId);
        Task<DailyInventory> RecalculateInventoryAsync(DateTime date);
        Task<byte[]> ExportToExcelAsync(DateTime date);
        Task<byte[]> ExportDetailedExcelAsync(DateTime date);
        Task<bool> IsDayClosedAsync(DateTime date);
        Task<DailyInventory?> GetPreviousDayInventoryAsync(DateTime date);
        Task<decimal> GetTotalSalesInRangeAsync(DateTime startDate, DateTime endDate);
        Task<List<DailyProductSummary>> GetTopSellingProductsAsync(DateTime date, int count = 10);
        Task<List<DailyCustomerSummary>> GetTopCustomersAsync(DateTime date, int count = 10);
    }
}
