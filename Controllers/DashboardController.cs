using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using PesticideShop.Data;
using PesticideShop.Models;
using PesticideShop.Services;
using System.Linq;
using System.Threading.Tasks;

namespace PesticideShop.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IActivityService _activityService;

        public DashboardController(ApplicationDbContext context, IActivityService activityService)
        {
            _context = context;
            _activityService = activityService;
        }

        public async Task<IActionResult> Index()
        {
            // Calculate total sales from customer transactions (by piece)
            // للإرجاع: نأخذ القيمة كما هي، للبيع: نخصم الخصم
            var totalSales = await _context.CustomerTransactions
                .Include(t => t.Product)
                .SumAsync(t => t.TotalPrice);

            // Calculate total cost based on actual cost price
            var customerTransactions = await _context.CustomerTransactions
                .Include(t => t.Product)
                .ToListAsync();

            decimal totalCost = 0;
            bool hasValidCost = false;
            foreach (var transaction in customerTransactions)
            {
                // Calculate cost per piece from carton price
                if (transaction.Product.CartonPrice > 0)
                {
                    hasValidCost = true;
                    totalCost += (transaction.Product.CartonPrice ?? 0) * Math.Abs(transaction.Quantity);
                }
                // لا نحسب تكلفة افتراضية - فقط إذا كان هناك سعر جملة حقيقي
            }

            // Calculate total profit - فرق سعر الجملة وسعر القطاعي لكل قطعة
            decimal totalProfit;
            if (hasValidCost)
            {
                // حساب الربح لكل قطعة: سعر البيع - سعر الجملة (مع مراعاة الإرجاع)
                var grossProfit = customerTransactions.Sum(t => 
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
                var totalDiscounts = customerTransactions.Where(t => t.Quantity > 0).Sum(t => t.Discount);
                totalProfit = grossProfit; // الربح بدون خصم الخصومات
            }
            else
            {
                // إذا لم يكن هناك سعر جملة (CartonPrice = 0 أو null)، لا نحسب ربح
                totalProfit = 0;
            }

            // Calculate outstanding payments from customers (only for sales, not returns)
            var customerOutstanding = await _context.CustomerTransactions
                .Where(t => t.Quantity > 0) // معاملات البيع فقط
                .SumAsync(t => t.TotalPrice - t.AmountPaid);

            // Calculate total inventory value (at selling price)
            var totalInventoryValue = await _context.Products
                .SumAsync(p => p.Price * p.Quantity);

            // Calculate profit margin percentage
            var profitMargin = totalSales > 0 ? (totalProfit / totalSales) * 100 : 0;

            // Calculate counts
            var totalProducts = await _context.Products.CountAsync();
            var totalCustomers = await _context.Customers.CountAsync();
            var totalTransactions = await _context.CustomerTransactions.CountAsync();

            // Calculate average selling price
            var averageSellingPrice = totalTransactions > 0 ? totalSales / totalTransactions : 0;

            // Add debug information
            ViewData["DebugInfo"] = $"Sales: {totalSales:C}, Cost: {totalCost:C}, Profit: {totalProfit:C}, Margin: {profitMargin:F2}%";

            ViewData["AverageSellingPrice"] = averageSellingPrice;
            ViewData["TotalSales"] = totalSales;
            ViewData["TotalProfit"] = totalProfit;
            ViewData["TotalCost"] = totalCost;
            ViewData["ProfitMargin"] = profitMargin;
            ViewData["TotalInventoryValue"] = totalInventoryValue;
            ViewData["TotalProducts"] = totalProducts;
            ViewData["TotalCustomers"] = totalCustomers;
            ViewData["TotalTransactions"] = totalTransactions;
            ViewData["CustomerOutstanding"] = customerOutstanding;
            ViewData["TotalDiscounts"] = hasValidCost ? customerTransactions.Where(t => t.Quantity > 0).Sum(t => t.Discount) : 0;

            // Get recent activities
            var recentActivities = await _activityService.GetRecentActivitiesAsync(5);
            ViewData["RecentActivities"] = recentActivities;

            return View();
        }
    }
} 