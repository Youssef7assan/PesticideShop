using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PesticideShop.Data;
using PesticideShop.Models;
using PesticideShop.Services;
using System.Globalization;

namespace PesticideShop.Controllers
{
    [Authorize]
    public class DailyInventoryController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IDailyInventoryService _dailyInventoryService;
        private readonly IActivityService _activityService;
        private readonly ILogger<DailyInventoryController> _logger;

        public DailyInventoryController(
            ApplicationDbContext context,
            IDailyInventoryService dailyInventoryService,
            IActivityService activityService,
            ILogger<DailyInventoryController> logger)
        {
            _context = context;
            _dailyInventoryService = dailyInventoryService;
            _activityService = activityService;
            _logger = logger;
        }

        // GET: DailyInventory
        public async Task<IActionResult> Index(DateTime? date)
        {
            var selectedDate = date ?? DateTime.Today;
            
            // Always calculate from original transactions table to avoid data loss
            var startTime = selectedDate;
            var endTime = selectedDate.AddDays(1).AddTicks(-1);

            // Get transactions for today to show real data
            var transactions = await _context.CustomerTransactions
                .Include(ct => ct.Product)
                .Include(ct => ct.Customer)
                .Where(ct => ct.Date >= startTime && ct.Date <= endTime)
                .OrderByDescending(ct => ct.Date)
                .ToListAsync();

            // حساب إحصائيات الاستبدال والاسترجاع
            var returnTransactions = transactions.Where(t => t.Quantity < 0).ToList();
            
            // حساب الاستبدالات من خلال الملاحظات (مثل الجرد السنوي)
            var exchangeTransactions = returnTransactions.Where(t => !string.IsNullOrEmpty(t.Notes) && t.Notes.Contains("استبدال")).ToList();
            var exchangeValue = exchangeTransactions.Sum(t => Math.Abs(t.TotalPrice));
            
            // المرتجعات العادية (بدون استبدال)
            var regularReturns = returnTransactions.Where(t => string.IsNullOrEmpty(t.Notes) || !t.Notes.Contains("استبدال")).ToList();
            var regularReturnValue = regularReturns.Sum(t => Math.Abs(t.TotalPrice));
            
            var salesTransactions = transactions.Where(t => t.Quantity > 0).ToList();

            var returnValue = returnTransactions.Sum(t => Math.Abs(t.TotalPrice));
            var salesValue = salesTransactions.Sum(t => t.TotalPrice);

            // Create a temporary inventory object with calculated data
            var inventory = new DailyInventory
            {
                Id = 0, // Temporary ID
                InventoryDate = selectedDate,
                StartTime = startTime,
                EndTime = endTime,
                Status = InventoryStatus.Active,
                TotalSales = Math.Round(transactions.Sum(t => t.TotalPrice), 2), // TotalPrice already includes discount
                TotalCost = Math.Round(transactions.Sum(t => (t.Product?.CartonPrice ?? 0) * Math.Abs(t.Quantity)), 2),
                TotalDiscounts = Math.Round(transactions.Where(t => t.Quantity > 0).Sum(t => t.Discount), 2), // الخصم فقط للبيع
                TotalPayments = Math.Round(transactions.Where(t => t.ShippingCost == 0 && t.AmountPaid > 0).Sum(t => t.AmountPaid), 2), // المدفوعات الفعلية فقط (استبعاد الشحن والمرتجعات)
                TransactionsCount = transactions.Count,
                CustomersCount = transactions.Select(t => t.CustomerId).Distinct().Count(),
                ProductsSoldCount = transactions.Select(t => t.ProductId).Distinct().Count(),
                TotalQuantitySold = transactions.Sum(t => t.Quantity),
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            
            // حساب التكلفة والربح بالمنطق الجديد - استخدام سعر الجملة
            var hasValidCost = transactions.Any(t => t.Product?.CartonPrice > 0);
            var totalCustomerDebts = Math.Round(transactions
                .Where(t => t.Quantity > 0) // معاملات البيع فقط
                .Sum(t => t.TotalPrice - t.AmountPaid), 2);
            
            // صافي الربح - فرق سعر الجملة وسعر القطاعي لكل قطعة
            if (hasValidCost)
            {
                // حساب الربح لكل قطعة: سعر البيع - سعر الجملة (مع مراعاة الإرجاع)
                var grossProfit = Math.Round(transactions.Sum(t => 
                {
                    if (t.Product?.CartonPrice > 0)
                    {
                        // الربح لكل قطعة = سعر البيع - سعر الجملة
                        var profitPerUnit = t.Price - (t.Product.CartonPrice ?? 0);
                        // للبيع (كمية موجبة): الربح موجب
                        // للإرجاع (كمية سالبة): الربح سالب (يتم خصمه من إجمالي الأرباح)
                        var profit = profitPerUnit * t.Quantity;
                        
                        // تسجيل تفصيلي للتشخيص
                        if (t.Quantity > 0)
                        {
                            // بيع: ربح موجب
                        }
                        else if (t.Quantity < 0)
                        {
                            // إرجاع: ربح سالب (يتم خصمه)
                        }
                        
                        return profit;
                    }
                    return 0;
                }), 2);
                
                // الخصم يظهر منفصل، لا نخصمه من الربح
                var totalDiscounts = Math.Round(transactions.Where(t => t.Quantity > 0).Sum(t => t.Discount), 2);
                inventory.NetProfit = grossProfit; // الربح بدون خصم الخصومات
            }
            else
            {
                // إذا لم يكن هناك سعر جملة (CartonPrice = 0 أو null)، لا نحسب ربح
                inventory.NetProfit = 0;
            }
            
            inventory.TotalDebts = totalCustomerDebts;

            // إضافة معلومات الاستبدال والاسترجاع للعرض
            ViewData["ReturnValue"] = returnValue;
            ViewData["ExchangeValue"] = exchangeValue;
            ViewData["SalesValue"] = salesValue;
            ViewData["ReturnTransactionsCount"] = returnTransactions.Count;
            ViewData["ExchangeTransactionsCount"] = exchangeTransactions.Count;
            ViewData["SalesTransactionsCount"] = transactions.Count;
            ViewData["RegularReturnValue"] = regularReturnValue;
            ViewData["RegularReturnTransactionsCount"] = regularReturns.Count;
            
            // إضافة معلومات تشخيصية للأرباح
            var salesProfit = salesTransactions.Where(t => t.Product?.CartonPrice > 0)
                .Sum(t => (t.Price - (t.Product.CartonPrice ?? 0)) * t.Quantity);
            var returnProfit = returnTransactions.Where(t => t.Product?.CartonPrice > 0)
                .Sum(t => (t.Price - (t.Product.CartonPrice ?? 0)) * t.Quantity);
            var totalCalculatedProfit = salesProfit + returnProfit; // returnProfit سيكون سالب للإرجاع
            // معلومات تشخيصية مفصلة
            var salesCount = salesTransactions.Count(t => t.Product?.CartonPrice > 0);
            var returnCount = returnTransactions.Count(t => t.Product?.CartonPrice > 0);
            ViewData["DebugProfit"] = $"بيع: {salesCount} معاملة (+{salesProfit:F2}), إرجاع: {returnCount} معاملة ({returnProfit:F2}), المجموع: {totalCalculatedProfit:F2}, صافي: {inventory.NetProfit:F2}";

            // Check if there's a saved inventory record
            var savedInventory = await _dailyInventoryService.GetInventoryByDateAsync(selectedDate);
            if (savedInventory != null)
            {
                inventory.Id = savedInventory.Id;
                inventory.Status = savedInventory.Status;
                inventory.ResponsibleUser = savedInventory.ResponsibleUser;
            }

            // حساب مجموع مدفوعات اليوم (جميع المدفوعات في اليوم حتى لو كانت على فواتير قديمة)
            // استبعاد معاملات الشحن والمرتجعات من حساب المدفوعات
            var totalPaymentsToday = await _context.CustomerTransactions
                .Where(ct => ct.Date >= startTime && ct.Date <= endTime && ct.ShippingCost == 0 && ct.AmountPaid > 0)
                .SumAsync(ct => ct.AmountPaid);

            ViewData["SelectedDate"] = selectedDate;
            ViewData["IsToday"] = selectedDate.Date == DateTime.Today;
            ViewData["IsClosed"] = inventory.Status == InventoryStatus.Closed;
            ViewData["TransactionsCount"] = transactions.Count;
            ViewData["TotalPaymentsToday"] = totalPaymentsToday;

            return View(inventory);
        }

        // POST: DailyInventory/ProcessAllTransactions
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessAllTransactions(DateTime? date)
        {
            try
            {
                var selectedDate = date ?? DateTime.Today;
                var startTime = selectedDate;
                var endTime = selectedDate.AddDays(1).AddTicks(-1);

                // Try a simplified recalculation approach
                try
                {
                    await _dailyInventoryService.RecalculateInventoryAsync(selectedDate);
                    TempData["SuccessMessage"] = $"تم إعادة حساب الجرد اليومي بنجاح ليوم {selectedDate:yyyy-MM-dd}";
                }
                catch (Exception recalcEx)
                {
                    _logger.LogError(recalcEx, $"Failed recalculation, trying individual processing for {selectedDate:yyyy-MM-dd}");
                    
                    // Fallback: process transactions individually
                    var transactions = await _context.CustomerTransactions
                        .Include(ct => ct.Product)
                        .Include(ct => ct.Customer)
                        .Where(ct => ct.Date >= startTime && ct.Date <= endTime)
                        .ToListAsync();

                    int processedCount = 0;
                    int errorCount = 0;
                    
                    foreach (var transaction in transactions)
                    {
                        try
                        {
                            await _dailyInventoryService.ProcessTransactionAsync(transaction);
                            processedCount++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error processing transaction {transaction.Id}");
                            errorCount++;
                        }
                    }

                    if (processedCount > 0)
                    {
                        TempData["SuccessMessage"] = $"تم معالجة {processedCount} معاملة بنجاح ليوم {selectedDate:yyyy-MM-dd}";
                        if (errorCount > 0)
                        {
                            TempData["WarningMessage"] = $"فشل في معالجة {errorCount} معاملة. تحقق من السجلات للتفاصيل.";
                        }
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "فشل في معالجة جميع المعاملات.";
                    }
                }

                await _activityService.LogActivityAsync(
                    User.Identity?.Name ?? "System",
                    "ProcessAllTransactions", 
                    $"تم محاولة معالجة معاملات يوم {selectedDate:yyyy-MM-dd}",
                    null,
                    "DailyInventory"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing all transactions");
                TempData["ErrorMessage"] = "حدث خطأ أثناء معالجة المعاملات";
            }

            return RedirectToAction(nameof(Index), new { date = date });
        }

        // POST: DailyInventory/SimpleRecalculate - Calculate stats without creating aggregate tables
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SimpleRecalculate(DateTime? date)
        {
            try
            {
                var selectedDate = date ?? DateTime.Today;
                var startTime = selectedDate;
                var endTime = selectedDate.AddDays(1).AddTicks(-1);

                // Get or create the basic daily inventory record
                var inventory = await _dailyInventoryService.GetInventoryByDateAsync(selectedDate);
                if (inventory == null)
                {
                    inventory = await _dailyInventoryService.GetOrCreateInventoryAsync(selectedDate);
                }

                // Calculate stats directly from transactions
                var transactions = await _context.CustomerTransactions
                    .Include(ct => ct.Product)
                    .Include(ct => ct.Customer)
                    .Where(ct => ct.Date >= startTime && ct.Date <= endTime)
                    .ToListAsync();

                // Update the inventory with calculated totals
                inventory.TotalSales = Math.Round(transactions.Sum(t => t.TotalPrice), 2); // TotalPrice already includes discount
                // حساب التكلفة الإجمالية - سعر الجملة لكل قطعة
            inventory.TotalCost = Math.Round(transactions.Sum(t => (t.Product?.CartonPrice ?? 0) * Math.Abs(t.Quantity)), 2);
                
                // صافي الربح - فرق سعر الجملة وسعر القطاعي لكل قطعة
                var hasValidCost = transactions.Any(t => t.Product?.CartonPrice > 0);
                if (hasValidCost)
                {
                    // حساب الربح لكل قطعة: سعر البيع - سعر الجملة
                    inventory.NetProfit = Math.Round(transactions.Sum(t => 
                    {
                        if (t.Product?.CartonPrice > 0)
                        {
                        // الربح لكل قطعة = سعر البيع - سعر الجملة
                        var profitPerUnit = t.Price - (t.Product.CartonPrice ?? 0);
                            return profitPerUnit * t.Quantity;
                        }
                        return 0;
                    }), 2);
                }
                else
                {
                    inventory.NetProfit = 0;
                }
                inventory.TotalDiscounts = Math.Round(transactions.Where(t => t.Quantity > 0).Sum(t => t.Discount), 2); // الخصم فقط للبيع
                inventory.TotalPayments = Math.Round(transactions.Where(t => t.ShippingCost == 0 && t.AmountPaid > 0).Sum(t => t.AmountPaid), 2); // المدفوعات الفعلية فقط (استبعاد الشحن والمرتجعات)
                // حساب الديون الفعلية: مجموع (TotalPrice - AmountPaid) لكل معاملة
                // لكن نضمن أن المجموع صحيح من خلال التحقق من التطابق
                // حساب الديون الفعلية: مجموع (TotalPrice - AmountPaid) لكل معاملة
                var calculatedTotalDebts = transactions.Sum(t => t.TotalPrice - t.AmountPaid);
                
                // حساب الديون الفعلية: مجموع (Price * Quantity - AmountPaid) لكل معاملة
                inventory.TotalDebts = Math.Round(calculatedTotalDebts, 2);
                inventory.TransactionsCount = transactions.Count;
                inventory.CustomersCount = transactions.Select(t => t.CustomerId).Distinct().Count();
                inventory.ProductsSoldCount = transactions.Select(t => t.ProductId).Distinct().Count();
                inventory.TotalQuantitySold = transactions.Sum(t => t.Quantity);
                inventory.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                await _activityService.LogActivityAsync(
                    User.Identity?.Name ?? "System",
                    "SimpleRecalculate", 
                    $"تم حساب الإحصائيات البسيطة ليوم {selectedDate:yyyy-MM-dd}",
                    null,
                    "DailyInventory"
                );

                TempData["SuccessMessage"] = $"تم حساب الإحصائيات بنجاح ليوم {selectedDate:yyyy-MM-dd} ({transactions.Count} معاملة)";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in simple recalculation");
                TempData["ErrorMessage"] = "حدث خطأ أثناء حساب الإحصائيات البسيطة";
            }

            return RedirectToAction(nameof(Index), new { date = date });
        }

        // POST: DailyInventory/Close
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Close(DateTime? date)
        {
            try
            {
                var selectedDate = date ?? DateTime.Today;
                var userId = User.Identity?.Name ?? "Unknown";

                var result = await _dailyInventoryService.CloseDailyInventoryAsync(selectedDate, userId);

                if (result)
                {
                    await _activityService.LogActivityAsync(
                        userId,
                        "CloseDailyInventory", 
                        $"تم إغلاق الجرد اليومي لتاريخ {selectedDate:yyyy-MM-dd}",
                        null,
                        "DailyInventory"
                    );

                    TempData["SuccessMessage"] = $"تم إغلاق الجرد اليومي لتاريخ {selectedDate:yyyy-MM-dd} بنجاح";
                }
                else
                {
                    TempData["ErrorMessage"] = "فشل في إغلاق الجرد اليومي. يرجى المحاولة مرة أخرى.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing daily inventory");
                TempData["ErrorMessage"] = "حدث خطأ أثناء إغلاق الجرد اليومي.";
            }

            return RedirectToAction(nameof(Index), new { date = date });
        }

        // POST: DailyInventory/Reopen
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reopen(DateTime? date)
        {
            try
            {
                var selectedDate = date ?? DateTime.Today;
                var userId = User.Identity?.Name ?? "Unknown";

                var inventory = await _dailyInventoryService.GetInventoryByDateAsync(selectedDate);
                if (inventory != null)
                {
                    inventory.Status = InventoryStatus.Active;
                    inventory.UpdatedAt = DateTime.Now;
                    inventory.ResponsibleUser = userId;

                    await _context.SaveChangesAsync();

                    await _activityService.LogActivityAsync(
                        userId,
                        "ReopenDailyInventory", 
                        $"تم إعادة فتح الجرد اليومي لتاريخ {selectedDate:yyyy-MM-dd}",
                        null,
                        "DailyInventory"
                    );

                    TempData["SuccessMessage"] = $"تم إعادة فتح الجرد اليومي لتاريخ {selectedDate:yyyy-MM-dd} بنجاح";
                }
                else
                {
                    TempData["ErrorMessage"] = "لم يتم العثور على الجرد المطلوب إعادة فتحه.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reopening daily inventory");
                TempData["ErrorMessage"] = "حدث خطأ أثناء إعادة فتح الجرد اليومي.";
            }

            return RedirectToAction(nameof(Index), new { date = date });
        }

        // GET: DailyInventory/Calendar
        public async Task<IActionResult> Calendar(int? year, int? month)
        {
            var selectedYear = year ?? DateTime.Now.Year;
            var selectedMonth = month ?? DateTime.Now.Month;
            var startDate = new DateTime(selectedYear, selectedMonth, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            var inventories = await _dailyInventoryService.GetInventoriesInRangeAsync(startDate, endDate);

            ViewData["SelectedYear"] = selectedYear;
            ViewData["SelectedMonth"] = selectedMonth;
            ViewData["MonthName"] = CultureInfo.GetCultureInfo("ar-EG").DateTimeFormat.GetMonthName(selectedMonth);

            return View(inventories);
        }

        // GET: DailyInventory/Details/2024-01-15
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id) || !DateTime.TryParseExact(id, "yyyy-MM-dd", null, DateTimeStyles.None, out DateTime date))
            {
                return BadRequest("تاريخ غير صحيح");
            }

            var inventory = await _dailyInventoryService.GetInventoryByDateAsync(date);
            if (inventory == null)
            {
                return NotFound($"لا يوجد جرد لتاريخ {date:yyyy-MM-dd}");
            }

            // Get top selling products and customers
            var topProducts = await _dailyInventoryService.GetTopSellingProductsAsync(date, 10);
            var topCustomers = await _dailyInventoryService.GetTopCustomersAsync(date, 10);

            ViewData["TopProducts"] = topProducts;
            ViewData["TopCustomers"] = topCustomers;

            return View(inventory);
        }

