using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PesticideShop.Data;
using PesticideShop.Models;

namespace PesticideShop.Controllers
{
    [Authorize]
    public class OrdersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OrdersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Orders
        public async Task<IActionResult> Index(
            string searchTerm = "",
            string statusFilter = "",
            string typeFilter = "",
            string originFilter = "",
            string paymentFilter = "",
            DateTime? dateFrom = null,
            DateTime? dateTo = null)
        {
            // Build query with filters
            var query = _context.Invoices
                .Include(i => i.Customer)
                .Include(i => i.Items)
                .ThenInclude(item => item.Product)
                .ThenInclude(p => p.Category)
                .AsQueryable();

            // Search filter
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                query = query.Where(i => 
                    i.InvoiceNumber.ToLower().Contains(term) ||
                    i.OrderNumber.ToLower().Contains(term) ||
                    (i.Customer != null && i.Customer.Name.ToLower().Contains(term)) ||
                    (i.Customer != null && i.Customer.PhoneNumber.Contains(term)));
            }

            // Status filter
            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                if (Enum.TryParse<InvoiceStatus>(statusFilter, true, out var status))
                {
                    query = query.Where(i => i.Status == status);
                }
            }

            // Type filter
            if (!string.IsNullOrWhiteSpace(typeFilter))
            {
                if (Enum.TryParse<InvoiceType>(typeFilter, true, out var type))
                {
                    query = query.Where(i => i.Type == type);
                }
            }

            // Origin filter
            if (!string.IsNullOrWhiteSpace(originFilter))
            {
                if (Enum.TryParse<OrderOrigin>(originFilter, true, out var origin))
                {
                    query = query.Where(i => i.OrderOrigin == origin);
                }
            }

            // Payment method filter
            if (!string.IsNullOrWhiteSpace(paymentFilter))
            {
                query = query.Where(i => i.PaymentMethod != null && i.PaymentMethod.ToLower() == paymentFilter.ToLower());
            }

            // Date range filter
            if (dateFrom.HasValue)
            {
                var startDate = dateFrom.Value.Date;
                query = query.Where(i => i.InvoiceDate >= startDate);
            }

            if (dateTo.HasValue)
            {
                var endDate = dateTo.Value.Date.AddDays(1).AddTicks(-1); // نهاية اليوم
                query = query.Where(i => i.InvoiceDate <= endDate);
            }

            // Get all invoices as "orders"
            var orders = await query
                .OrderByDescending(i => i.InvoiceDate)
                .ToListAsync();

            // Pass filter values to view
            ViewBag.SearchTerm = searchTerm;
            ViewBag.StatusFilter = statusFilter;
            ViewBag.TypeFilter = typeFilter;
            ViewBag.OriginFilter = originFilter;
            ViewBag.PaymentFilter = paymentFilter;
            ViewBag.DateFrom = dateFrom?.ToString("yyyy-MM-dd");
            ViewBag.DateTo = dateTo?.ToString("yyyy-MM-dd");

            return View(orders);
        }

        // GET: Orders/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // Get invoice details with all related data
            var order = await _context.Invoices
                .Include(i => i.Customer)
                .Include(i => i.Items)
                .ThenInclude(item => item.Product)
                .ThenInclude(p => p.Category)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        // GET: Orders by Customer
        public async Task<IActionResult> ByCustomer(int? customerId)
        {
            if (customerId == null)
            {
                return RedirectToAction(nameof(Index));
            }

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Id == customerId);

            if (customer == null)
            {
                return NotFound();
            }

            // Get all invoices for this customer
            var customerOrders = await _context.Invoices
                .Include(i => i.Items)
                .ThenInclude(item => item.Product)
                .ThenInclude(p => p.Category)
                .Where(i => i.CustomerId == customerId)
                .OrderByDescending(i => i.InvoiceDate)
                .ToListAsync();

            ViewData["Customer"] = customer;
            return View("ByCustomer", customerOrders);
        }

        // POST: Orders/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var order = await _context.Invoices
                    .Include(i => i.Items)
                    .FirstOrDefaultAsync(i => i.Id == id);

                if (order == null)
                {
                    TempData["ErrorMessage"] = "الأوردر غير موجود";
                    return RedirectToAction(nameof(Index));
                }

                // حذف جميع العناصر المرتبطة بالأوردر
                if (order.Items != null && order.Items.Any())
                {
                    _context.InvoiceItems.RemoveRange(order.Items);
                }

                // حذف الأوردر
                _context.Invoices.Remove(order);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"تم حذف الأوردر رقم {order.OrderNumber} بنجاح";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "حدث خطأ أثناء حذف الأوردر";
                // يمكن إضافة logging هنا
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Orders/DeleteAll
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAll()
        {
            try
            {
                // حذف جميع عناصر الفواتير أولاً
                var allInvoiceItems = await _context.InvoiceItems.ToListAsync();
                _context.InvoiceItems.RemoveRange(allInvoiceItems);

                // حذف جميع الفواتير
                var allInvoices = await _context.Invoices.ToListAsync();
                _context.Invoices.RemoveRange(allInvoices);

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "تم حذف جميع الأوردرات بنجاح";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "حدث خطأ أثناء حذف جميع الأوردرات";
                // يمكن إضافة logging هنا
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
