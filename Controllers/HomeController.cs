using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using PesticideShop.Models;
using PesticideShop.Data;

namespace PesticideShop.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ApplicationDbContext _context;

    public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        // جلب الإحصائيات من قاعدة البيانات
        var stats = new HomeStatsViewModel
        {
            TotalCustomers = await _context.Customers.CountAsync(),
            TotalProducts = await _context.Products.CountAsync(),
            TotalInvoices = await _context.Invoices.CountAsync(),
            TotalSales = await _context.CustomerTransactions
                .Where(t => t.Quantity > 0) // فقط المبيعات (ليس المرتجعات)
                .SumAsync(t => t.TotalPrice - t.Discount),
            TotalOrders = await _context.Invoices.CountAsync(), // استخدام Invoices بدلاً من Orders
            // حساب نسبة الرضا بناءً على عدد العملاء النشطين
            CustomerSatisfaction = await CalculateCustomerSatisfaction()
        };

        return View(stats);
    }

    private async Task<int> CalculateCustomerSatisfaction()
    {
        // حساب نسبة الرضا بناءً على العملاء الذين لديهم معاملات
        var totalCustomers = await _context.Customers.CountAsync();
        if (totalCustomers == 0) return 0;

        var activeCustomers = await _context.Customers
            .Where(c => c.Transactions.Any())
            .CountAsync();

        return (int)Math.Round((double)activeCustomers / totalCustomers * 100);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