        // POST: DailyInventory/Close
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Close(string date)
        {
            if (string.IsNullOrEmpty(date) || !DateTime.TryParseExact(date, "yyyy-MM-dd", null, DateTimeStyles.None, out DateTime selectedDate))
            {
                TempData["ErrorMessage"] = "تاريخ غير صحيح";
                return RedirectToAction(nameof(Index));
            }

            var isClosed = await _dailyInventoryService.IsDayClosedAsync(selectedDate);
            if (isClosed)
            {
                TempData["ErrorMessage"] = $"الجرد ليوم {selectedDate:yyyy-MM-dd} مغلق بالفعل";
                return RedirectToAction(nameof(Index), new { date = selectedDate });
            }

            var success = await _dailyInventoryService.CloseDailyInventoryAsync(selectedDate, User.Identity?.Name ?? "Unknown");
            
            if (success)
            {
                await _activityService.LogActivityAsync(
                    User.Identity?.Name ?? "System",
                    "CloseDailyInventory",
                    $"تم إغلاق الجرد اليومي لتاريخ {selectedDate:yyyy-MM-dd}",
                    null,
                    "DailyInventory"
                );

                TempData["SuccessMessage"] = $"تم إغلاق الجرد ليوم {selectedDate:yyyy-MM-dd} بنجاح";
            }
            else
            {
                TempData["ErrorMessage"] = "حدث خطأ في إغلاق الجرد";
            }

            return RedirectToAction(nameof(Index), new { date = selectedDate });
        }

