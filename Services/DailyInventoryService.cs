using Microsoft.EntityFrameworkCore;
using PesticideShop.Data;
using PesticideShop.Models;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;

namespace PesticideShop.Services
{
    public class DailyInventoryService : IDailyInventoryService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DailyInventoryService> _logger;

        public DailyInventoryService(ApplicationDbContext context, ILogger<DailyInventoryService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<DailyInventory> GetOrCreateTodayInventoryAsync()
        {
            var today = DateTime.Today;
            return await GetOrCreateInventoryAsync(today);
        }

        public async Task<DailyInventory?> GetInventoryByDateAsync(DateTime date)
        {
            var inventoryDate = date.Date;
            
            return await _context.DailyInventories
                .Include(di => di.SaleTransactions)
                    .ThenInclude(dst => dst.Product)
                .Include(di => di.SaleTransactions)
                    .ThenInclude(dst => dst.Customer)
                .Include(di => di.ProductSummaries)
                    .ThenInclude(dps => dps.Product)
                .Include(di => di.CustomerSummaries)
                    .ThenInclude(dcs => dcs.Customer)
                .FirstOrDefaultAsync(di => di.InventoryDate.Date == inventoryDate);
        }

        public async Task<List<DailyInventory>> GetInventoriesInRangeAsync(DateTime startDate, DateTime endDate)
        {
            return await _context.DailyInventories
                .Where(di => di.InventoryDate.Date >= startDate.Date && di.InventoryDate.Date <= endDate.Date)
                .OrderByDescending(di => di.InventoryDate)
                .ToListAsync();
        }

        public async Task ProcessTransactionAsync(CustomerTransaction transaction)
        {
            try
            {
                var inventoryDate = transaction.Date.Date;
                var dailyInventory = await GetOrCreateInventoryAsync(inventoryDate);

                // Check if day is already closed
                if (dailyInventory.Status == InventoryStatus.Closed)
                {
                    _logger.LogWarning($"Attempted to process transaction for closed day: {inventoryDate:yyyy-MM-dd}");
                    return;
                }

                // Load product and customer data if not already loaded
                if (transaction.Product == null && transaction.ProductId > 0)
                {
                    transaction.Product = await _context.Products.FindAsync(transaction.ProductId);
                }
                if (transaction.Customer == null)
                {
                    transaction.Customer = await _context.Customers.FindAsync(transaction.CustomerId);
                }

                // تسجيل العملية فقط بدون تحديث المخزون (تم التحديث مسبقاً في ProcessTransactionItemsAsync)
                if (transaction.Product != null)
                {
                    _logger.LogInformation($"DAILY INVENTORY PROCESS: Product: {transaction.Product.Name}, Qty: {transaction.Quantity}, Current Qty: {transaction.Product.Quantity}");
                }

                // Create daily sale transaction record
                var dailySaleTransaction = new DailySaleTransaction
                {
                    DailyInventoryId = dailyInventory.Id,
                    CustomerId = transaction.CustomerId,
                    ProductId = transaction.ProductId,
                    Quantity = transaction.Quantity,
                    UnitPrice = transaction.Price,
                    CostPrice = transaction.Product?.CartonPrice ?? 0,
                    TotalPrice = transaction.TotalPrice,
                    Discount = transaction.Discount,
                    AmountPaid = transaction.AmountPaid,
                    TransactionTime = transaction.Date,
                    Notes = transaction.Notes,
                    OriginalTransactionId = transaction.Id
                };

                _context.DailySaleTransactions.Add(dailySaleTransaction);

                // Update or create product summary
                await UpdateProductSummaryAsync(dailyInventory.Id, transaction);

                // Update or create customer summary
                await UpdateCustomerSummaryAsync(dailyInventory.Id, transaction);

                // Update daily inventory totals
                await UpdateDailyInventoryTotalsAsync(dailyInventory.Id);

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Processed transaction {transaction.Id} for daily inventory {dailyInventory.InventoryDate:yyyy-MM-dd}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing transaction {transaction.Id} for daily inventory");
                throw;
            }
        }

        public async Task<bool> CloseDailyInventoryAsync(DateTime date, string userId)
        {
            try
            {
                var inventory = await GetInventoryByDateAsync(date);
                if (inventory == null)
                {
                    // Create inventory if it doesn't exist
                    inventory = await GetOrCreateInventoryAsync(date.Date);
                }

                // Update status first
                inventory.Status = InventoryStatus.Closed;
                inventory.UpdatedAt = DateTime.Now;
                inventory.ResponsibleUser = userId;

                // Save the status change first
                await _context.SaveChangesAsync();

                // Then try to recalculate (if this fails, at least the status is saved)
                try
                {
                    await RecalculateInventoryAsync(date);
                }
                catch (Exception recalcEx)
                {
                    _logger.LogError(recalcEx, $"Error recalculating inventory during close for {date:yyyy-MM-dd}, but status was saved");
                    // Don't fail the close operation just because recalculation failed
                }

                _logger.LogInformation($"Closed daily inventory for {date:yyyy-MM-dd} by user {userId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error closing daily inventory for {date:yyyy-MM-dd}");
                return false;
            }
        }

        public async Task<DailyInventory> RecalculateInventoryAsync(DateTime date)
        {
            var inventoryDate = date.Date;
            var dailyInventory = await GetOrCreateInventoryAsync(inventoryDate);

            try
            {
                // Get all transactions for this day
                var startTime = inventoryDate; // 12:00 AM
                var endTime = inventoryDate.AddDays(1).AddTicks(-1); // 11:59:59.999 PM

                var transactions = await _context.CustomerTransactions
                    .Include(ct => ct.Product)
                    .Include(ct => ct.Customer)
                    .Where(ct => ct.Date >= startTime && ct.Date <= endTime)
                    .ToListAsync();

                // Clear existing summaries in batches to avoid large deletions
                try
                {
                    var existingSaleTransactions = await _context.DailySaleTransactions
                        .Where(dst => dst.DailyInventoryId == dailyInventory.Id)
                        .ToListAsync();
                    if (existingSaleTransactions.Any())
                    {
                        _context.DailySaleTransactions.RemoveRange(existingSaleTransactions);
                        await _context.SaveChangesAsync();
                    }

                    var existingProductSummaries = await _context.DailyProductSummaries
                        .Where(dps => dps.DailyInventoryId == dailyInventory.Id)
                        .ToListAsync();
                    if (existingProductSummaries.Any())
                    {
                        _context.DailyProductSummaries.RemoveRange(existingProductSummaries);
                        await _context.SaveChangesAsync();
                    }

                    var existingCustomerSummaries = await _context.DailyCustomerSummaries
                        .Where(dcs => dcs.DailyInventoryId == dailyInventory.Id)
                        .ToListAsync();
                    if (existingCustomerSummaries.Any())
                    {
                        _context.DailyCustomerSummaries.RemoveRange(existingCustomerSummaries);
                        await _context.SaveChangesAsync();
                    }
                }
                catch (Exception clearEx)
                {
                    _logger.LogError(clearEx, $"Error clearing existing summaries for {date:yyyy-MM-dd}");
                }

                // Recalculate everything
                foreach (var transaction in transactions)
                {
                    try
                    {
                        // Create daily sale transaction
                        var dailySaleTransaction = new DailySaleTransaction
                        {
                            DailyInventoryId = dailyInventory.Id,
                            CustomerId = transaction.CustomerId,
                            ProductId = transaction.ProductId,
                            Quantity = transaction.Quantity,
                            UnitPrice = transaction.Price,
                            CostPrice = transaction.Product?.CartonPrice ?? 0,
                            TotalPrice = transaction.TotalPrice,
                            Discount = transaction.Discount,
                            AmountPaid = transaction.AmountPaid,
                            TransactionTime = transaction.Date,
                            Notes = transaction.Notes,
                            OriginalTransactionId = transaction.Id
                        };
                        _context.DailySaleTransactions.Add(dailySaleTransaction);

                        // Update summaries
                        await UpdateProductSummaryAsync(dailyInventory.Id, transaction);
                        await UpdateCustomerSummaryAsync(dailyInventory.Id, transaction);
                    }
                    catch (Exception transEx)
                    {
                        _logger.LogError(transEx, $"Error processing transaction {transaction.Id} during recalculation");
                        continue; // Skip this transaction but continue with others
                    }
                }

                // Update daily inventory totals
                try
                {
                    await UpdateDailyInventoryTotalsAsync(dailyInventory.Id);
                }
                catch (Exception totalsEx)
                {
                    _logger.LogError(totalsEx, $"Error updating daily inventory totals for {date:yyyy-MM-dd}");
                }

                await _context.SaveChangesAsync();

                return dailyInventory;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error recalculating inventory for {date:yyyy-MM-dd}");
                throw;
            }
        }

        public async Task<byte[]> ExportToExcelAsync(DateTime date)
        {
            try
            {
                // EPPlus license is set in appsettings.json
                using var package = new ExcelPackage();
                
                // Get transactions for the day (since invoices might be stored as transactions)
                var startTime = date;
                var endTime = date.AddDays(1).AddTicks(-1);

                // First try to get from Invoices table
                var invoices = await _context.Invoices
                    .Include(i => i.Customer)
                    .Include(i => i.Items)
                        .ThenInclude(ii => ii.Product)
                    .Where(i => i.InvoiceDate >= startTime && i.InvoiceDate <= endTime)
                    .OrderByDescending(i => i.InvoiceDate)
                    .ToListAsync();

                // If no invoices found, get from CustomerTransactions and convert to invoice format
                if (!invoices.Any())
                {
                    var transactions = await _context.CustomerTransactions
                        .Include(t => t.Customer)
                        .Include(t => t.Product)
                        .Where(t => t.Date >= startTime && t.Date <= endTime)
                        .OrderByDescending(t => t.Date)
                        .ToListAsync();

                    // Group transactions by customer and date to create virtual invoices
                    var groupedTransactions = transactions
                        .GroupBy(t => new { t.CustomerId, Date = t.Date.Date, Hour = t.Date.Hour })
                        .Select(g => new
                        {
                            Customer = g.First().Customer,
                            Transactions = g.ToList(),
                            InvoiceDate = g.Key.Date.AddHours(g.Key.Hour),
                            InvoiceNumber = $"TXN-{g.Key.Date:yyyyMMdd}-{g.Key.Hour:D2}-{g.First().CustomerId}",
                            OrderNumber = $"ORD-{g.Key.Date:yyyyMMdd}-{g.Key.Hour:D2}-{g.First().CustomerId}"
                        })
                        .ToList();

                    // Convert to invoice-like objects
                    invoices = groupedTransactions.Select(g => new Invoice
                    {
                        Id = g.Transactions.First().Id, // Use transaction ID as temporary ID
                        CustomerId = g.Transactions.First().CustomerId,
                        Customer = g.Customer,
                        InvoiceNumber = g.InvoiceNumber,
                        OrderNumber = g.OrderNumber,
                        InvoiceDate = g.InvoiceDate,
                        TotalAmount = g.Transactions.Sum(t => t.Price * t.Quantity), // المبلغ الإجمالي قبل الخصم
                        Discount = g.Transactions.Sum(t => t.Discount),
                        AmountPaid = g.Transactions.Sum(t => t.AmountPaid),
                        RemainingAmount = g.Transactions.Sum(t => t.TotalPrice - t.AmountPaid),
                        Type = InvoiceType.Sale,
                        Items = g.Transactions.Select(t => new InvoiceItem
                        {
                            ProductId = t.ProductId,
                            Product = t.Product,
                            Quantity = t.Quantity,
                            UnitPrice = t.Price,
                            Discount = t.Discount,
                            TotalPrice = t.TotalPrice
                        }).ToList()
                    }).ToList();
                }

                _logger.LogInformation($"Found {invoices.Count} invoices for date {date:yyyy-MM-dd}");

                // If no invoices found, create a simple summary
                if (!invoices.Any())
                {
                    var noDataSheet = package.Workbook.Worksheets.Add("لا توجد فواتير");
                    noDataSheet.Cells[1, 1].Value = $"لا توجد فواتير ليوم {date:yyyy-MM-dd}";
                    noDataSheet.Cells[1, 1].Style.Font.Size = 16;
                    noDataSheet.Cells[1, 1].Style.Font.Bold = true;
                    noDataSheet.Cells.AutoFitColumns();
                    return package.GetAsByteArray();
                }

                // Summary Sheet
                var summarySheet = package.Workbook.Worksheets.Add("ملخص اليوم");
                await CreateInvoicesSummarySheet(summarySheet, invoices, date);

                // Invoices Sheet
                var invoicesSheet = package.Workbook.Worksheets.Add("الفواتير");
                await CreateInvoicesSheet(invoicesSheet, invoices);

                // Invoice Items Sheet
                var itemsSheet = package.Workbook.Worksheets.Add("تفاصيل الفواتير");
                await CreateInvoiceItemsSheet(itemsSheet, invoices);

                // Customers Summary Sheet
                var customersSheet = package.Workbook.Worksheets.Add("ملخص العملاء");
                await CreateCustomersSummarySheet(customersSheet, invoices);

                return package.GetAsByteArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error exporting invoices to Excel for date {date:yyyy-MM-dd}");
                throw new Exception($"خطأ في تصدير الفواتير إلى Excel: {ex.Message}", ex);
            }
        }

        public async Task<byte[]> ExportDetailedExcelAsync(DateTime date)
        {
            var inventory = await GetInventoryByDateAsync(date);
            if (inventory == null)
            {
                throw new ArgumentException($"No inventory found for date {date:yyyy-MM-dd}");
            }

            // EPPlus license is set in appsettings.json
            using var package = new ExcelPackage();
            
            // Summary Sheet
            var summarySheet = package.Workbook.Worksheets.Add("ملخص اليوم");
            await CreateDetailedSummarySheet(summarySheet, inventory);

            // Transactions Sheet
            var transactionsSheet = package.Workbook.Worksheets.Add("المعاملات المفصلة");
            await CreateDetailedTransactionsSheet(transactionsSheet, inventory);

            // Products Sheet
            var productsSheet = package.Workbook.Worksheets.Add("المنتجات المفصلة");
            await CreateDetailedProductsSheet(productsSheet, inventory);

            // Customers Sheet
            var customersSheet = package.Workbook.Worksheets.Add("العملاء المفصلين");
            await CreateDetailedCustomersSheet(customersSheet, inventory);

            // Returns and Exchanges Sheet
            var returnsSheet = package.Workbook.Worksheets.Add("المرتجعات والاستبدالات");
            await CreateReturnsAndExchangesSheet(returnsSheet, inventory);

            // Financial Summary Sheet
            var financialSheet = package.Workbook.Worksheets.Add("الملخص المالي");
            await CreateFinancialSummarySheet(financialSheet, inventory);

            return package.GetAsByteArray();
        }

        public async Task<bool> IsDayClosedAsync(DateTime date)
        {
            var inventory = await _context.DailyInventories
                .FirstOrDefaultAsync(di => di.InventoryDate.Date == date.Date);
            
            return inventory?.Status == InventoryStatus.Closed;
        }

        public async Task<DailyInventory?> GetPreviousDayInventoryAsync(DateTime date)
        {
            var previousDate = date.Date.AddDays(-1);
            return await GetInventoryByDateAsync(previousDate);
        }

        public async Task<decimal> GetTotalSalesInRangeAsync(DateTime startDate, DateTime endDate)
        {
            return await _context.DailyInventories
                .Where(di => di.InventoryDate.Date >= startDate.Date && di.InventoryDate.Date <= endDate.Date)
                .SumAsync(di => di.TotalSales);
        }

        public async Task<List<DailyProductSummary>> GetTopSellingProductsAsync(DateTime date, int count = 10)
        {
            var inventory = await GetInventoryByDateAsync(date);
            if (inventory == null) return new List<DailyProductSummary>();

            return inventory.ProductSummaries
                .OrderByDescending(ps => ps.TotalQuantitySold)
                .Take(count)
                .ToList();
        }

        public async Task<List<DailyCustomerSummary>> GetTopCustomersAsync(DateTime date, int count = 10)
        {
            var inventory = await GetInventoryByDateAsync(date);
            if (inventory == null) return new List<DailyCustomerSummary>();

            return inventory.CustomerSummaries
                .OrderByDescending(cs => cs.TotalPurchases)
                .Take(count)
                .ToList();
        }

        // Private helper methods
        public async Task<DailyInventory> GetOrCreateInventoryAsync(DateTime date)
        {
            var inventoryDate = date.Date;
            
            var inventory = await _context.DailyInventories
                .FirstOrDefaultAsync(di => di.InventoryDate.Date == inventoryDate);

            if (inventory == null)
            {
                inventory = new DailyInventory
                {
                    InventoryDate = inventoryDate,
                    StartTime = inventoryDate, // 12:00 AM
                    EndTime = inventoryDate.AddDays(1).AddTicks(-1), // 11:59:59.999 PM
                    Status = InventoryStatus.Active,
                    CreatedAt = DateTime.Now
                };

                _context.DailyInventories.Add(inventory);
                await _context.SaveChangesAsync();
            }

            return inventory;
        }

        private async Task UpdateProductSummaryAsync(int dailyInventoryId, CustomerTransaction transaction)
        {
            try
            {
                var productSummary = await _context.DailyProductSummaries
                    .FirstOrDefaultAsync(dps => dps.DailyInventoryId == dailyInventoryId && dps.ProductId == transaction.ProductId);

                if (productSummary == null)
                {
                    productSummary = new DailyProductSummary
                    {
                        DailyInventoryId = dailyInventoryId,
                        ProductId = transaction.ProductId,
                        StartingQuantity = transaction.Product?.Quantity + transaction.Quantity ?? 0 // Quantity before sale
                    };
                    _context.DailyProductSummaries.Add(productSummary);
                }

                // حساب التكلفة الإجمالية - سعر الجملة لكل قطعة
                var costValue = (transaction.Product?.CartonPrice ?? 0) * transaction.Quantity;

                // تحديث الكميات مع مراعاة عمليات الاستبدال والاسترجاع
                // فقط الكميات الموجبة (البيع) تحسب في TotalQuantitySold
                if (transaction.Quantity > 0)
                {
                    productSummary.TotalQuantitySold += transaction.Quantity;
                }
                productSummary.TotalSalesValue += transaction.Price * transaction.Quantity; // المبيعات قبل الخصم
                productSummary.TotalCostValue += costValue; // التكلفة السالبة ستخصم تلقائياً
                
                // الخصم يحسب فقط للبيع، وليس للإرجاع (الخصم الأصلي لا يُعتبر خسارة في الإرجاع)
                if (transaction.Quantity > 0)
                {
                    productSummary.TotalDiscounts += transaction.Discount;
                }
                // للإرجاع: لا نضيف الخصم إلى TotalDiscounts لأن الخصم الأصلي لا يُعتبر خسارة
                
                // صافي المبيعات: TotalPrice يحتوي على الخصم بالفعل
                var netSalesValue = Math.Round(transaction.TotalPrice, 2);
                
                productSummary.NetSalesValue += netSalesValue; // صافي المبيعات مع الاستبدال
                // حساب الربح لكل قطعة: سعر البيع - سعر الجملة (مع مراعاة الإرجاع)
                if (transaction.Product?.CartonPrice > 0)
                {
                    var profitPerUnit = transaction.Price - (transaction.Product.CartonPrice ?? 0);
                    // للبيع: الربح موجب، للإرجاع: الربح سالب (يتم خصمه)
                    var profitAmount = Math.Round(profitPerUnit * transaction.Quantity, 2);
                    productSummary.NetProfit += profitAmount;
                }
                productSummary.TransactionsCount += 1;
                productSummary.EndingQuantity = transaction.Product?.Quantity ?? 0; // Current quantity after sale

                // تسجيل تفاصيل إضافية للاستبدال والاسترجاع
                if (transaction.Quantity < 0)
                {
                    _logger.LogInformation($"RETURN/EXCHANGE: Product {transaction.ProductId} - Quantity: {transaction.Quantity}, Value: {transaction.TotalPrice}, Net: {netSalesValue}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating product summary for product {transaction.ProductId} in daily inventory {dailyInventoryId}");
                throw;
            }
        }

        private async Task UpdateCustomerSummaryAsync(int dailyInventoryId, CustomerTransaction transaction)
        {
            try
            {
                var customerSummary = await _context.DailyCustomerSummaries
                    .FirstOrDefaultAsync(dcs => dcs.DailyInventoryId == dailyInventoryId && dcs.CustomerId == transaction.CustomerId);

                if (customerSummary == null)
                {
                    customerSummary = new DailyCustomerSummary
                    {
                        DailyInventoryId = dailyInventoryId,
                        CustomerId = transaction.CustomerId
                    };
                    _context.DailyCustomerSummaries.Add(customerSummary);
                }

                // TotalPrice يتضمن الخصم بالفعل، لذلك نأخذه كما هو
                var netPurchaseValue = transaction.TotalPrice;

                customerSummary.TransactionsCount += 1;
                customerSummary.TotalPurchases += netPurchaseValue; // القيم السالبة ستخصم تلقائياً من المشتريات
                // استبعاد معاملات الشحن والمرتجعات من حساب المدفوعات
                if (transaction.ShippingCost == 0 && transaction.AmountPaid > 0)
                {
                    customerSummary.TotalPayments += transaction.AmountPaid; // المدفوعات الفعلية فقط
                }
                customerSummary.DebtAmount = Math.Round(customerSummary.TotalPurchases - customerSummary.TotalPayments, 2);
                customerSummary.LastTransactionTime = transaction.Date;

                // تسجيل تفاصيل إضافية للاستبدال والاسترجاع
                if (transaction.TotalPrice < 0)
                {
                    _logger.LogInformation($"RETURN/EXCHANGE: Customer {transaction.CustomerId} - Net Purchase: {netPurchaseValue}, Amount Paid: {transaction.AmountPaid}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating customer summary for customer {transaction.CustomerId} in daily inventory {dailyInventoryId}");
                throw;
            }
        }

        private async Task UpdateDailyInventoryTotalsAsync(int dailyInventoryId)
        {
            var inventory = await _context.DailyInventories.FindAsync(dailyInventoryId);
            if (inventory == null) return;

            var productSummaries = await _context.DailyProductSummaries
                .Where(dps => dps.DailyInventoryId == dailyInventoryId)
                .ToListAsync();

            var customerSummaries = await _context.DailyCustomerSummaries
                .Where(dcs => dcs.DailyInventoryId == dailyInventoryId)
                .ToListAsync();

            // حساب المبيعات الإجمالية قبل الخصم
            inventory.TotalSales = Math.Round(productSummaries.Sum(ps => ps.TotalSalesValue), 2);
            inventory.TotalCost = Math.Round(productSummaries.Sum(ps => ps.TotalCostValue), 2);
            // الخصم يظهر منفصل، لا نخصمه من الربح
            var grossProfit = Math.Round(productSummaries.Sum(ps => ps.NetProfit), 2);
            var totalDiscounts = Math.Round(productSummaries.Sum(ps => ps.TotalDiscounts), 2);
            inventory.NetProfit = grossProfit; // الربح بدون خصم الخصومات
            inventory.TotalDiscounts = totalDiscounts;
            inventory.TotalPayments = Math.Round(customerSummaries.Sum(cs => cs.TotalPayments), 2);
            // حساب الديون الفعلية: مجموع (TotalPrice - AmountPaid) لكل معاملة
            // لكن نضمن أن المجموع صحيح من خلال التحقق من التطابق
            // حساب الديون الفعلية: مجموع (TotalPrice - AmountPaid) لكل معاملة
            var calculatedTotalDebts = customerSummaries.Sum(cs => cs.DebtAmount);
            
            inventory.TotalDebts = Math.Round(calculatedTotalDebts, 2);
            inventory.TransactionsCount = productSummaries.Sum(ps => ps.TransactionsCount);
            inventory.CustomersCount = customerSummaries.Count;
            inventory.ProductsSoldCount = productSummaries.Count(ps => ps.TotalQuantitySold > 0);
            inventory.TotalQuantitySold = productSummaries.Sum(ps => ps.TotalQuantitySold);
            inventory.UpdatedAt = DateTime.Now;
        }

        // Excel creation helper methods
        private async Task CreateSummarySheet(ExcelWorksheet sheet, DailyInventory inventory)
        {
            sheet.Cells["A1"].Value = "تقرير الجرد اليومي";
            sheet.Cells["A1:E1"].Merge = true;
            sheet.Cells["A1"].Style.Font.Size = 16;
            sheet.Cells["A1"].Style.Font.Bold = true;
            sheet.Cells["A1"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            sheet.Cells["A3"].Value = "التاريخ:";
            sheet.Cells["B3"].Value = inventory.InventoryDate.ToString("yyyy-MM-dd");
            
            // استخدام CustomerTransactions بدلاً من DailyInventory المحفوظة للحصول على الأرقام الصحيحة
            var startTime = inventory.InventoryDate;
            var endTime = inventory.InventoryDate.AddDays(1).AddTicks(-1);

            var transactions = await _context.CustomerTransactions
                .Include(ct => ct.Product)
                .Include(ct => ct.Customer)
                .Where(ct => ct.Date >= startTime && ct.Date <= endTime)
                .ToListAsync();

            // حساب المبيعات الإجمالية (قبل الخصم) - فقط للمعاملات الإيجابية
            var totalSales = transactions.Where(t => t.Quantity > 0).Sum(t => t.Price * t.Quantity);
            var totalCost = transactions.Sum(t => (t.Product?.CartonPrice ?? 0) * t.Quantity);
            // حساب الربح لكل قطعة: سعر البيع - سعر الجملة
            var grossProfit = transactions.Sum(t => 
            {
                    if (t.Product?.CartonPrice > 0)
                {
                    var profitPerUnit = t.Price - (t.Product.CartonPrice ?? 0);
                    return profitPerUnit * t.Quantity;
                }
                return 0;
            });
            var totalDiscounts = transactions.Where(t => t.Quantity > 0).Sum(t => t.Discount); // الخصم فقط للبيع
            var netProfit = grossProfit; // الربح بدون خصم الخصومات
            var totalPayments = transactions.Where(t => t.Quantity > 0 && t.ShippingCost == 0).Sum(t => t.AmountPaid); // المدفوعات فقط للبيع (استبعاد الشحن)
            // حساب المديونيات
            var totalDebts = transactions.Sum(t => t.TotalPrice - t.AmountPaid);
            var transactionsCount = transactions.Count;
            var customersCount = transactions.Select(t => t.CustomerId).Distinct().Count();
            var productsSoldCount = transactions.Select(t => t.ProductId).Distinct().Count();
            var totalQuantitySold = transactions.Sum(t => t.Quantity);
            
            sheet.Cells["A4"].Value = "إجمالي المبيعات:";
            sheet.Cells["B4"].Value = totalSales;
            sheet.Cells["B4"].Style.Numberformat.Format = "#,##0.00";

            sheet.Cells["A5"].Value = "إجمالي التكلفة:";
            sheet.Cells["B5"].Value = totalCost;
            sheet.Cells["B5"].Style.Numberformat.Format = "#,##0.00";

            sheet.Cells["A6"].Value = "صافي الربح:";
            sheet.Cells["B6"].Value = netProfit;
            sheet.Cells["B6"].Style.Numberformat.Format = "#,##0.00";

            sheet.Cells["A7"].Value = "إجمالي الخصومات:";
            sheet.Cells["B7"].Value = totalDiscounts;
            sheet.Cells["B7"].Style.Numberformat.Format = "#,##0.00";

            sheet.Cells["A8"].Value = "إجمالي المدفوعات:";
            sheet.Cells["B8"].Value = totalPayments;
            sheet.Cells["B8"].Style.Numberformat.Format = "#,##0.00";

            // إضافة مجموع مدفوعات اليوم (استبعاد معاملات الشحن)
            var totalPaymentsToday = transactions
                .Where(t => t.ShippingCost == 0 && t.AmountPaid > 0) // استبعاد معاملات الشحن والمرتجعات
                .Sum(t => t.AmountPaid);
            sheet.Cells["A9"].Value = "مجموع مدفوعات اليوم:";
            sheet.Cells["B9"].Value = totalPaymentsToday;
            sheet.Cells["B9"].Style.Numberformat.Format = "#,##0.00";

            sheet.Cells["A10"].Value = "إجمالي المديونيات:";
            sheet.Cells["B10"].Value = totalDebts;
            sheet.Cells["B10"].Style.Numberformat.Format = "#,##0.00";

            sheet.Cells["A11"].Value = "عدد المعاملات:";
            sheet.Cells["B11"].Value = transactionsCount;

            sheet.Cells["A12"].Value = "عدد العملاء:";
            sheet.Cells["B12"].Value = customersCount;

            sheet.Cells["A13"].Value = "عدد المنتجات المباعة:";
            sheet.Cells["B13"].Value = productsSoldCount;

            sheet.Cells["A14"].Value = "إجمالي الكمية المباعة:";
            sheet.Cells["B14"].Value = totalQuantitySold;

            sheet.Cells.AutoFitColumns();
        }

        private async Task CreateTransactionsSheet(ExcelWorksheet sheet, DailyInventory inventory)
        {
            // Headers
            var headers = new[] { "الوقت", "العميل", "المنتج", "الكمية", "سعر الوحدة", "الإجمالي", "الخصم", "الصافي", "المدفوع", "المتبقي" };
            for (int i = 0; i < headers.Length; i++)
            {
                sheet.Cells[1, i + 1].Value = headers[i];
                sheet.Cells[1, i + 1].Style.Font.Bold = true;
                sheet.Cells[1, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                sheet.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(Color.LightBlue);
            }

            // استخدام CustomerTransactions بدلاً من DailySaleTransactions للحصول على الأرقام الصحيحة
            var startTime = inventory.InventoryDate;
            var endTime = inventory.InventoryDate.AddDays(1).AddTicks(-1);

            var transactions = await _context.CustomerTransactions
                .Include(ct => ct.Customer)
                .Include(ct => ct.Product)
                .Where(ct => ct.Date >= startTime && ct.Date <= endTime)
                .OrderBy(ct => ct.Date)
                .ToListAsync();

            for (int i = 0; i < transactions.Count; i++)
            {
                var transaction = transactions[i];
                var row = i + 2;
                // TotalPrice already includes discount, so we don't subtract it again
                var netAmount = transaction.TotalPrice;
                var remaining = Math.Round(transaction.TotalPrice - transaction.AmountPaid, 2);

                sheet.Cells[row, 1].Value = transaction.Date.ToString("HH:mm:ss");
                sheet.Cells[row, 2].Value = transaction.Customer?.Name ?? "";
                sheet.Cells[row, 3].Value = transaction.Product?.Name ?? "";
                sheet.Cells[row, 4].Value = transaction.Quantity;
                sheet.Cells[row, 5].Value = transaction.Price;
                sheet.Cells[row, 6].Value = transaction.TotalPrice;
                sheet.Cells[row, 7].Value = transaction.Discount;
                sheet.Cells[row, 8].Value = netAmount;
                sheet.Cells[row, 9].Value = transaction.AmountPaid;
                sheet.Cells[row, 10].Value = remaining;

                // Format currency columns
                for (int col = 5; col <= 10; col++)
                {
                    sheet.Cells[row, col].Style.Numberformat.Format = "#,##0.00";
                }
            }

            sheet.Cells.AutoFitColumns();
        }

        private async Task CreateProductsSheet(ExcelWorksheet sheet, DailyInventory inventory)
        {
            var headers = new[] { "المنتج", "الكمية المباعة", "الكمية المرتجعة", "قيمة المبيعات", "التكلفة", "الخصومات", "صافي المبيعات", "صافي الربح", "عدد المعاملات" };
            for (int i = 0; i < headers.Length; i++)
            {
                sheet.Cells[1, i + 1].Value = headers[i];
                sheet.Cells[1, i + 1].Style.Font.Bold = true;
                sheet.Cells[1, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                sheet.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(Color.LightGreen);
            }

            // استخدام CustomerTransactions بدلاً من DailyProductSummaries للحصول على الأرقام الصحيحة
            var startTime = inventory.InventoryDate;
            var endTime = inventory.InventoryDate.AddDays(1).AddTicks(-1);

            var transactions = await _context.CustomerTransactions
                .Include(ct => ct.Product)
                .Where(ct => ct.Date >= startTime && ct.Date <= endTime)
                .ToListAsync();

            var productGroups = transactions
                .GroupBy(ct => ct.ProductId)
                .Select(g => new
                {
                    Product = g.First().Product,
                    TotalQuantitySold = g.Where(ct => ct.Quantity > 0).Sum(ct => ct.Quantity), // فقط الكميات الموجبة (البيع)
                    TotalQuantityReturned = g.Where(ct => ct.Quantity < 0).Sum(ct => Math.Abs(ct.Quantity)), // الكميات المرتجعة
                    TotalSalesValue = g.Sum(ct => ct.TotalPrice),
                    TotalCostValue = g.Sum(ct => (ct.Product?.CartonPrice ?? 0) * ct.Quantity),
                    TotalDiscounts = g.Where(ct => ct.Quantity > 0).Sum(ct => ct.Discount), // الخصم فقط للبيع
                    NetSalesValue = g.Sum(ct => ct.TotalPrice), // TotalPrice يحتوي على الخصم بالفعل
                    TransactionsCount = g.Count()
                })
                .OrderByDescending(p => p.TotalQuantitySold)
                .ToList();

            for (int i = 0; i < productGroups.Count; i++)
            {
                var product = productGroups[i];
                var row = i + 2;
                var netProfit = Math.Round(product.NetSalesValue - product.TotalCostValue, 2);

                sheet.Cells[row, 1].Value = product.Product?.Name ?? "";
                sheet.Cells[row, 2].Value = product.TotalQuantitySold;
                sheet.Cells[row, 3].Value = product.TotalQuantityReturned;
                sheet.Cells[row, 4].Value = product.TotalSalesValue;
                sheet.Cells[row, 5].Value = product.TotalCostValue;
                sheet.Cells[row, 6].Value = product.TotalDiscounts;
                sheet.Cells[row, 7].Value = product.NetSalesValue;
                sheet.Cells[row, 8].Value = netProfit;
                sheet.Cells[row, 9].Value = product.TransactionsCount;

                // Format currency columns
                for (int col = 4; col <= 8; col++)
                {
                    sheet.Cells[row, col].Style.Numberformat.Format = "#,##0.00";
                }
            }

            sheet.Cells.AutoFitColumns();
        }

        private async Task CreateCustomersSheet(ExcelWorksheet sheet, DailyInventory inventory)
        {
            var headers = new[] { "العميل", "عدد المعاملات", "إجمالي المشتريات", "إجمالي المدفوعات", "المديونية", "آخر معاملة" };
            for (int i = 0; i < headers.Length; i++)
            {
                sheet.Cells[1, i + 1].Value = headers[i];
                sheet.Cells[1, i + 1].Style.Font.Bold = true;
                sheet.Cells[1, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                sheet.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(Color.LightYellow);
            }

            // استخدام CustomerTransactions بدلاً من DailyCustomerSummaries للحصول على الأرقام الصحيحة
            var startTime = inventory.InventoryDate;
            var endTime = inventory.InventoryDate.AddDays(1).AddTicks(-1);

            var transactions = await _context.CustomerTransactions
                .Include(ct => ct.Customer)
                .Where(ct => ct.Date >= startTime && ct.Date <= endTime)
                .ToListAsync();

            var customerGroups = transactions
                .GroupBy(ct => ct.CustomerId)
                .Select(g => new
                {
                    Customer = g.First().Customer,
                    TransactionsCount = g.Count(),
                    TotalPurchases = g.Sum(ct => ct.TotalPrice), // TotalPrice يحتوي على الخصم بالفعل
                    TotalPayments = g.Where(ct => ct.ShippingCost == 0 && ct.AmountPaid > 0).Sum(ct => ct.AmountPaid), // استبعاد معاملات الشحن والمرتجعات
                    LastTransactionTime = g.Max(ct => ct.Date)
                })
                .OrderByDescending(c => c.TotalPurchases)
                .ToList();

            for (int i = 0; i < customerGroups.Count; i++)
            {
                var customer = customerGroups[i];
                var row = i + 2;
                var debtAmount = Math.Round(customer.TotalPurchases - customer.TotalPayments, 2);

                sheet.Cells[row, 1].Value = customer.Customer?.Name ?? "";
                sheet.Cells[row, 2].Value = customer.TransactionsCount;
                sheet.Cells[row, 3].Value = customer.TotalPurchases;
                sheet.Cells[row, 4].Value = customer.TotalPayments;
                sheet.Cells[row, 5].Value = debtAmount;
                sheet.Cells[row, 6].Value = customer.LastTransactionTime.ToString("HH:mm:ss");

                // Format currency columns
                for (int col = 3; col <= 5; col++)
                {
                    sheet.Cells[row, col].Style.Numberformat.Format = "#,##0.00";
                }
            }

            sheet.Cells.AutoFitColumns();
        }

        // Detailed Excel creation helper methods
        private async Task CreateDetailedSummarySheet(ExcelWorksheet sheet, DailyInventory inventory)
        {
            sheet.Cells["A1"].Value = "تقرير الجرد اليومي المفصل";
            sheet.Cells["A1:E1"].Merge = true;
            sheet.Cells["A1"].Style.Font.Size = 18;
            sheet.Cells["A1"].Style.Font.Bold = true;
            sheet.Cells["A1"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            sheet.Cells["A3"].Value = "التاريخ:";
            sheet.Cells["B3"].Value = inventory.InventoryDate.ToString("yyyy-MM-dd");
            
            // استخدام CustomerTransactions بدلاً من DailyInventory المحفوظة للحصول على الأرقام الصحيحة
            var startTime = inventory.InventoryDate;
            var endTime = inventory.InventoryDate.AddDays(1).AddTicks(-1);

            var transactions = await _context.CustomerTransactions
                .Include(ct => ct.Product)
                .Include(ct => ct.Customer)
                .Where(ct => ct.Date >= startTime && ct.Date <= endTime)
                .ToListAsync();

            // حساب المبيعات الإجمالية (قبل الخصم) - فقط للمعاملات الإيجابية
            var totalSales = transactions.Where(t => t.Quantity > 0).Sum(t => t.Price * t.Quantity);
            var totalCost = transactions.Sum(t => (t.Product?.CartonPrice ?? 0) * t.Quantity);
            // حساب الربح لكل قطعة: سعر البيع - سعر الجملة
            var grossProfit = transactions.Sum(t => 
            {
                    if (t.Product?.CartonPrice > 0)
                {
                    var profitPerUnit = t.Price - (t.Product.CartonPrice ?? 0);
                    return profitPerUnit * t.Quantity;
                }
                return 0;
            });
            var totalDiscounts = transactions.Where(t => t.Quantity > 0).Sum(t => t.Discount); // الخصم فقط للبيع
            var netProfit = grossProfit; // الربح بدون خصم الخصومات
            var totalPayments = transactions.Where(t => t.Quantity > 0 && t.ShippingCost == 0).Sum(t => t.AmountPaid); // المدفوعات فقط للبيع (استبعاد الشحن)
            // حساب المديونيات
            var totalDebts = transactions.Sum(t => t.TotalPrice - t.AmountPaid);
            var transactionsCount = transactions.Count;
            var customersCount = transactions.Select(t => t.CustomerId).Distinct().Count();
            var productsSoldCount = transactions.Select(t => t.ProductId).Distinct().Count();
            var totalQuantitySold = transactions.Sum(t => t.Quantity);
            
            // حساب الاستبدالات والمرتجعات العادية
            var returnTransactions = transactions.Where(t => t.Quantity < 0).ToList();
            var exchangeTransactions = returnTransactions.Where(t => !string.IsNullOrEmpty(t.Notes) && t.Notes.Contains("استبدال")).ToList();
            var regularReturns = returnTransactions.Where(t => string.IsNullOrEmpty(t.Notes) || !t.Notes.Contains("استبدال")).ToList();
            
            var exchangeValue = exchangeTransactions.Sum(t => Math.Abs(t.TotalPrice));
            var regularReturnValue = regularReturns.Sum(t => Math.Abs(t.TotalPrice));
            
            sheet.Cells["A4"].Value = "إجمالي المبيعات:";
            sheet.Cells["B4"].Value = totalSales;
            sheet.Cells["B4"].Style.Numberformat.Format = "#,##0.00";

            sheet.Cells["A5"].Value = "إجمالي التكلفة:";
            sheet.Cells["B5"].Value = totalCost;
            sheet.Cells["B5"].Style.Numberformat.Format = "#,##0.00";

            sheet.Cells["A6"].Value = "صافي الربح:";
            sheet.Cells["B6"].Value = netProfit;
            sheet.Cells["B6"].Style.Numberformat.Format = "#,##0.00";

            sheet.Cells["A7"].Value = "إجمالي الخصومات:";
            sheet.Cells["B7"].Value = totalDiscounts;
            sheet.Cells["B7"].Style.Numberformat.Format = "#,##0.00";

            sheet.Cells["A8"].Value = "إجمالي المدفوعات:";
            sheet.Cells["B8"].Value = totalPayments;
            sheet.Cells["B8"].Style.Numberformat.Format = "#,##0.00";

            sheet.Cells["A9"].Value = "إجمالي المديونيات:";
            sheet.Cells["B9"].Value = totalDebts;
            sheet.Cells["B9"].Style.Numberformat.Format = "#,##0.00";

            sheet.Cells["A10"].Value = "قيمة المرتجعات العادية:";
            sheet.Cells["B10"].Value = regularReturnValue;
            sheet.Cells["B10"].Style.Numberformat.Format = "#,##0.00";

            sheet.Cells["A11"].Value = "قيمة الاستبدالات:";
            sheet.Cells["B11"].Value = exchangeValue;
            sheet.Cells["B11"].Style.Numberformat.Format = "#,##0.00";

            sheet.Cells["A12"].Value = "عدد المعاملات:";
            sheet.Cells["B12"].Value = transactionsCount;

            sheet.Cells["A13"].Value = "عدد العملاء:";
            sheet.Cells["B13"].Value = customersCount;

            sheet.Cells["A14"].Value = "عدد المنتجات المباعة:";
            sheet.Cells["B14"].Value = productsSoldCount;

            sheet.Cells["A15"].Value = "إجمالي الكمية المباعة:";
            sheet.Cells["B15"].Value = totalQuantitySold;

            sheet.Cells["A17"].Value = "معدل الربحية:";
            sheet.Cells["B17"].Value = totalSales > 0 ? (netProfit / totalSales * 100) : 0;
            sheet.Cells["B17"].Style.Numberformat.Format = "0.00%";

            sheet.Cells["A18"].Value = "متوسط قيمة المعاملة:";
            sheet.Cells["B18"].Value = transactionsCount > 0 ? (totalSales / transactionsCount) : 0;
            sheet.Cells["B18"].Style.Numberformat.Format = "#,##0.00";

            sheet.Cells.AutoFitColumns();
        }

        private async Task CreateDetailedTransactionsSheet(ExcelWorksheet sheet, DailyInventory inventory)
        {
            // Headers
            var headers = new[] { "الوقت", "العميل", "المنتج", "الكمية", "سعر الوحدة", "الإجمالي", "الخصم", "الصافي", "المدفوع", "المتبقي", "نوع العملية", "ملاحظات" };
            for (int i = 0; i < headers.Length; i++)
            {
                sheet.Cells[1, i + 1].Value = headers[i];
                sheet.Cells[1, i + 1].Style.Font.Bold = true;
                sheet.Cells[1, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                sheet.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(Color.LightBlue);
            }

            // استخدام CustomerTransactions بدلاً من DailySaleTransactions للحصول على الأرقام الصحيحة
            var startTime = inventory.InventoryDate;
            var endTime = inventory.InventoryDate.AddDays(1).AddTicks(-1);

            var transactions = await _context.CustomerTransactions
                .Include(ct => ct.Customer)
                .Include(ct => ct.Product)
                .Where(ct => ct.Date >= startTime && ct.Date <= endTime)
                .OrderBy(ct => ct.Date)
                .ToListAsync();

            for (int i = 0; i < transactions.Count; i++)
            {
                var transaction = transactions[i];
                var row = i + 2;
                // حساب المتبقي: الإجمالي - المدفوع
                var netAmount = transaction.TotalPrice;
                // إصلاح حساب المتبقي - استخدام المبلغ الصافي بدلاً من المدفوع الموزع
                var remaining = Math.Round(transaction.TotalPrice - transaction.AmountPaid, 2);
                var operationType = transaction.Quantity > 0 ? "بيع" : "إرجاع";

                sheet.Cells[row, 1].Value = transaction.Date.ToString("HH:mm:ss");
                sheet.Cells[row, 2].Value = transaction.Customer?.Name ?? "";
                sheet.Cells[row, 3].Value = transaction.Product?.Name ?? "";
                sheet.Cells[row, 4].Value = transaction.Quantity;
                sheet.Cells[row, 5].Value = transaction.Price;
                sheet.Cells[row, 6].Value = transaction.TotalPrice;
                sheet.Cells[row, 7].Value = transaction.Discount;
                sheet.Cells[row, 8].Value = netAmount;
                sheet.Cells[row, 9].Value = transaction.AmountPaid;
                sheet.Cells[row, 10].Value = remaining;
                sheet.Cells[row, 11].Value = operationType;
                sheet.Cells[row, 12].Value = transaction.Notes ?? "";

                // Format currency columns
                for (int col = 5; col <= 10; col++)
                {
                    sheet.Cells[row, col].Style.Numberformat.Format = "#,##0.00";
                }

                // Color coding for operation type
                if (operationType == "إرجاع")
                {
                    sheet.Cells[row, 11].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    sheet.Cells[row, 11].Style.Fill.BackgroundColor.SetColor(Color.LightCoral);
                }
            }

            sheet.Cells.AutoFitColumns();
        }

        private async Task CreateDetailedProductsSheet(ExcelWorksheet sheet, DailyInventory inventory)
        {
            var headers = new[] { "المنتج", "الكمية المباعة", "الكمية المرتجعة", "صافي الكمية", "قيمة المبيعات", "التكلفة", "الخصومات", "صافي المبيعات", "صافي الربح", "عدد المعاملات", "معدل الربحية" };
            for (int i = 0; i < headers.Length; i++)
            {
                sheet.Cells[1, i + 1].Value = headers[i];
                sheet.Cells[1, i + 1].Style.Font.Bold = true;
                sheet.Cells[1, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                sheet.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(Color.LightGreen);
            }

            // استخدام CustomerTransactions بدلاً من DailyProductSummaries للحصول على الأرقام الصحيحة
            var startTime = inventory.InventoryDate;
            var endTime = inventory.InventoryDate.AddDays(1).AddTicks(-1);

            var transactions = await _context.CustomerTransactions
                .Include(ct => ct.Product)
                .Where(ct => ct.Date >= startTime && ct.Date <= endTime)
                .ToListAsync();

            var productGroups = transactions
                .GroupBy(ct => ct.ProductId)
                .Select(g => new
                {
                    Product = g.First().Product,
                    SoldQuantity = g.Where(ct => ct.Quantity > 0).Sum(ct => ct.Quantity),
                    ReturnedQuantity = Math.Abs(g.Where(ct => ct.Quantity < 0).Sum(ct => ct.Quantity)),
                    TotalSalesValue = g.Sum(ct => ct.TotalPrice),
                    TotalCostValue = g.Sum(ct => (ct.Product?.CartonPrice ?? 0) * ct.Quantity),
                    TotalDiscounts = g.Where(ct => ct.Quantity > 0).Sum(ct => ct.Discount), // الخصم فقط للبيع
                    NetSalesValue = g.Sum(ct => ct.TotalPrice), // TotalPrice يحتوي على الخصم بالفعل
                    NetProfit = g.Sum(ct => 
                    {
                        if (ct.Product?.CartonPrice > 0)
                        {
                            var profitPerUnit = ct.Price - (ct.Product.CartonPrice ?? 0);
                            // للبيع (كمية موجبة): الربح موجب، للإرجاع (كمية سالبة): الربح سالب
                            return profitPerUnit * ct.Quantity;
                        }
                        return 0;
                    }),
                    TransactionsCount = g.Count()
                })
                .OrderByDescending(p => p.SoldQuantity)
                .ToList();

            for (int i = 0; i < productGroups.Count; i++)
            {
                var product = productGroups[i];
                var row = i + 2;
                var netQuantity = product.SoldQuantity - product.ReturnedQuantity;
                
                // حساب الربح لكل قطعة: سعر البيع - سعر الجملة
                decimal netProfit = product.NetProfit; // استخدام الربح المحسوب مسبقاً
                
                var profitMargin = product.NetSalesValue > 0 ? Math.Round((netProfit / product.NetSalesValue * 100), 2) : 0;

                sheet.Cells[row, 1].Value = product.Product?.Name ?? "";
                sheet.Cells[row, 2].Value = product.SoldQuantity;
                sheet.Cells[row, 3].Value = product.ReturnedQuantity;
                sheet.Cells[row, 4].Value = netQuantity;
                sheet.Cells[row, 5].Value = product.TotalSalesValue;
                sheet.Cells[row, 6].Value = product.TotalCostValue;
                sheet.Cells[row, 7].Value = product.TotalDiscounts;
                sheet.Cells[row, 8].Value = product.NetSalesValue;
                sheet.Cells[row, 9].Value = netProfit;
                sheet.Cells[row, 10].Value = product.TransactionsCount;
                sheet.Cells[row, 11].Value = profitMargin;

                // Format currency columns
                for (int col = 5; col <= 9; col++)
                {
                    sheet.Cells[row, col].Style.Numberformat.Format = "#,##0.00";
                }

                // Format percentage
                sheet.Cells[row, 11].Style.Numberformat.Format = "0.00%";
            }

            sheet.Cells.AutoFitColumns();
        }

        private async Task CreateDetailedCustomersSheet(ExcelWorksheet sheet, DailyInventory inventory)
        {
            var headers = new[] { "العميل", "رقم الهاتف", "عدد المعاملات", "إجمالي المشتريات", "إجمالي المدفوعات", "المديونية", "آخر معاملة", "متوسط قيمة المعاملة", "نسبة الدفع" };
            for (int i = 0; i < headers.Length; i++)
            {
                sheet.Cells[1, i + 1].Value = headers[i];
                sheet.Cells[1, i + 1].Style.Font.Bold = true;
                sheet.Cells[1, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                sheet.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(Color.LightYellow);
            }

            // استخدام CustomerTransactions بدلاً من DailyCustomerSummaries للحصول على الأرقام الصحيحة
            var startTime = inventory.InventoryDate;
            var endTime = inventory.InventoryDate.AddDays(1).AddTicks(-1);

            var transactions = await _context.CustomerTransactions
                .Include(ct => ct.Customer)
                .Where(ct => ct.Date >= startTime && ct.Date <= endTime)
                .ToListAsync();

            var customerGroups = transactions
                .GroupBy(ct => ct.CustomerId)
                .Select(g => new
                {
                    Customer = g.First().Customer,
                    TransactionsCount = g.Count(),
                    TotalPurchases = g.Sum(ct => ct.TotalPrice), // TotalPrice يحتوي على الخصم بالفعل
                    TotalPayments = g.Where(ct => ct.ShippingCost == 0 && ct.AmountPaid > 0).Sum(ct => ct.AmountPaid), // استبعاد معاملات الشحن والمرتجعات
                    LastTransactionTime = g.Max(ct => ct.Date)
                })
                .OrderByDescending(c => c.TotalPurchases)
                .ToList();

            for (int i = 0; i < customerGroups.Count; i++)
            {
                var customer = customerGroups[i];
                var row = i + 2;
                var debtAmount = Math.Round(customer.TotalPurchases - customer.TotalPayments, 2);
                var avgTransactionValue = customer.TransactionsCount > 0 ? Math.Round((customer.TotalPurchases / customer.TransactionsCount), 2) : 0;
                // Calculate payment ratio correctly - cap at 100% to avoid unrealistic percentages
                var paymentRatio = customer.TotalPurchases > 0 ? Math.Round(Math.Min((customer.TotalPayments / customer.TotalPurchases * 100), 100), 2) : 0;

                sheet.Cells[row, 1].Value = customer.Customer?.Name ?? "";
                sheet.Cells[row, 2].Value = customer.Customer?.PhoneNumber ?? "";
                sheet.Cells[row, 3].Value = customer.TransactionsCount;
                sheet.Cells[row, 4].Value = customer.TotalPurchases;
                sheet.Cells[row, 5].Value = customer.TotalPayments;
                sheet.Cells[row, 6].Value = debtAmount;
                sheet.Cells[row, 7].Value = customer.LastTransactionTime.ToString("HH:mm:ss");
                sheet.Cells[row, 8].Value = avgTransactionValue;
                sheet.Cells[row, 9].Value = paymentRatio;

                // Format currency columns
                for (int col = 4; col <= 6; col++)
                {
                    sheet.Cells[row, col].Style.Numberformat.Format = "#,##0.00";
                }

                // Format average transaction value
                sheet.Cells[row, 8].Style.Numberformat.Format = "#,##0.00";

                // Format percentage
                sheet.Cells[row, 9].Style.Numberformat.Format = "0.00%";
            }

            sheet.Cells.AutoFitColumns();
        }

        private async Task CreateReturnsAndExchangesSheet(ExcelWorksheet sheet, DailyInventory inventory)
        {
            var headers = new[] { "الوقت", "العميل", "المنتج", "الكمية", "القيمة", "نوع العملية", "سبب الإرجاع", "ملاحظات" };
            for (int i = 0; i < headers.Length; i++)
            {
                sheet.Cells[1, i + 1].Value = headers[i];
                sheet.Cells[1, i + 1].Style.Font.Bold = true;
                sheet.Cells[1, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                sheet.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(Color.LightCoral);
            }

            // استخدام CustomerTransactions بدلاً من DailySaleTransactions للحصول على الأرقام الصحيحة
            var startTime = inventory.InventoryDate;
            var endTime = inventory.InventoryDate.AddDays(1).AddTicks(-1);

            var returnTransactions = await _context.CustomerTransactions
                .Include(ct => ct.Customer)
                .Include(ct => ct.Product)
                .Where(ct => ct.Date >= startTime && ct.Date <= endTime && ct.Quantity < 0)
                .OrderBy(ct => ct.Date)
                .ToListAsync();

            for (int i = 0; i < returnTransactions.Count; i++)
            {
                var transaction = returnTransactions[i];
                var row = i + 2;
                var operationType = transaction.TotalPrice < 0 ? "استبدال" : "إرجاع";

                sheet.Cells[row, 1].Value = transaction.Date.ToString("HH:mm:ss");
                sheet.Cells[row, 2].Value = transaction.Customer?.Name ?? "";
                sheet.Cells[row, 3].Value = transaction.Product?.Name ?? "";
                sheet.Cells[row, 4].Value = Math.Abs(transaction.Quantity);
                sheet.Cells[row, 5].Value = Math.Abs(transaction.TotalPrice);
                sheet.Cells[row, 6].Value = operationType;
                sheet.Cells[row, 7].Value = ""; // Placeholder for return reason
                sheet.Cells[row, 8].Value = transaction.Notes ?? "";

                // Format currency column
                sheet.Cells[row, 5].Style.Numberformat.Format = "#,##0.00";
            }

            sheet.Cells.AutoFitColumns();
        }

        private async Task CreateFinancialSummarySheet(ExcelWorksheet sheet, DailyInventory inventory)
        {
            sheet.Cells["A1"].Value = "الملخص المالي المفصل";
            sheet.Cells["A1:C1"].Merge = true;
            sheet.Cells["A1"].Style.Font.Size = 16;
            sheet.Cells["A1"].Style.Font.Bold = true;
            sheet.Cells["A1"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            // استخدام CustomerTransactions بدلاً من DailyInventory المحفوظة للحصول على الأرقام الصحيحة
            var startTime = inventory.InventoryDate;
            var endTime = inventory.InventoryDate.AddDays(1).AddTicks(-1);

            var transactions = await _context.CustomerTransactions
                .Include(ct => ct.Product)
                .Include(ct => ct.Customer)
                .Where(ct => ct.Date >= startTime && ct.Date <= endTime)
                .ToListAsync();

            // حساب المبيعات الإجمالية (قبل الخصم) - فقط للمعاملات الإيجابية
            var totalSales = transactions.Where(t => t.Quantity > 0).Sum(t => t.Price * t.Quantity);
            var totalCost = transactions.Sum(t => (t.Product?.CartonPrice ?? 0) * t.Quantity);
            // حساب الربح لكل قطعة: سعر البيع - سعر الجملة
            var grossProfit = transactions.Sum(t => 
            {
                    if (t.Product?.CartonPrice > 0)
                {
                    var profitPerUnit = t.Price - (t.Product.CartonPrice ?? 0);
                    return profitPerUnit * t.Quantity;
                }
                return 0;
            });
            var totalDiscounts = transactions.Where(t => t.Quantity > 0).Sum(t => t.Discount); // الخصم فقط للبيع
            var netProfit = grossProfit; // الربح بدون خصم الخصومات
            var totalPayments = transactions.Where(t => t.Quantity > 0 && t.ShippingCost == 0).Sum(t => t.AmountPaid); // المدفوعات فقط للبيع (استبعاد الشحن)
            // حساب المديونيات
            var totalDebts = transactions.Sum(t => t.TotalPrice - t.AmountPaid);

            // Revenue Analysis
            sheet.Cells["A3"].Value = "تحليل الإيرادات";
            sheet.Cells["A3:C3"].Merge = true;
            sheet.Cells["A3"].Style.Font.Bold = true;
            sheet.Cells["A3"].Style.Fill.PatternType = ExcelFillStyle.Solid;
            sheet.Cells["A3"].Style.Fill.BackgroundColor.SetColor(Color.LightBlue);

            sheet.Cells["A4"].Value = "إجمالي المبيعات:";
            sheet.Cells["B4"].Value = totalSales;
            sheet.Cells["B4"].Style.Numberformat.Format = "#,##0.00";

            sheet.Cells["A5"].Value = "إجمالي الخصومات:";
            sheet.Cells["B5"].Value = totalDiscounts;
            sheet.Cells["B5"].Style.Numberformat.Format = "#,##0.00";

            sheet.Cells["A6"].Value = "صافي المبيعات:";
            sheet.Cells["B6"].Value = totalSales - totalDiscounts;
            sheet.Cells["B6"].Style.Numberformat.Format = "#,##0.00";

            // Cost Analysis
            sheet.Cells["A8"].Value = "تحليل التكاليف";
            sheet.Cells["A8:C8"].Merge = true;
            sheet.Cells["A8"].Style.Font.Bold = true;
            sheet.Cells["A8"].Style.Fill.PatternType = ExcelFillStyle.Solid;
            sheet.Cells["A8"].Style.Fill.BackgroundColor.SetColor(Color.LightGreen);

            sheet.Cells["A9"].Value = "إجمالي التكلفة:";
            sheet.Cells["B9"].Value = totalCost;
            sheet.Cells["B9"].Style.Numberformat.Format = "#,##0.00";

            sheet.Cells["A10"].Value = "صافي الربح:";
            sheet.Cells["B10"].Value = netProfit;
            sheet.Cells["B10"].Style.Numberformat.Format = "#,##0.00";

            sheet.Cells["A11"].Value = "معدل الربحية:";
            sheet.Cells["B11"].Value = totalSales > 0 ? (netProfit / totalSales * 100) : 0;
            sheet.Cells["B11"].Style.Numberformat.Format = "0.00%";

            // Payment Analysis
            sheet.Cells["A13"].Value = "تحليل المدفوعات";
            sheet.Cells["A13:C13"].Merge = true;
            sheet.Cells["A13"].Style.Font.Bold = true;
            sheet.Cells["A13"].Style.Fill.PatternType = ExcelFillStyle.Solid;
            sheet.Cells["A13"].Style.Fill.BackgroundColor.SetColor(Color.LightYellow);

            sheet.Cells["A14"].Value = "إجمالي المدفوعات:";
            sheet.Cells["B14"].Value = totalPayments;
            sheet.Cells["B14"].Style.Numberformat.Format = "#,##0.00";

            sheet.Cells["A15"].Value = "إجمالي المديونيات:";
            sheet.Cells["B15"].Value = totalDebts;
            sheet.Cells["B15"].Style.Numberformat.Format = "#,##0.00";

            sheet.Cells["A16"].Value = "نسبة الدفع:";
            // Calculate payment ratio correctly - if totalSales is 0, show 0%
            var paymentRatio = totalSales > 0 ? Math.Round(Math.Min((totalPayments / totalSales * 100), 100), 2) : 0;
            sheet.Cells["B16"].Value = paymentRatio;
            sheet.Cells["B16"].Style.Numberformat.Format = "0.00%";

            sheet.Cells.AutoFitColumns();
        }

        #region Invoice Export Methods

        private Task CreateInvoicesSummarySheet(ExcelWorksheet worksheet, List<Invoice> invoices, DateTime date)
        {
            // Header
            worksheet.Cells[1, 1].Value = "ملخص الجرد اليومي - الفواتير";
            worksheet.Cells[1, 1, 1, 6].Merge = true;
            worksheet.Cells[1, 1].Style.Font.Size = 16;
            worksheet.Cells[1, 1].Style.Font.Bold = true;
            worksheet.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            worksheet.Cells[2, 1].Value = $"التاريخ: {date:yyyy-MM-dd}";
            worksheet.Cells[2, 1, 2, 6].Merge = true;
            worksheet.Cells[2, 1].Style.Font.Size = 12;
            worksheet.Cells[2, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            // Summary data - separate sales from returns
            var totalInvoices = invoices.Count;
            var totalAmount = invoices.Sum(i => i.TotalAmount);
            var totalPaid = invoices.Sum(i => i.AmountPaid); // هذا لا يحتاج تغيير لأنه من الفواتير وليس المعاملات
            var totalRemaining = invoices.Sum(i => i.RemainingAmount);
            var totalDiscount = invoices.Sum(i => i.Discount);
            var uniqueCustomers = invoices.Select(i => i.CustomerId).Distinct().Count();
            
            // Calculate actual products sold (excluding returns)
            var actualProductsSold = invoices
                .SelectMany(i => i.Items)
                .Where(item => item.Quantity > 0) // Only positive quantities (sales)
                .Sum(item => item.Quantity);
            
            var actualProductsReturned = invoices
                .SelectMany(i => i.Items)
                .Where(item => item.Quantity < 0) // Only negative quantities (returns)
                .Sum(item => Math.Abs(item.Quantity));

            int row = 4;
            worksheet.Cells[row, 1].Value = "إجمالي الفواتير:";
            worksheet.Cells[row, 2].Value = totalInvoices;
            worksheet.Cells[row, 2].Style.Font.Bold = true;

            row++;
            worksheet.Cells[row, 1].Value = "إجمالي المبلغ:";
            worksheet.Cells[row, 2].Value = totalAmount;
            worksheet.Cells[row, 2].Style.Numberformat.Format = "#,##0.00";

            row++;
            worksheet.Cells[row, 1].Value = "إجمالي المدفوع:";
            worksheet.Cells[row, 2].Value = totalPaid;
            worksheet.Cells[row, 2].Style.Numberformat.Format = "#,##0.00";

            row++;
            worksheet.Cells[row, 1].Value = "إجمالي المتبقي:";
            worksheet.Cells[row, 2].Value = totalRemaining;
            worksheet.Cells[row, 2].Style.Numberformat.Format = "#,##0.00";

            row++;
            worksheet.Cells[row, 1].Value = "إجمالي الخصم:";
            worksheet.Cells[row, 2].Value = totalDiscount;
            worksheet.Cells[row, 2].Style.Numberformat.Format = "#,##0.00";

            row++;
            worksheet.Cells[row, 1].Value = "عدد العملاء:";
            worksheet.Cells[row, 2].Value = uniqueCustomers;
            worksheet.Cells[row, 2].Style.Font.Bold = true;

            row++;
            worksheet.Cells[row, 1].Value = "المنتجات المباعة:";
            worksheet.Cells[row, 2].Value = actualProductsSold;
            worksheet.Cells[row, 2].Style.Font.Bold = true;
            worksheet.Cells[row, 2].Style.Fill.PatternType = ExcelFillStyle.Solid;
            worksheet.Cells[row, 2].Style.Fill.BackgroundColor.SetColor(Color.LightGreen);

            row++;
            worksheet.Cells[row, 1].Value = "المنتجات المرتجعة:";
            worksheet.Cells[row, 2].Value = actualProductsReturned;
            worksheet.Cells[row, 2].Style.Font.Bold = true;
            worksheet.Cells[row, 2].Style.Fill.PatternType = ExcelFillStyle.Solid;
            worksheet.Cells[row, 2].Style.Fill.BackgroundColor.SetColor(Color.LightCoral);

            row++;
            worksheet.Cells[row, 1].Value = "صافي المنتجات:";
            worksheet.Cells[row, 2].Value = actualProductsSold - actualProductsReturned;
            worksheet.Cells[row, 2].Style.Font.Bold = true;
            worksheet.Cells[row, 2].Style.Fill.PatternType = ExcelFillStyle.Solid;
            worksheet.Cells[row, 2].Style.Fill.BackgroundColor.SetColor(Color.LightBlue);

            // Auto-fit columns
            worksheet.Cells.AutoFitColumns();
            return Task.CompletedTask;
        }

        private Task CreateInvoicesSheet(ExcelWorksheet worksheet, List<Invoice> invoices)
        {
            // Headers
            var headers = new[]
            {
                "رقم الفاتورة", "رقم الطلب", "التاريخ", "الوقت", "اسم العميل", 
                "رقم الهاتف", "المبلغ الإجمالي", "الخصم", "المدفوع", "المتبقي", 
                "نوع الفاتورة", "الحالة"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cells[1, i + 1].Value = headers[i];
                worksheet.Cells[1, i + 1].Style.Font.Bold = true;
                worksheet.Cells[1, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(Color.LightBlue);
            }

            // Data - Sort by date and time
            var sortedInvoices = invoices.OrderBy(i => i.InvoiceDate).ToList();
            
            for (int i = 0; i < sortedInvoices.Count; i++)
            {
                var invoice = sortedInvoices[i];
                var row = i + 2;

                worksheet.Cells[row, 1].Value = invoice.InvoiceNumber;
                worksheet.Cells[row, 2].Value = invoice.OrderNumber;
                worksheet.Cells[row, 3].Value = invoice.InvoiceDate.ToString("yyyy-MM-dd");
                worksheet.Cells[row, 4].Value = invoice.InvoiceDate.ToString("HH:mm:ss");
                worksheet.Cells[row, 5].Value = invoice.Customer?.Name ?? "غير محدد";
                worksheet.Cells[row, 6].Value = invoice.Customer?.PhoneNumber ?? "";
                worksheet.Cells[row, 7].Value = invoice.TotalAmount;
                worksheet.Cells[row, 7].Style.Numberformat.Format = "#,##0.00";
                worksheet.Cells[row, 8].Value = invoice.Discount;
                worksheet.Cells[row, 8].Style.Numberformat.Format = "#,##0.00";
                worksheet.Cells[row, 9].Value = invoice.AmountPaid;
                worksheet.Cells[row, 9].Style.Numberformat.Format = "#,##0.00";
                worksheet.Cells[row, 10].Value = invoice.RemainingAmount;
                worksheet.Cells[row, 10].Style.Numberformat.Format = "#,##0.00";
                worksheet.Cells[row, 11].Value = invoice.Type.ToString();
                worksheet.Cells[row, 12].Value = invoice.RemainingAmount <= 0 ? "مدفوعة" : "غير مدفوعة";
                
                // Add color coding for payment status
                if (invoice.RemainingAmount <= 0)
                {
                    worksheet.Cells[row, 12].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[row, 12].Style.Fill.BackgroundColor.SetColor(Color.LightGreen);
                }
                else
                {
                    worksheet.Cells[row, 12].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[row, 12].Style.Fill.BackgroundColor.SetColor(Color.LightCoral);
                }
            }

            // Auto-fit columns
            worksheet.Cells.AutoFitColumns();
            return Task.CompletedTask;
        }

        private Task CreateInvoiceItemsSheet(ExcelWorksheet worksheet, List<Invoice> invoices)
        {
            // Headers
            var headers = new[]
            {
                "رقم الفاتورة", "اسم العميل", "اسم المنتج", "الكمية", 
                "سعر الوحدة", "إجمالي السعر", "الخصم", "صافي السعر", "نوع المعاملة"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cells[1, i + 1].Value = headers[i];
                worksheet.Cells[1, i + 1].Style.Font.Bold = true;
                worksheet.Cells[1, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(Color.LightGreen);
            }

            // Data - Sort invoices by date and time, then items by product name
            var sortedInvoices = invoices.OrderBy(i => i.InvoiceDate).ToList();
            int row = 2;
            
            foreach (var invoice in sortedInvoices)
            {
                var sortedItems = invoice.Items.OrderBy(item => item.Product?.Name ?? "").ToList();
                
                foreach (var item in sortedItems)
                {
                    worksheet.Cells[row, 1].Value = invoice.InvoiceNumber;
                    worksheet.Cells[row, 2].Value = invoice.Customer?.Name ?? "غير محدد";
                    worksheet.Cells[row, 3].Value = item.Product?.Name ?? "غير محدد";
                    worksheet.Cells[row, 4].Value = item.Quantity;
                    worksheet.Cells[row, 5].Value = item.UnitPrice;
                    worksheet.Cells[row, 5].Style.Numberformat.Format = "#,##0.00";
                    worksheet.Cells[row, 6].Value = item.TotalPrice;
                    worksheet.Cells[row, 6].Style.Numberformat.Format = "#,##0.00";
                    worksheet.Cells[row, 7].Value = item.Discount;
                    worksheet.Cells[row, 7].Style.Numberformat.Format = "#,##0.00";
                    worksheet.Cells[row, 8].Value = item.TotalPrice;
                    worksheet.Cells[row, 8].Style.Numberformat.Format = "#,##0.00";
                    
                    // Determine transaction type based on quantity
                    string transactionType;
                    if (item.Quantity > 0)
                    {
                        transactionType = "بيع";
                        worksheet.Cells[row, 9].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        worksheet.Cells[row, 9].Style.Fill.BackgroundColor.SetColor(Color.LightGreen);
                    }
                    else if (item.Quantity < 0)
                    {
                        transactionType = "إرجاع";
                        worksheet.Cells[row, 9].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        worksheet.Cells[row, 9].Style.Fill.BackgroundColor.SetColor(Color.LightCoral);
                    }
                    else
                    {
                        transactionType = "صفر";
                        worksheet.Cells[row, 9].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        worksheet.Cells[row, 9].Style.Fill.BackgroundColor.SetColor(Color.LightYellow);
                    }
                    
                    worksheet.Cells[row, 9].Value = transactionType;
                    row++;
                }
            }

            // Auto-fit columns
            worksheet.Cells.AutoFitColumns();
            return Task.CompletedTask;
        }

        private Task CreateCustomersSummarySheet(ExcelWorksheet worksheet, List<Invoice> invoices)
        {
            // Headers
            var headers = new[]
            {
                "اسم العميل", "رقم الهاتف", "عدد الفواتير", "إجمالي المبلغ", 
                "إجمالي المدفوع", "إجمالي المتبقي", "آخر فاتورة"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cells[1, i + 1].Value = headers[i];
                worksheet.Cells[1, i + 1].Style.Font.Bold = true;
                worksheet.Cells[1, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(Color.LightYellow);
            }

            // Group by customer and sort by total amount (highest first)
            var customerSummaries = invoices
                .GroupBy(i => i.CustomerId)
                .Select(g => new
                {
                    Customer = g.First().Customer,
                    InvoiceCount = g.Count(),
                    TotalAmount = g.Sum(i => i.TotalAmount),
                    TotalPaid = g.Sum(i => i.AmountPaid), // هذا من الفواتير وليس المعاملات
                    TotalRemaining = g.Sum(i => i.RemainingAmount),
                    LastInvoice = g.Max(i => i.InvoiceDate)
                })
                .OrderByDescending(c => c.TotalAmount)
                .ThenBy(c => c.Customer?.Name ?? "")
                .ToList();

            // Data with color coding
            for (int i = 0; i < customerSummaries.Count; i++)
            {
                var summary = customerSummaries[i];
                var row = i + 2;

                worksheet.Cells[row, 1].Value = summary.Customer?.Name ?? "غير محدد";
                worksheet.Cells[row, 2].Value = summary.Customer?.PhoneNumber ?? "";
                worksheet.Cells[row, 3].Value = summary.InvoiceCount;
                worksheet.Cells[row, 4].Value = summary.TotalAmount;
                worksheet.Cells[row, 4].Style.Numberformat.Format = "#,##0.00";
                worksheet.Cells[row, 5].Value = summary.TotalPaid;
                worksheet.Cells[row, 5].Style.Numberformat.Format = "#,##0.00";
                worksheet.Cells[row, 6].Value = summary.TotalRemaining;
                worksheet.Cells[row, 6].Style.Numberformat.Format = "#,##0.00";
                worksheet.Cells[row, 7].Value = summary.LastInvoice.ToString("yyyy-MM-dd HH:mm");
                
                // Color coding for remaining amount
                if (summary.TotalRemaining > 0)
                {
                    worksheet.Cells[row, 6].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[row, 6].Style.Fill.BackgroundColor.SetColor(Color.LightCoral);
                }
                else
                {
                    worksheet.Cells[row, 6].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[row, 6].Style.Fill.BackgroundColor.SetColor(Color.LightGreen);
                }
            }

            // Auto-fit columns
            worksheet.Cells.AutoFitColumns();
            return Task.CompletedTask;
        }

        #endregion
    }
}
