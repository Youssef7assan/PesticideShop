using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using PesticideShop.Data;
using PesticideShop.Models;
using System.Linq;
using System.Threading.Tasks;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;

namespace PesticideShop.Controllers
{
    [Authorize]
    public class AnnualInventoryController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AnnualInventoryController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int page = 1, int pageSize = 10)
        {
            // Get current inventory items with categories
            var allInventoryItems = await _context.Products
                .Include(p => p.Category)
                .ToListAsync();

            // Calculate pagination
            var totalItems = allInventoryItems.Count;
            var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            var inventoryItems = allInventoryItems
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // Calculate total inventory value
            var totalInventoryValue = await _context.Products
                .SumAsync(p => p.Price * p.Quantity);

            // Calculate total sales for the year (including returns and exchanges)
            var currentYear = DateTime.Now.Year;
            var yearTransactions = await _context.CustomerTransactions
                .Where(t => t.Date.Year == currentYear)
                .Include(t => t.Product)
                .ToListAsync();

            // حساب إجمالي المبيعات مع مراعاة الاستبدال والاسترجاع
            var salesTransactions = yearTransactions.Where(t => t.Quantity > 0).ToList();
            var returnTransactions = yearTransactions.Where(t => t.Quantity < 0).ToList();
            
            // إجمالي المبيعات (الكميات الموجبة فقط) - بعد الخصم
            var grossSales = salesTransactions.Sum(t => t.TotalPrice);
            
            // إجمالي المرتجعات (الكميات السالبة)
            var returnValue = returnTransactions.Sum(t => Math.Abs(t.TotalPrice));
            
            // صافي المبيعات (المبيعات - المرتجعات)
            var netSales = grossSales - returnValue;
            
            // إجمالي الخصومات (فقط للبيع)
            var totalDiscounts = yearTransactions.Where(t => t.Quantity > 0).Sum(t => t.Discount);
            
            // صافي المبيعات بعد الخصومات (المبيعات محسوبة بالفعل بعد الخصم)
            var netSalesAfterDiscounts = netSales;

            // حساب التكلفة
            // حساب التكلفة الإجمالية - سعر الجملة لكل قطعة
            decimal yearCost = 0;
            foreach (var transaction in yearTransactions)
            {
                if (transaction.Product != null)
                {
                    // استخدام سعر الجملة إذا كان متوفراً
                    var costPrice = transaction.Product.CartonPrice ?? 0;
                    yearCost += costPrice * Math.Abs(transaction.Quantity); // استخدام القيمة المطلقة للكمية
                }
            }

            // صافي الربح - فرق سعر الجملة وسعر القطاعي لكل قطعة
            var hasValidCost = yearTransactions.Any(t => t.Product?.CartonPrice > 0);
            decimal netProfit;
            
            if (hasValidCost)
            {
                // حساب الربح لكل قطعة: سعر البيع - سعر الجملة (مع مراعاة الإرجاع)
                var grossProfit = yearTransactions.Sum(t => 
                {
                    if (t.Product?.CartonPrice > 0)
                    {
                        // الربح لكل قطعة = سعر البيع - سعر الجملة
                        var profitPerUnit = t.Price - (t.Product.CartonPrice ?? 0);
                        // للبيع (كمية موجبة): الربح موجب
                        // للإرجاع (كمية سالبة): الربح سالب (يتم خصمه من إجمالي الأرباح)
                        var profit = profitPerUnit * t.Quantity;
                        return profit;
                    }
                    return 0;
                });
                
                // الخصم يظهر منفصل، لا نخصمه من الربح
                var profitDiscounts = yearTransactions.Where(t => t.Quantity > 0).Sum(t => t.Discount);
                netProfit = grossProfit; // الربح بدون خصم الخصومات
            }
            else
            {
                // إذا لم يكن هناك سعر جملة (CartonPrice = 0 أو null)، لا نحسب ربح
                netProfit = 0;
            }

            // حساب إحصائيات الاستبدال (المرتجعات مع استبدال)
            var exchangeTransactions = returnTransactions.Where(t => !string.IsNullOrEmpty(t.Notes) && t.Notes.Contains("استبدال")).ToList();
            var exchangeValue = exchangeTransactions.Sum(t => Math.Abs(t.TotalPrice));
            
            // المرتجعات العادية (بدون استبدال)
            var regularReturns = returnTransactions.Where(t => string.IsNullOrEmpty(t.Notes) || !t.Notes.Contains("استبدال")).ToList();
            var regularReturnValue = regularReturns.Sum(t => Math.Abs(t.TotalPrice));

            // Get total customers count
            var totalCustomers = await _context.Customers.CountAsync();

            // Calculate profit margin for the year
            var profitMargin = netSalesAfterDiscounts > 0 ? (netProfit / netSalesAfterDiscounts) * 100 : 0;

            // Add detailed debug information
            ViewData["DebugInfo"] = $"Gross Sales: {grossSales:C}, Returns: {returnValue:C}, Regular Returns: {regularReturnValue:C}, Exchanges: {exchangeValue:C}, Net Sales: {netSales:C}, Net Sales After Discounts: {netSalesAfterDiscounts:C}, Year Cost: {yearCost:C}, Net Profit: {netProfit:C}, Margin: {profitMargin:F2}%";

            ViewData["InventoryItems"] = inventoryItems;
            ViewData["TotalInventoryValue"] = totalInventoryValue;
            ViewData["CurrentPage"] = page;
            ViewData["TotalPages"] = totalPages;
            ViewData["TotalItems"] = totalItems;
            ViewData["PageSize"] = pageSize;
            ViewData["TotalSales"] = netSalesAfterDiscounts; // استخدام صافي المبيعات بعد الخصومات
            ViewData["GrossSales"] = grossSales; // إجمالي المبيعات قبل الاسترجاع
            ViewData["ReturnValue"] = returnValue;
            ViewData["ExchangeValue"] = exchangeValue;
            ViewData["TotalDiscounts"] = totalDiscounts; // إجمالي الخصومات
            ViewData["NetProfit"] = netProfit;
            ViewData["ProfitMargin"] = profitMargin;
            ViewData["TotalCustomers"] = totalCustomers;
            ViewData["ReturnTransactionsCount"] = returnTransactions.Count;
            ViewData["ExchangeTransactionsCount"] = exchangeTransactions.Count;
            ViewData["SalesTransactionsCount"] = salesTransactions.Count;

            return View();
        }

        /// <summary>
        /// تصدير الجرد السنوي إلى Excel (مبسط)
        /// </summary>
        public async Task<IActionResult> ExportToExcel()
        {
            try
            {
                var excelData = await GenerateAnnualExcelAsync(false);
                var fileName = $"الجرد_السنوي_{DateTime.Now.Year}.xlsx";
                
                return File(excelData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"حدث خطأ في تصدير البيانات إلى Excel: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// تصدير الجرد السنوي إلى Excel (مفصل)
        /// </summary>
        public async Task<IActionResult> ExportDetailedExcel()
        {
            try
            {
                var excelData = await GenerateAnnualExcelAsync(true);
                var fileName = $"الجرد_السنوي_المفصل_{DateTime.Now.Year}.xlsx";
                
                return File(excelData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"حدث خطأ في تصدير البيانات المفصلة إلى Excel: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// توليد ملف Excel للجرد السنوي
        /// </summary>
        private async Task<byte[]> GenerateAnnualExcelAsync(bool isDetailed)
        {
            // EPPlus 8+ license is set in appsettings.json
            using var package = new ExcelPackage();

            // Get data
            var currentYear = DateTime.Now.Year;
            var inventoryItems = await _context.Products.ToListAsync();
            var yearTransactions = await _context.CustomerTransactions
                .Where(t => t.Date.Year == currentYear)
                .Include(t => t.Product)
                .Include(t => t.Customer)
                .ToListAsync();

            var customerBalances = await _context.Customers
                .Include(c => c.Transactions)
                .ToListAsync();

            var customerBalancesFiltered = customerBalances
                .Where(c => c.Transactions != null && c.Transactions.Count > 0)
                .Select(c => new
                {
                    CustomerName = c.Name,
                    PhoneNumber = c.PhoneNumber,
                    TotalPurchases = c.Transactions.Where(t => t.Quantity > 0).Sum(t => t.TotalPrice),
                    TotalReturns = c.Transactions.Where(t => t.Quantity < 0).Sum(t => Math.Abs(t.TotalPrice)),
                    TotalPaid = c.Transactions.Where(t => t.Quantity > 0).Sum(t => t.AmountPaid), // المدفوعات فقط للبيع
                    RemainingBalance = (decimal)(c.Transactions.Where(t => t.Quantity > 0).Sum(t => t.TotalPrice) - 
                                     c.Transactions.Where(t => t.Quantity < 0).Sum(t => Math.Abs(t.TotalPrice)) - 
                                     c.Transactions.Where(t => t.Quantity > 0).Sum(t => t.AmountPaid)), // المدفوعات فقط للبيع
                    ReturnsCount = c.Transactions.Count(t => t.Quantity < 0),
                    ExchangesCount = c.Transactions.Count(t => t.Quantity < 0 && !string.IsNullOrEmpty(t.Notes) && t.Notes.Contains("استبدال")),
                    TransactionsCount = c.Transactions.Count
                })
                .ToList();

            // Summary Sheet
            var summarySheet = package.Workbook.Worksheets.Add("ملخص السنة");
            CreateAnnualSummarySheet(summarySheet, inventoryItems, yearTransactions, customerBalancesFiltered, currentYear);

            // Inventory Sheet
            var inventorySheet = package.Workbook.Worksheets.Add("المخزون");
            CreateAnnualInventorySheet(inventorySheet, inventoryItems);

            // Customers Sheet
            var customersSheet = package.Workbook.Worksheets.Add("العملاء");
            CreateAnnualCustomersSheet(customersSheet, customerBalancesFiltered);

            if (isDetailed)
            {
                // Detailed Transactions Sheet
                var transactionsSheet = package.Workbook.Worksheets.Add("المعاملات المفصلة");
                CreateAnnualTransactionsSheet(transactionsSheet, yearTransactions);

                // Monthly Summary Sheet
                var monthlySheet = package.Workbook.Worksheets.Add("الملخص الشهري");
                CreateMonthlySummarySheet(monthlySheet, yearTransactions, currentYear);

                // Returns and Exchanges Sheet
                var returnsSheet = package.Workbook.Worksheets.Add("المرتجعات والاستبدالات");
                CreateReturnsAndExchangesSheet(returnsSheet, yearTransactions);
            }

            return package.GetAsByteArray();
        }

        private void CreateAnnualSummarySheet(ExcelWorksheet sheet, List<Product> inventoryItems, List<CustomerTransaction> yearTransactions, dynamic customerBalances, int year)
        {
            sheet.Cells["A1"].Value = $"تقرير الجرد السنوي {year}";
            sheet.Cells["A1:E1"].Merge = true;
            sheet.Cells["A1"].Style.Font.Size = 18;
            sheet.Cells["A1"].Style.Font.Bold = true;
            sheet.Cells["A1"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            // Calculate totals
            var totalInventoryValue = inventoryItems.Sum(p => p.Price * p.Quantity);
            var totalSales = yearTransactions.Sum(t => t.Quantity > 0 ? (t.TotalPrice - t.Discount) : t.TotalPrice); // للإرجاع: لا نخصم الخصم
            var totalDiscounts = yearTransactions.Where(t => t.Quantity > 0).Sum(t => t.Discount); // الخصم فقط للبيع
            var netSales = totalSales; // المبيعات الصافية محسوبة بالفعل أعلاه
            
            // حساب التكلفة والربح بالمنطق الجديد - استخدام سعر الجملة
            decimal yearCost = 0;
            bool hasValidCost = false;
            foreach (var transaction in yearTransactions)
            {
                if (transaction.Product != null && transaction.Product.CartonPrice > 0)
                {
                    yearCost += (transaction.Product.CartonPrice ?? 0) * Math.Abs(transaction.Quantity);
                    hasValidCost = true;
                }
            }
            
            // حساب إجمالي المديونيات على العملاء
            var totalCustomerDebts = yearTransactions
                .Where(t => t.Quantity > 0)
                .Sum(t => (t.TotalPrice - t.Discount) - t.AmountPaid);
            
            // صافي الربح - فرق سعر الجملة وسعر القطاعي لكل قطعة
            decimal netProfit;
            if (hasValidCost)
            {
                // حساب الربح لكل قطعة: سعر البيع - سعر الجملة (مع مراعاة الإرجاع)
                var grossProfit = yearTransactions.Sum(t => 
                {
                    if (t.Product?.CartonPrice > 0)
                    {
                        // الربح لكل قطعة = سعر البيع - سعر الجملة
                        var profitPerUnit = t.Price - (t.Product.CartonPrice ?? 0);
                        // للبيع (كمية موجبة): الربح موجب
                        // للإرجاع (كمية سالبة): الربح سالب (يتم خصمه من إجمالي الأرباح)
                        var profit = profitPerUnit * t.Quantity;
                        return profit;
                    }
                    return 0;
                });
                
                // الخصم يظهر منفصل، لا نخصمه من الربح
                var yearTotalDiscounts = yearTransactions.Where(t => t.Quantity > 0).Sum(t => t.Discount);
                netProfit = grossProfit; // الربح بدون خصم الخصومات
            }
            else
            {
                // إذا لم يكن هناك سعر جملة (CartonPrice = 0 أو null)، لا نحسب ربح
                netProfit = 0;
            }
            
            var profitMargin = netSales > 0 ? (netProfit / netSales * 100) : 0;

            var returnTransactions = yearTransactions.Where(t => t.Quantity < 0).ToList();
            
            // حساب الاستبدالات من خلال الملاحظات (مثل الجرد السنوي)
            var exchangeTransactions = returnTransactions.Where(t => !string.IsNullOrEmpty(t.Notes) && t.Notes.Contains("استبدال")).ToList();
            var exchangeValue = exchangeTransactions.Sum(t => Math.Abs(t.TotalPrice));
            
            // المرتجعات العادية (بدون استبدال)
            var regularReturns = returnTransactions.Where(t => string.IsNullOrEmpty(t.Notes) || !t.Notes.Contains("استبدال")).ToList();
            var regularReturnValue = regularReturns.Sum(t => Math.Abs(t.TotalPrice));
            
            var salesTransactions = yearTransactions.Where(t => t.Quantity > 0).ToList();

            var returnValue = returnTransactions.Sum(t => Math.Abs(t.TotalPrice));
            var salesValue = salesTransactions.Sum(t => t.TotalPrice - t.Discount); // المبيعات بعد الخصم

            // Summary data
            var summaryData = new (string Label, object Value, string Format)[]
            {
                ("إجمالي المبيعات", salesValue, "currency"),
                ("قيمة المرتجعات العادية", regularReturnValue, "currency"),
                ("قيمة الاستبدالات", exchangeValue, "currency"),
                ("إجمالي المرتجعات", returnValue, "currency"),
                ("صافي المبيعات", netSales, "currency"),
                ("إجمالي التكلفة", yearCost, "currency"),
                ("صافي الربح", netProfit, "currency"),
                ("معدل الربحية", profitMargin, "percentage"),
                ("قيمة المخزون", totalInventoryValue, "currency"),
                ("عدد المنتجات", inventoryItems.Count, "number"),
                ("عدد العملاء", customerBalances.Count, "number"),
                ("عدد المعاملات", yearTransactions.Count, "number"),
                ("معاملات البيع", salesTransactions.Count, "number"),
                ("معاملات الإرجاع", returnTransactions.Count, "number"),
                ("معاملات الاستبدال", exchangeTransactions.Count, "number")
            };

            int row = 3;
            foreach (var item in summaryData)
            {
                sheet.Cells[row, 1].Value = item.Label;
                sheet.Cells[row, 2].Value = item.Value;

                if (item.Format == "currency")
                {
                    sheet.Cells[row, 2].Style.Numberformat.Format = "#,##0.00";
                }
                else if (item.Format == "percentage")
                {
                    sheet.Cells[row, 2].Style.Numberformat.Format = "0.00%";
                }

                row++;
            }

            sheet.Cells.AutoFitColumns();
        }

        private void CreateAnnualInventorySheet(ExcelWorksheet sheet, List<Product> inventoryItems)
        {
            var headers = new[] { "المنتج", "الفئة", "الكمية المتوفرة", "سعر البيع", "سعر التكلفة", "قيمة المخزون", "آخر تحديث" };
            for (int i = 0; i < headers.Length; i++)
            {
                sheet.Cells[1, i + 1].Value = headers[i];
                sheet.Cells[1, i + 1].Style.Font.Bold = true;
                sheet.Cells[1, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                sheet.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(Color.LightGreen);
            }

            for (int i = 0; i < inventoryItems.Count; i++)
            {
                var product = inventoryItems[i];
                var row = i + 2;
                var inventoryValue = product.Price * product.Quantity;

                sheet.Cells[row, 1].Value = product.Name;
                sheet.Cells[row, 2].Value = product.Category?.Name ?? "";
                sheet.Cells[row, 3].Value = product.Quantity;
                sheet.Cells[row, 4].Value = product.Price;
                sheet.Cells[row, 5].Value = product.CostPrice;
                sheet.Cells[row, 6].Value = inventoryValue;
                sheet.Cells[row, 7].Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

                // Format currency columns
                for (int col = 4; col <= 6; col++)
                {
                    sheet.Cells[row, col].Style.Numberformat.Format = "#,##0.00";
                }
            }

            sheet.Cells.AutoFitColumns();
        }

        private void CreateAnnualCustomersSheet(ExcelWorksheet sheet, dynamic customerBalances)
        {
            var headers = new[] { "العميل", "رقم الهاتف", "عدد المعاملات", "إجمالي المشتريات", "إجمالي المدفوعات", "المديونية", "المرتجعات", "الاستبدالات", "نسبة الدفع" };
            for (int i = 0; i < headers.Length; i++)
            {
                sheet.Cells[1, i + 1].Value = headers[i];
                sheet.Cells[1, i + 1].Style.Font.Bold = true;
                sheet.Cells[1, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                sheet.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(Color.LightYellow);
            }

            for (int i = 0; i < customerBalances.Count; i++)
            {
                var customer = customerBalances[i];
                var row = i + 2;
                var paymentRatio = customer.TotalPurchases > 0 ? (customer.TotalPaid / customer.TotalPurchases * 100) : 0;

                sheet.Cells[row, 1].Value = customer.CustomerName;
                sheet.Cells[row, 2].Value = customer.PhoneNumber ?? "";
                sheet.Cells[row, 3].Value = customer.TransactionsCount;
                sheet.Cells[row, 4].Value = customer.TotalPurchases;
                sheet.Cells[row, 5].Value = customer.TotalPaid;
                sheet.Cells[row, 6].Value = customer.RemainingBalance;
                sheet.Cells[row, 7].Value = customer.ReturnsCount;
                sheet.Cells[row, 8].Value = customer.ExchangesCount;
                sheet.Cells[row, 9].Value = paymentRatio;

                // Format currency columns
                for (int col = 4; col <= 6; col++)
                {
                    sheet.Cells[row, col].Style.Numberformat.Format = "#,##0.00";
                }

                // Format percentage
                sheet.Cells[row, 9].Style.Numberformat.Format = "0.00%";
            }

            sheet.Cells.AutoFitColumns();
        }

        private void CreateAnnualTransactionsSheet(ExcelWorksheet sheet, List<CustomerTransaction> yearTransactions)
        {
            var headers = new[] { "التاريخ", "الوقت", "العميل", "المنتج", "الكمية", "سعر الوحدة", "الإجمالي", "الخصم", "الصافي", "المدفوع", "المتبقي", "نوع العملية" };
            for (int i = 0; i < headers.Length; i++)
            {
                sheet.Cells[1, i + 1].Value = headers[i];
                sheet.Cells[1, i + 1].Style.Font.Bold = true;
                sheet.Cells[1, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                sheet.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(Color.LightBlue);
            }

            for (int i = 0; i < yearTransactions.Count; i++)
            {
                var transaction = yearTransactions[i];
                var row = i + 2;
                var netAmount = transaction.Quantity > 0 ? (transaction.TotalPrice - transaction.Discount) : transaction.TotalPrice; // للإرجاع: لا نخصم الخصم
                var remaining = netAmount - transaction.AmountPaid;
                var operationType = transaction.Quantity > 0 ? "بيع" : "إرجاع";

                sheet.Cells[row, 1].Value = transaction.Date.ToString("yyyy-MM-dd");
                sheet.Cells[row, 2].Value = transaction.Date.ToString("HH:mm:ss");
                sheet.Cells[row, 3].Value = transaction.Customer?.Name ?? "";
                sheet.Cells[row, 4].Value = transaction.Product?.Name ?? "";
                sheet.Cells[row, 5].Value = transaction.Quantity;
                sheet.Cells[row, 6].Value = transaction.Price;
                sheet.Cells[row, 7].Value = transaction.TotalPrice;
                sheet.Cells[row, 8].Value = transaction.Discount;
                sheet.Cells[row, 9].Value = netAmount;
                sheet.Cells[row, 10].Value = transaction.AmountPaid;
                sheet.Cells[row, 11].Value = remaining;
                sheet.Cells[row, 12].Value = operationType;

                // Format currency columns
                for (int col = 6; col <= 11; col++)
                {
                    sheet.Cells[row, col].Style.Numberformat.Format = "#,##0.00";
                }
            }

            sheet.Cells.AutoFitColumns();
        }

        private void CreateMonthlySummarySheet(ExcelWorksheet sheet, List<CustomerTransaction> yearTransactions, int year)
        {
            var headers = new[] { "الشهر", "المبيعات", "المرتجعات", "الاستبدالات", "صافي المبيعات", "التكلفة", "الربح", "معدل الربحية", "عدد المعاملات" };
            for (int i = 0; i < headers.Length; i++)
            {
                sheet.Cells[1, i + 1].Value = headers[i];
                sheet.Cells[1, i + 1].Style.Font.Bold = true;
                sheet.Cells[1, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                sheet.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(Color.LightCyan);
            }

            var months = Enumerable.Range(1, 12).ToList();
            var row = 2;

            foreach (var month in months)
            {
                var monthTransactions = yearTransactions.Where(t => t.Date.Month == month).ToList();
                var sales = monthTransactions.Where(t => t.Quantity > 0).Sum(t => t.TotalPrice);
                var returns = monthTransactions.Where(t => t.Quantity < 0).Sum(t => Math.Abs(t.TotalPrice));
                var exchanges = monthTransactions.Where(t => t.Quantity < 0 && t.TotalPrice < 0).Sum(t => Math.Abs(t.TotalPrice));
                var netSales = sales - returns;
                var cost = monthTransactions.Sum(t => (t.Product?.CartonPrice ?? 0) * Math.Abs(t.Quantity));
                // حساب الربح لكل قطعة: سعر البيع - سعر الجملة
                var profit = monthTransactions.Sum(t => 
                {
                    if (t.Product?.CartonPrice > 0)
                    {
                        var profitPerUnit = t.Price - (t.Product.CartonPrice ?? 0);
                        return profitPerUnit * t.Quantity;
                    }
                    return 0;
                });
                var profitMargin = netSales > 0 ? (profit / netSales * 100) : 0;

                var monthName = new DateTime(year, month, 1).ToString("MMMM", new System.Globalization.CultureInfo("ar-EG"));

                sheet.Cells[row, 1].Value = monthName;
                sheet.Cells[row, 2].Value = sales;
                sheet.Cells[row, 3].Value = returns;
                sheet.Cells[row, 4].Value = exchanges;
                sheet.Cells[row, 5].Value = netSales;
                sheet.Cells[row, 6].Value = cost;
                sheet.Cells[row, 7].Value = profit;
                sheet.Cells[row, 8].Value = profitMargin;
                sheet.Cells[row, 9].Value = monthTransactions.Count;

                // Format currency columns
                for (int col = 2; col <= 7; col++)
                {
                    sheet.Cells[row, col].Style.Numberformat.Format = "#,##0.00";
                }

                // Format percentage
                sheet.Cells[row, 8].Style.Numberformat.Format = "0.00%";

                row++;
            }

            sheet.Cells.AutoFitColumns();
        }

        private void CreateReturnsAndExchangesSheet(ExcelWorksheet sheet, List<CustomerTransaction> yearTransactions)
        {
            var headers = new[] { "التاريخ", "العميل", "المنتج", "الكمية", "القيمة", "نوع العملية", "ملاحظات" };
            for (int i = 0; i < headers.Length; i++)
            {
                sheet.Cells[1, i + 1].Value = headers[i];
                sheet.Cells[1, i + 1].Style.Font.Bold = true;
                sheet.Cells[1, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                sheet.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(Color.LightCoral);
            }

            var returnTransactions = yearTransactions.Where(t => t.Quantity < 0).ToList();
            var row = 2;

            for (int i = 0; i < returnTransactions.Count; i++)
            {
                var transaction = returnTransactions[i];
                var operationType = transaction.TotalPrice < 0 ? "استبدال" : "إرجاع";

                sheet.Cells[row, 1].Value = transaction.Date.ToString("yyyy-MM-dd");
                sheet.Cells[row, 2].Value = transaction.Customer?.Name ?? "";
                sheet.Cells[row, 3].Value = transaction.Product?.Name ?? "";
                sheet.Cells[row, 4].Value = Math.Abs(transaction.Quantity);
                sheet.Cells[row, 5].Value = Math.Abs(transaction.TotalPrice);
                sheet.Cells[row, 6].Value = operationType;
                sheet.Cells[row, 7].Value = transaction.Notes ?? "";

                // Format currency column
                sheet.Cells[row, 5].Style.Numberformat.Format = "#,##0.00";

                row++;
            }

            sheet.Cells.AutoFitColumns();
        }
    }
} 