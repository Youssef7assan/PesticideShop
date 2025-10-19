using Microsoft.EntityFrameworkCore;
using PesticideShop.Data;
using PesticideShop.Models;

namespace PesticideShop.Services
{
    public class FinancialService
    {
        private readonly ApplicationDbContext _context;

        public FinancialService(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// تحديث الأرباح والإحصائيات بعد عملية الاستبدال
        /// </summary>
        public async Task UpdateFinancialsAfterExchangeAsync(ExchangeTracking exchange)
        {
            try
            {
                // Update daily inventory if exists
                await UpdateDailyInventoryForExchangeAsync(exchange);
                
                // Update customer transactions summary
                await UpdateCustomerFinancialsAsync(exchange);
                
                // Log financial impact
                await LogFinancialImpactAsync(exchange);
                
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Log error but don't throw to avoid breaking the exchange process
                Console.WriteLine($"Error updating financials after exchange: {ex.Message}");
            }
        }

        /// <summary>
        /// تحديث الجرد اليومي لعملية الاستبدال
        /// </summary>
        private async Task UpdateDailyInventoryForExchangeAsync(ExchangeTracking exchange)
        {
            var today = DateTime.Today;
            var dailyInventory = await _context.DailyInventories
                .FirstOrDefaultAsync(di => di.InventoryDate.Date == today);

            if (dailyInventory != null)
            {
                // Update total transactions count to include exchanges
                var dailyExchangeCount = await _context.ExchangeTrackings
                    .CountAsync(e => e.ExchangeDate.Date == today);
                
                // Note: Since DailyInventory doesn't have TotalExchanges property,
                // we'll update the Notes field with exchange information
                var exchangeInfo = $"استبدالات: {dailyExchangeCount}";
                dailyInventory.Notes = dailyInventory.Notes?.Contains("استبدالات:") == true 
                    ? System.Text.RegularExpressions.Regex.Replace(dailyInventory.Notes, @"استبدالات: \d+", exchangeInfo)
                    : (dailyInventory.Notes + " | " + exchangeInfo).Trim('|', ' ');

                // Create product summary entries for exchange
                await UpdateProductSummaryForExchangeAsync(dailyInventory.Id, exchange);
            }
        }

        /// <summary>
        /// تحديث ملخص المنتجات للاستبدال
        /// </summary>
        private async Task UpdateProductSummaryForExchangeAsync(int dailyInventoryId, ExchangeTracking exchange)
        {
            // Update old product summary (returned)
            var oldProductSummary = await _context.DailyProductSummaries
                .FirstOrDefaultAsync(dps => dps.DailyInventoryId == dailyInventoryId && 
                                           dps.ProductId == exchange.OldProductId);

            if (oldProductSummary != null)
            {
                // Since DailyProductSummary doesn't have ReturnedQuantity, we'll use negative values
                oldProductSummary.TotalQuantitySold -= exchange.ExchangedQuantity; // Reduce sold quantity (return effect)
                oldProductSummary.TotalSalesValue -= (exchange.OldProduct?.Price ?? 0) * exchange.ExchangedQuantity;
                oldProductSummary.NetSalesValue -= (exchange.OldProduct?.Price ?? 0) * exchange.ExchangedQuantity;
            }
            else
            {
                // Create new summary for old product (returned via exchange)
                var newOldSummary = new DailyProductSummary
                {
                    DailyInventoryId = dailyInventoryId,
                    ProductId = exchange.OldProductId,
                    StartingQuantity = exchange.OldProduct?.Quantity ?? 0,
                    EndingQuantity = exchange.OldProduct?.Quantity ?? 0,
                    TotalQuantitySold = -exchange.ExchangedQuantity, // Negative for return
                    TotalSalesValue = -(exchange.OldProduct?.Price ?? 0) * exchange.ExchangedQuantity,
                    TotalCostValue = 0,
                    TotalDiscounts = 0,
                    NetSalesValue = -(exchange.OldProduct?.Price ?? 0) * exchange.ExchangedQuantity,
                    NetProfit = 0, // لا نحسب ربح للإرجاع - فقط إذا كان هناك تكلفة حقيقية
                    TransactionsCount = 1
                };
                _context.DailyProductSummaries.Add(newOldSummary);
            }

            // Update new product summary (sold via exchange)
            var newProductSummary = await _context.DailyProductSummaries
                .FirstOrDefaultAsync(dps => dps.DailyInventoryId == dailyInventoryId && 
                                           dps.ProductId == exchange.NewProductId);

            if (newProductSummary != null)
            {
                newProductSummary.TotalQuantitySold += exchange.ExchangedQuantity;
                newProductSummary.TotalSalesValue += (exchange.NewProduct?.Price ?? 0) * exchange.ExchangedQuantity;
                newProductSummary.NetSalesValue += (exchange.NewProduct?.Price ?? 0) * exchange.ExchangedQuantity;
                // لا نحسب ربح للبيع الجديد - فقط إذا كان هناك تكلفة حقيقية
                if (exchange.NewProduct?.CostPrice > 0)
                {
                    newProductSummary.NetProfit += ((exchange.NewProduct?.Price ?? 0) - (exchange.NewProduct?.CostPrice ?? 0)) * exchange.ExchangedQuantity;
                }
                newProductSummary.TransactionsCount += 1;
            }
            else
            {
                // Create new summary for new product (sold via exchange)
                var newProductSummary2 = new DailyProductSummary
                {
                    DailyInventoryId = dailyInventoryId,
                    ProductId = exchange.NewProductId,
                    StartingQuantity = exchange.NewProduct?.Quantity ?? 0,
                    EndingQuantity = exchange.NewProduct?.Quantity ?? 0,
                    TotalQuantitySold = exchange.ExchangedQuantity,
                    TotalSalesValue = (exchange.NewProduct?.Price ?? 0) * exchange.ExchangedQuantity,
                    TotalCostValue = 0,
                    TotalDiscounts = 0,
                    NetSalesValue = (exchange.NewProduct?.Price ?? 0) * exchange.ExchangedQuantity,
                    NetProfit = 0, // لا نحسب ربح للبيع الجديد - فقط إذا كان هناك تكلفة حقيقية
                    TransactionsCount = 1
                };
                _context.DailyProductSummaries.Add(newProductSummary2);
            }
        }

        /// <summary>
        /// تحديث إحصائيات العميل
        /// </summary>
        private async Task UpdateCustomerFinancialsAsync(ExchangeTracking exchange)
        {
            var originalInvoice = await _context.Invoices
                .Include(i => i.Customer)
                .FirstOrDefaultAsync(i => i.InvoiceNumber == exchange.OriginalInvoiceNumber);

            if (originalInvoice?.Customer != null)
            {
                var today = DateTime.Today;
                var dailyInventory = await _context.DailyInventories
                    .FirstOrDefaultAsync(di => di.InventoryDate.Date == today);

                if (dailyInventory != null)
                {
                    var customerSummary = await _context.DailyCustomerSummaries
                        .FirstOrDefaultAsync(dcs => dcs.DailyInventoryId == dailyInventory.Id && 
                                                   dcs.CustomerId == originalInvoice.CustomerId);

                    if (customerSummary != null)
                    {
                        // Update exchange statistics (using existing fields)
                        customerSummary.TransactionsCount += 1;
                        
                        // Add exchange amount to purchases or adjust debt based on price difference
                        if (exchange.PriceDifference > 0)
                        {
                            customerSummary.TotalPurchases += exchange.PriceDifference;
                            customerSummary.DebtAmount += exchange.PriceDifference;
                        }
                        else if (exchange.PriceDifference < 0)
                        {
                            customerSummary.DebtAmount += exchange.PriceDifference; // Negative = credit
                        }
                    }
                    else
                    {
                        // Create new customer summary for exchange
                        var newCustomerSummary = new DailyCustomerSummary
                        {
                            DailyInventoryId = dailyInventory.Id,
                            CustomerId = originalInvoice.CustomerId,
                            TransactionsCount = 1,
                            TotalPurchases = Math.Max(0, exchange.PriceDifference),
                            TotalPayments = 0,
                            DebtAmount = exchange.PriceDifference,
                            LastTransactionTime = DateTime.Now
                        };
                        _context.DailyCustomerSummaries.Add(newCustomerSummary);
                    }
                }
            }
        }

        /// <summary>
        /// تسجيل الأثر المالي للاستبدال
        /// </summary>
        private async Task LogFinancialImpactAsync(ExchangeTracking exchange)
        {
            var activityLog = new ActivityLog
            {
                Action = "Exchange Financial Impact",
                EntityType = "Financial",
                EntityName = $"Exchange {exchange.ExchangeInvoiceNumber}",
                Details = $"Exchange {exchange.ExchangeInvoiceNumber}: {exchange.OldProduct?.Name} → {exchange.NewProduct?.Name}, " +
                         $"Quantity: {exchange.ExchangedQuantity}, Price Difference: {exchange.PriceDifference:F2}",
                UserId = exchange.CreatedBy,
                Timestamp = DateTime.Now
            };

            _context.ActivityLogs.Add(activityLog);
        }

        /// <summary>
        /// تحديث الأرباح والإحصائيات بعد عملية الإرجاع
        /// </summary>
        public async Task UpdateFinancialsAfterReturnAsync(ReturnTracking returnTracking)
        {
            try
            {
                var today = DateTime.Today;
                var dailyInventory = await _context.DailyInventories
                    .FirstOrDefaultAsync(di => di.InventoryDate.Date == today);

                if (dailyInventory != null)
                {
                    // Update daily return statistics in Notes field
                    var dailyReturnCount = await _context.ReturnTrackings
                        .CountAsync(r => r.ReturnDate.Date == today);
                    
                    var returnInfo = $"إرجاعات: {dailyReturnCount}";
                    dailyInventory.Notes = dailyInventory.Notes?.Contains("إرجاعات:") == true 
                        ? System.Text.RegularExpressions.Regex.Replace(dailyInventory.Notes, @"إرجاعات: \d+", returnInfo)
                        : (dailyInventory.Notes + " | " + returnInfo).Trim('|', ' ');

                    // Update product summary for return
                    var productSummary = await _context.DailyProductSummaries
                        .FirstOrDefaultAsync(dps => dps.DailyInventoryId == dailyInventory.Id && 
                                                   dps.ProductId == returnTracking.ProductId);

                    if (productSummary != null)
                    {
                        // Use negative values to represent returns
                        productSummary.TotalQuantitySold -= returnTracking.ReturnedQuantity;
                        productSummary.TotalSalesValue -= (returnTracking.Product?.Price ?? 0) * returnTracking.ReturnedQuantity;
                        // For returns, don't consider original discount as loss - just subtract the product value
                        productSummary.NetSalesValue -= (returnTracking.Product?.Price ?? 0) * returnTracking.ReturnedQuantity;
                        // لا نحسب ربح للإرجاع - فقط إذا كان هناك تكلفة حقيقية
                        if (returnTracking.Product?.CostPrice > 0)
                        {
                            productSummary.NetProfit -= ((returnTracking.Product?.Price ?? 0) - (returnTracking.Product?.CostPrice ?? 0)) * returnTracking.ReturnedQuantity;
                        }
                        // Don't add discount to TotalDiscounts for returns - original discount is not a loss
                    }

                    // Update customer summary
                    var originalInvoice = await _context.Invoices
                        .FirstOrDefaultAsync(i => i.InvoiceNumber == returnTracking.OriginalInvoiceNumber);

                    if (originalInvoice != null)
                    {
                        var customerSummary = await _context.DailyCustomerSummaries
                            .FirstOrDefaultAsync(dcs => dcs.DailyInventoryId == dailyInventory.Id && 
                                                       dcs.CustomerId == originalInvoice.CustomerId);

                        if (customerSummary != null)
                        {
                            customerSummary.TransactionsCount += 1;
                            // Reduce purchases and debt due to return
                            var returnAmount = (returnTracking.Product?.Price ?? 0) * returnTracking.ReturnedQuantity;
                            customerSummary.TotalPurchases -= returnAmount;
                            customerSummary.DebtAmount -= returnAmount;
                        }
                    }
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating financials after return: {ex.Message}");
            }
        }

        /// <summary>
        /// إعادة حساب الأرباح لفترة معينة
        /// </summary>
        public async Task RecalculateProfitsAsync(DateTime fromDate, DateTime toDate)
        {
            var invoices = await _context.Invoices
                .Include(i => i.Items)
                .ThenInclude(ii => ii.Product)
                .Where(i => i.InvoiceDate.Date >= fromDate.Date && i.InvoiceDate.Date <= toDate.Date)
                .ToListAsync();

            var exchanges = await _context.ExchangeTrackings
                .Include(e => e.OldProduct)
                .Include(e => e.NewProduct)
                .Where(e => e.ExchangeDate.Date >= fromDate.Date && e.ExchangeDate.Date <= toDate.Date)
                .ToListAsync();

            var returns = await _context.ReturnTrackings
                .Include(r => r.Product)
                .Where(r => r.ReturnDate.Date >= fromDate.Date && r.ReturnDate.Date <= toDate.Date)
                .ToListAsync();

            // Recalculate daily inventories for the period
            for (var date = fromDate.Date; date <= toDate.Date; date = date.AddDays(1))
            {
                await RecalculateDailyInventoryAsync(date, invoices, exchanges, returns);
            }
        }

        /// <summary>
        /// إعادة حساب الجرد اليومي لتاريخ معين
        /// </summary>
        private async Task RecalculateDailyInventoryAsync(DateTime date, 
            List<Invoice> allInvoices, 
            List<ExchangeTracking> allExchanges, 
            List<ReturnTracking> allReturns)
        {
            var dayInvoices = allInvoices.Where(i => i.InvoiceDate.Date == date).ToList();
            var dayExchanges = allExchanges.Where(e => e.ExchangeDate.Date == date).ToList();
            var dayReturns = allReturns.Where(r => r.ReturnDate.Date == date).ToList();

            var dailyInventory = await _context.DailyInventories
                .FirstOrDefaultAsync(di => di.InventoryDate.Date == date);

            if (dailyInventory != null)
            {
                // Update transaction counts
                dailyInventory.TransactionsCount = dayInvoices.Count + dayExchanges.Count + dayReturns.Count;
                
                // Update sales amount (using existing TotalSales property as amount, not count)
                dailyInventory.TotalSales = dayInvoices
                    .Where(i => i.Type == InvoiceType.Sale)
                    .Sum(i => i.TotalAmount);

                // Update counts in Notes field
                var salesCount = dayInvoices.Count(i => i.Type == InvoiceType.Sale);
                var returnCount = dayInvoices.Count(i => i.Type == InvoiceType.Return) + dayReturns.Count;
                var exchangeCount = dayInvoices.Count(i => i.Type == InvoiceType.Exchange) + dayExchanges.Count;
                
                dailyInventory.Notes = $"مبيعات: {salesCount} | إرجاعات: {returnCount} | استبدالات: {exchangeCount}";

                // Update other financial metrics as needed
                await _context.SaveChangesAsync();
            }
        }
    }
}