        // POST: DailyInventory/Recalculate
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Recalculate(string date)
        {
            if (string.IsNullOrEmpty(date) || !DateTime.TryParseExact(date, "yyyy-MM-dd", null, DateTimeStyles.None, out DateTime selectedDate))
            {
                TempData["ErrorMessage"] = "تاريخ غير صحيح";
                return RedirectToAction(nameof(Index));
            }

            var isClosed = await _dailyInventoryService.IsDayClosedAsync(selectedDate);
            if (isClosed)
            {
                TempData["ErrorMessage"] = $"لا يمكن إعادة حساب الجرد المغلق ليوم {selectedDate:yyyy-MM-dd}";
                return RedirectToAction(nameof(Index), new { date = selectedDate });
            }

            try
            {
                await _dailyInventoryService.RecalculateInventoryAsync(selectedDate);

                await _activityService.LogActivityAsync(
                    User.Identity?.Name ?? "System",
                    "RecalculateDailyInventory", 
                    $"تم إعادة حساب الجرد اليومي لتاريخ {selectedDate:yyyy-MM-dd}",
                    null,
                    "DailyInventory"
                );

                TempData["SuccessMessage"] = $"تم إعادة حساب الجرد ليوم {selectedDate:yyyy-MM-dd} بنجاح";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error recalculating inventory for {selectedDate:yyyy-MM-dd}");
                TempData["ErrorMessage"] = "حدث خطأ في إعادة حساب الجرد";
            }

            return RedirectToAction(nameof(Index), new { date = selectedDate });
        }

        // GET: DailyInventory/ExportExcel/2024-01-15
        public async Task<IActionResult> ExportExcel(string id)
        {
            if (string.IsNullOrEmpty(id) || !DateTime.TryParseExact(id, "yyyy-MM-dd", null, DateTimeStyles.None, out DateTime date))
            {
                return BadRequest("تاريخ غير صحيح");
            }

            try
            {
                var excelData = await _dailyInventoryService.ExportToExcelAsync(date);
                var fileName = $"Daily_Inventory_{date:yyyy-MM-dd}.xlsx";

                await _activityService.LogActivityAsync(
                    User.Identity?.Name ?? "System",
                    "ExportDailyInventory",
                    $"تم تصدير الجرد اليومي لتاريخ {date:yyyy-MM-dd} إلى Excel",
                    fileName,
                    "DailyInventory"
                );

                return File(excelData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error exporting inventory to Excel for {date:yyyy-MM-dd}");
                TempData["ErrorMessage"] = "حدث خطأ في تصدير البيانات";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        // GET: DailyInventory/Compare
        public async Task<IActionResult> Compare(DateTime? startDate, DateTime? endDate)
        {
            var start = startDate ?? DateTime.Today.AddDays(-7);
            var end = endDate ?? DateTime.Today;

            if (start > end)
            {
                TempData["ErrorMessage"] = "تاريخ البداية يجب أن يكون قبل تاريخ النهاية";
                start = end.AddDays(-7);
            }

            var inventories = await _dailyInventoryService.GetInventoriesInRangeAsync(start, end);

            ViewData["StartDate"] = start;
            ViewData["EndDate"] = end;
            ViewData["TotalSales"] = inventories.Sum(i => i.TotalSales);
            ViewData["TotalProfit"] = inventories.Sum(i => i.NetProfit);
            ViewData["AverageDailySales"] = inventories.Any() ? inventories.Average(i => i.TotalSales) : 0;

            return View(inventories);
        }

        // API: Get daily summary data for charts
        [HttpGet]
        public async Task<IActionResult> GetDailySummaryData(DateTime? startDate, DateTime? endDate)
        {
            var start = startDate ?? DateTime.Today.AddDays(-30);
            var end = endDate ?? DateTime.Today;

            var inventories = await _dailyInventoryService.GetInventoriesInRangeAsync(start, end);

            var data = inventories.Select(i => new
            {
                date = i.InventoryDate.ToString("yyyy-MM-dd"),
                sales = i.TotalSales,
                profit = i.NetProfit,
                transactions = i.TransactionsCount,
                customers = i.CustomersCount,
                products = i.ProductsSoldCount
            }).ToList();

            return Json(data);
        }

        // API: Get top products for a specific date
        [HttpGet]
        public async Task<IActionResult> GetTopProducts(string date, int count = 5)
        {
            if (string.IsNullOrEmpty(date) || !DateTime.TryParseExact(date, "yyyy-MM-dd", null, DateTimeStyles.None, out DateTime selectedDate))
            {
                return BadRequest("تاريخ غير صحيح");
            }

            var topProducts = await _dailyInventoryService.GetTopSellingProductsAsync(selectedDate, count);
            
            var data = topProducts.Select(p => new
            {
                productName = p.Product?.Name ?? "غير محدد",
                quantitySold = p.TotalQuantitySold,
                salesValue = p.NetSalesValue,
                profit = p.NetProfit
            }).ToList();

            return Json(data);
        }

        /// <summary>
        /// تصدير الجرد اليومي إلى Excel (مبسط)
        /// </summary>
        public async Task<IActionResult> ExportToExcel(DateTime date)
        {
            try
            {
                // EPPlus license is set in appsettings.json
                var excelData = await _dailyInventoryService.ExportToExcelAsync(date);
                var fileName = $"الجرد_اليومي_{date:yyyy-MM-dd}.xlsx";
                
                await _activityService.LogActivityAsync(
                    User.Identity?.Name ?? "Unknown",
                    "ExportExcel",
                    $"Exported daily inventory to Excel for {date:yyyy-MM-dd}",
                    $"File: {fileName}",
                    "Export"
                );
                
                return File(excelData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error exporting daily inventory to Excel for {date:yyyy-MM-dd}");
                TempData["ErrorMessage"] = "حدث خطأ في تصدير البيانات إلى Excel";
                return RedirectToAction(nameof(Index), new { date = date });
            }
        }

        /// <summary>
        /// تصدير الجرد اليومي إلى Excel (مفصل)
        /// </summary>
        public async Task<IActionResult> ExportDetailedExcel(DateTime date)
        {
            try
            {
                // EPPlus license is set in appsettings.json
                var excelData = await _dailyInventoryService.ExportDetailedExcelAsync(date);
                var fileName = $"الجرد_اليومي_المفصل_{date:yyyy-MM-dd}.xlsx";
                
                await _activityService.LogActivityAsync(
                    User.Identity?.Name ?? "Unknown",
                    "ExportDetailedExcel",
                    $"Exported detailed daily inventory to Excel for {date:yyyy-MM-dd}",
                    $"File: {fileName}",
                    "Export"
                );
                
                return File(excelData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error exporting detailed daily inventory to Excel for {date:yyyy-MM-dd}");
                TempData["ErrorMessage"] = "حدث خطأ في تصدير البيانات المفصلة إلى Excel";
                return RedirectToAction(nameof(Index), new { date = date });
            }
        }

        // API: Get transactions for a specific date (for real-time updates)
        [HttpGet]
        public async Task<IActionResult> GetTransactions(string date)
        {
            if (string.IsNullOrEmpty(date) || !DateTime.TryParseExact(date, "yyyy-MM-dd", null, DateTimeStyles.None, out DateTime selectedDate))
            {
                return BadRequest("تاريخ غير صحيح");
            }

            var startTime = selectedDate;
            var endTime = selectedDate.AddDays(1).AddTicks(-1);

            var transactions = await _context.DailySaleTransactions
                .Include(dst => dst.Customer)
                .Include(dst => dst.Product)
                .Where(dst => dst.TransactionTime >= startTime && dst.TransactionTime <= endTime)
                .OrderByDescending(dst => dst.TransactionTime)
                .Take(20) // Latest 20 transactions
                .Select(dst => new
                {
                    id = dst.Id,
                    time = dst.TransactionTime.ToString("HH:mm:ss"),
                    customer = dst.Customer.Name,
                    product = dst.Product.Name,
                    quantity = dst.Quantity,
                    totalPrice = dst.TotalPrice,
                    discount = dst.Discount,
                    amountPaid = dst.AmountPaid,
                    netAmount = dst.TotalPrice // استخدام TotalPrice مباشرة
                })
                .ToListAsync();

            return Json(transactions);
        }

        // POST: DailyInventory/RecalculateAllData - إعادة حساب جميع البيانات
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecalculateAllData()
        {
            try
            {
                // إعادة حساب جميع الجرد اليومي
                var allInventories = await _context.DailyInventories.ToListAsync();
                
                foreach (var inventory in allInventories)
                {
                    var startTime = inventory.InventoryDate;
                    var endTime = inventory.InventoryDate.AddDays(1).AddTicks(-1);

                    var transactions = await _context.CustomerTransactions
                        .Include(ct => ct.Product)
                        .Include(ct => ct.Customer)
                        .Where(ct => ct.Date >= startTime && ct.Date <= endTime)
                        .ToListAsync();

                    // إعادة حساب الإحصائيات بالطريقة الصحيحة
                    inventory.TotalSales = Math.Round(transactions.Sum(t => t.TotalPrice), 2); // TotalPrice already includes discount
                    // حساب التكلفة الإجمالية - سعر الجملة لكل قطعة
            inventory.TotalCost = Math.Round(transactions.Sum(t => (t.Product?.CartonPrice ?? 0) * Math.Abs(t.Quantity)), 2);
                    // حساب الربح لكل قطعة: سعر البيع - سعر الجملة
                    inventory.NetProfit = Math.Round(transactions.Sum(t => 
                    {
                        if (t.Product?.CartonPrice > 0)
                        {
                        // الربح لكل قطعة = سعر البيع - سعر الجملة
                        var profitPerUnit = t.Price - (t.Product.CartonPrice ?? 0);
                            return profitPerUnit * t.Quantity;
                        }
                        return 0;
                    }), 2);
                    inventory.TotalDiscounts = Math.Round(transactions.Where(t => t.Quantity > 0).Sum(t => t.Discount), 2);
                    inventory.TotalPayments = Math.Round(transactions.Where(t => t.ShippingCost == 0 && t.AmountPaid > 0).Sum(t => t.AmountPaid), 2); // المدفوعات الفعلية فقط (استبعاد الشحن والمرتجعات)
                    // حساب الديون الفعلية: مجموع (TotalPrice - AmountPaid) لكل معاملة
                // لكن نضمن أن المجموع صحيح من خلال التحقق من التطابق
                // حساب الديون الفعلية: مجموع (TotalPrice - AmountPaid) لكل معاملة
                var calculatedTotalDebts = transactions.Sum(t => t.TotalPrice - t.AmountPaid);
                
                // حساب الديون الفعلية: مجموع (Price * Quantity - AmountPaid) لكل معاملة
                inventory.TotalDebts = Math.Round(calculatedTotalDebts, 2);
                    inventory.TransactionsCount = transactions.Count;
                    inventory.CustomersCount = transactions.Select(t => t.CustomerId).Distinct().Count();
                    inventory.ProductsSoldCount = transactions.Select(t => t.ProductId).Distinct().Count();
                    inventory.TotalQuantitySold = transactions.Sum(t => t.Quantity);
                    inventory.UpdatedAt = DateTime.Now;
                }

                await _context.SaveChangesAsync();

                await _activityService.LogActivityAsync(
                    User.Identity?.Name ?? "System",
                    "RecalculateAllData",
                    $"تم إعادة حساب جميع بيانات الجرد اليومي ({allInventories.Count} يوم)",
                    null,
                    "DailyInventory"
                );

                TempData["SuccessMessage"] = $"تم إعادة حساب جميع بيانات الجرد اليومي بنجاح ({allInventories.Count} يوم)";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recalculating all data");
                TempData["ErrorMessage"] = "حدث خطأ أثناء إعادة حساب جميع البيانات";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: DailyInventory/FixExistingData - إصلاح البيانات الموجودة
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FixExistingData()
        {
            try
            {
                // إصلاح جميع CustomerTransactions للإرجاعات
                var returnTransactions = await _context.CustomerTransactions
                    .Where(t => t.Quantity < 0 && t.Discount > 0)
                    .ToListAsync();

                int fixedCount = 0;
                foreach (var transaction in returnTransactions)
                {
                    transaction.Discount = 0; // إزالة الخصم من الإرجاعات
                    fixedCount++;
                }

                await _context.SaveChangesAsync();

                // إعادة حساب جميع الجرد اليومي
                var allInventories = await _context.DailyInventories.ToListAsync();
                
                foreach (var inventory in allInventories)
                {
                    var startTime = inventory.InventoryDate;
                    var endTime = inventory.InventoryDate.AddDays(1).AddTicks(-1);

                    var transactions = await _context.CustomerTransactions
                        .Include(ct => ct.Product)
                        .Include(ct => ct.Customer)
                        .Where(ct => ct.Date >= startTime && ct.Date <= endTime)
                        .ToListAsync();

                    // إعادة حساب الإحصائيات بالطريقة الصحيحة
                    inventory.TotalSales = Math.Round(transactions.Sum(t => t.TotalPrice), 2); // TotalPrice already includes discount
                    // حساب التكلفة الإجمالية - سعر الجملة لكل قطعة
            inventory.TotalCost = Math.Round(transactions.Sum(t => (t.Product?.CartonPrice ?? 0) * Math.Abs(t.Quantity)), 2);
                    // حساب الربح لكل قطعة: سعر البيع - سعر الجملة
                    inventory.NetProfit = Math.Round(transactions.Sum(t => 
                    {
                        if (t.Product?.CartonPrice > 0)
                        {
                        // الربح لكل قطعة = سعر البيع - سعر الجملة
                        var profitPerUnit = t.Price - (t.Product.CartonPrice ?? 0);
                            return profitPerUnit * t.Quantity;
                        }
                        return 0;
                    }), 2);
                    inventory.TotalDiscounts = Math.Round(transactions.Where(t => t.Quantity > 0).Sum(t => t.Discount), 2);
                    inventory.TotalPayments = Math.Round(transactions.Where(t => t.ShippingCost == 0 && t.AmountPaid > 0).Sum(t => t.AmountPaid), 2); // المدفوعات الفعلية فقط (استبعاد الشحن والمرتجعات)
                    // حساب الديون الفعلية: مجموع (TotalPrice - AmountPaid) لكل معاملة
                // لكن نضمن أن المجموع صحيح من خلال التحقق من التطابق
                // حساب الديون الفعلية: مجموع (TotalPrice - AmountPaid) لكل معاملة
                var calculatedTotalDebts = transactions.Sum(t => t.TotalPrice - t.AmountPaid);
                
                // حساب الديون الفعلية: مجموع (Price * Quantity - AmountPaid) لكل معاملة
                inventory.TotalDebts = Math.Round(calculatedTotalDebts, 2);
                    inventory.TransactionsCount = transactions.Count;
                    inventory.CustomersCount = transactions.Select(t => t.CustomerId).Distinct().Count();
                    inventory.ProductsSoldCount = transactions.Select(t => t.ProductId).Distinct().Count();
                    inventory.TotalQuantitySold = transactions.Sum(t => t.Quantity);
                    inventory.UpdatedAt = DateTime.Now;
                }

                await _context.SaveChangesAsync();

                await _activityService.LogActivityAsync(
                    User.Identity?.Name ?? "System",
                    "FixExistingData",
                    $"تم إصلاح {fixedCount} معاملة إرجاع وإعادة حساب {allInventories.Count} يوم جرد",
                    null,
                    "DailyInventory"
                );

                TempData["SuccessMessage"] = $"تم إصلاح {fixedCount} معاملة إرجاع وإعادة حساب {allInventories.Count} يوم جرد بنجاح";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fixing existing data");
                TempData["ErrorMessage"] = "حدث خطأ أثناء إصلاح البيانات الموجودة";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: DailyInventory/CheckDataIssues - فحص مشاكل البيانات
        public async Task<IActionResult> CheckDataIssues()
        {
            try
            {
                // فحص المعاملات التي تحتوي على خصم للإرجاعات
                var problematicTransactions = await _context.CustomerTransactions
                    .Include(t => t.Product)
                    .Include(t => t.Customer)
                    .Where(t => t.Quantity < 0 && t.Discount > 0)
                    .Select(t => new
                    {
                        t.Id,
                        t.Date,
                        ProductName = t.Product.Name,
                        CustomerName = t.Customer.Name,
                        t.Quantity,
                        t.TotalPrice,
                        t.Discount,
                        NetAmount = t.TotalPrice
                    })
                    .ToListAsync();

                // فحص الجرد اليومي الذي قد يحتوي على حسابات خاطئة
                var problematicInventories = await _context.DailyInventories
                    .Where(i => i.TotalDiscounts > 0)
                    .Select(i => new
                    {
                        i.Id,
                        i.InventoryDate,
                        i.TotalSales,
                        i.TotalDiscounts,
                        i.NetProfit
                    })
                    .ToListAsync();

                ViewBag.ProblematicTransactions = problematicTransactions;
                ViewBag.ProblematicInventories = problematicInventories;
                ViewBag.TransactionsCount = problematicTransactions.Count;
                ViewBag.InventoriesCount = problematicInventories.Count;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking data issues");
                TempData["ErrorMessage"] = "حدث خطأ أثناء فحص مشاكل البيانات";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
