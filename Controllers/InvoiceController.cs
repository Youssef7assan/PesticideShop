using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PesticideShop.Data;
using PesticideShop.Models;
using PesticideShop.Services;
using PesticideShop.Extensions;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace PesticideShop.Controllers
{
    [Authorize]
    public class InvoiceController : Controller
    {
        private readonly ApplicationDbContext _context;

        public InvoiceController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Invoice
        public async Task<IActionResult> Index()
        {
            var invoices = await _context.Invoices
                .Include(i => i.Customer)
                .Include(i => i.Items)
                .ThenInclude(ii => ii.Product)
                .OrderByDescending(i => i.CreatedAt)
                .Select(i => new InvoiceListViewModel
                {
                    Id = i.Id,
                    InvoiceNumber = i.InvoiceNumber,
                    OrderNumber = i.OrderNumber,
                    CustomerName = i.Customer.Name,
                    CustomerPhone = i.Customer.PhoneNumber,
                    InvoiceDate = i.InvoiceDate,
                    OrderOrigin = i.OrderOrigin,
                    Type = i.Type,
                    Status = i.Status,
                    TotalAmount = i.TotalAmount,
                    AmountPaid = i.AmountPaid,
                    RemainingAmount = i.RemainingAmount,
                    ItemsCount = i.Items.Count
                })
                .ToListAsync();

            return View(invoices);
        }

        // GET: Invoice/Create
        public async Task<IActionResult> Create()
        {
            var customers = await _context.Customers.ToListAsync();
            var products = await _context.Products
                .Where(p => p.Quantity > 0)
                .Select(p => new { p.Id, p.Name, p.Price, p.Quantity })
                .ToListAsync();

            ViewBag.Customers = customers;
            ViewBag.Products = products;

            var viewModel = new InvoiceViewModel
            {
                InvoiceNumber = GenerateInvoiceNumber(),
                OrderNumber = GenerateOrderNumber(),
                InvoiceDate = DateTime.Now,
                Items = new List<InvoiceItemViewModel>()
            };

            return View(viewModel);
        }

        // POST: Invoice/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(InvoiceViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var invoice = new Invoice
                    {
                        CustomerId = viewModel.CustomerId,
                        InvoiceNumber = viewModel.InvoiceNumber,
                        OrderNumber = viewModel.OrderNumber,
                        PolicyNumber = viewModel.PolicyNumber,
                        OrderOrigin = viewModel.OrderOrigin,
                        InvoiceDate = viewModel.InvoiceDate,
                        DueDate = viewModel.DueDate,
                        Discount = viewModel.Discount,
                        ShippingCost = viewModel.ShippingCost,
                        AmountPaid = viewModel.AmountPaid,
                        Type = viewModel.Type,
                        Status = InvoiceStatus.Draft,
                        Notes = viewModel.Notes,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };

                    // Calculate totals
                    invoice.TotalAmount = viewModel.GrandTotal;
                    invoice.RemainingAmount = viewModel.RemainingAmount;

                    _context.Invoices.Add(invoice);
                    await _context.SaveChangesAsync();

                    // Add invoice items
                    foreach (var item in viewModel.Items)
                    {
                        var invoiceItem = new InvoiceItem
                        {
                            InvoiceId = invoice.Id,
                            ProductId = item.ProductId,
                            Quantity = item.Quantity,
                            UnitPrice = item.UnitPrice,
                            Discount = item.Discount,
                            TotalPrice = item.TotalPrice,
                            Notes = item.Notes,
                            CreatedAt = DateTime.Now
                        };

                        _context.InvoiceItems.Add(invoiceItem);

                        // Update product quantity if it's a sale invoice
                        if (invoice.Type == InvoiceType.Sale)
                        {
                            var product = await _context.Products.FindAsync(item.ProductId);
                            if (product != null)
                            {
                                product.Quantity -= item.Quantity;
                                if (product.Quantity < 0) product.Quantity = 0;
                            }
                        }
                    }

                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "تم إنشاء الفاتورة بنجاح!";
                    return RedirectToAction(nameof(Details), new { id = invoice.Id });
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"حدث خطأ أثناء إنشاء الفاتورة: {ex.Message}");
                }
            }

            // Reload view data if validation fails
            var customers = await _context.Customers.ToListAsync();
            var products = await _context.Products
                .Where(p => p.Quantity > 0)
                .Select(p => new { p.Id, p.Name, p.Price, p.Quantity })
                .ToListAsync();

            ViewBag.Customers = customers;
            ViewBag.Products = products;

            return View(viewModel);
        }

        // GET: Invoice/Details/5
        public async Task<IActionResult> Details(int? id, string? invoiceNumber)
        {
            Invoice? invoice = null;

            if (!string.IsNullOrEmpty(invoiceNumber))
            {
                // البحث بواسطة رقم الفاتورة
                invoice = await _context.Invoices
                    .Include(i => i.Customer)
                    .Include(i => i.Items)
                    .ThenInclude(ii => ii.Product)
                    .FirstOrDefaultAsync(m => m.InvoiceNumber == invoiceNumber);
            }
            else if (id.HasValue)
            {
                // البحث بواسطة ID
                invoice = await _context.Invoices
                    .Include(i => i.Customer)
                    .Include(i => i.Items)
                    .ThenInclude(ii => ii.Product)
                    .FirstOrDefaultAsync(m => m.Id == id);
            }

            if (invoice == null)
            {
                return NotFound();
            }

            return View(invoice);
        }

        // GET: Invoice/ModernInvoice/5
        public async Task<IActionResult> ModernInvoice(int? id, string? invoiceNumber)
        {
            Invoice? invoice = null;

            if (!string.IsNullOrEmpty(invoiceNumber))
            {
                // البحث بواسطة رقم الفاتورة
                invoice = await _context.Invoices
                    .Include(i => i.Customer)
                    .Include(i => i.Items)
                    .ThenInclude(ii => ii.Product)
                    .FirstOrDefaultAsync(m => m.InvoiceNumber == invoiceNumber);
            }
            else if (id.HasValue)
            {
                // البحث بواسطة ID
                invoice = await _context.Invoices
                    .Include(i => i.Customer)
                    .Include(i => i.Items)
                    .ThenInclude(ii => ii.Product)
                    .FirstOrDefaultAsync(m => m.Id == id);
            }

            if (invoice == null)
            {
                return NotFound();
            }

            return View(invoice);
        }

        // GET: Invoice/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var invoice = await _context.Invoices
                .Include(i => i.Items)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (invoice == null)
            {
                return NotFound();
            }

            var viewModel = new InvoiceViewModel
            {
                Id = invoice.Id,
                CustomerId = invoice.CustomerId,
                InvoiceNumber = invoice.InvoiceNumber,
                OrderNumber = invoice.OrderNumber,
                PolicyNumber = invoice.PolicyNumber,
                OrderOrigin = invoice.OrderOrigin,
                InvoiceDate = invoice.InvoiceDate,
                DueDate = invoice.DueDate,
                Discount = invoice.Discount,
                ShippingCost = invoice.ShippingCost,
                AmountPaid = invoice.AmountPaid,
                Type = invoice.Type,
                Notes = invoice.Notes,
                Items = invoice.Items.Select(ii => new InvoiceItemViewModel
                {
                    Id = ii.Id,
                    ProductId = ii.ProductId,
                    Quantity = ii.Quantity,
                    UnitPrice = ii.UnitPrice,
                    Discount = ii.Discount,
                    TotalPrice = ii.TotalPrice,
                    Notes = ii.Notes
                }).ToList()
            };

            var customers = await _context.Customers.ToListAsync();
            var products = await _context.Products
                .Where(p => p.Quantity > 0)
                .Select(p => new { p.Id, p.Name, p.Price, p.Quantity })
                .ToListAsync();

            ViewBag.Customers = customers;
            ViewBag.Products = products;

            return View(viewModel);
        }

        // POST: Invoice/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, InvoiceViewModel viewModel)
        {
            if (id != viewModel.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var invoice = await _context.Invoices
                        .Include(i => i.Items)
                        .FirstOrDefaultAsync(i => i.Id == id);

                    if (invoice == null)
                    {
                        return NotFound();
                    }

                    // Update invoice properties
                    invoice.CustomerId = viewModel.CustomerId;
                    invoice.InvoiceNumber = viewModel.InvoiceNumber;
                    invoice.OrderNumber = viewModel.OrderNumber;
                    invoice.PolicyNumber = viewModel.PolicyNumber;
                    invoice.OrderOrigin = viewModel.OrderOrigin;
                    invoice.InvoiceDate = viewModel.InvoiceDate;
                    invoice.DueDate = viewModel.DueDate;
                    invoice.Discount = viewModel.Discount;
                    invoice.ShippingCost = viewModel.ShippingCost;
                    invoice.AmountPaid = viewModel.AmountPaid;
                    invoice.Type = viewModel.Type;
                    invoice.Notes = viewModel.Notes;
                    invoice.UpdatedAt = DateTime.Now;

                    // Calculate totals
                    invoice.TotalAmount = viewModel.GrandTotal;
                    invoice.RemainingAmount = viewModel.RemainingAmount;

                    // Remove existing items
                    _context.InvoiceItems.RemoveRange(invoice.Items);

                    // Add new items
                    foreach (var item in viewModel.Items)
                    {
                        var invoiceItem = new InvoiceItem
                        {
                            InvoiceId = invoice.Id,
                            ProductId = item.ProductId,
                            Quantity = item.Quantity,
                            UnitPrice = item.UnitPrice,
                            Discount = item.Discount,
                            TotalPrice = item.TotalPrice,
                            Notes = item.Notes,
                            CreatedAt = DateTime.Now
                        };

                        _context.InvoiceItems.Add(invoiceItem);
                    }

                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "تم تحديث الفاتورة بنجاح!";
                    return RedirectToAction(nameof(Details), new { id = invoice.Id });
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"حدث خطأ أثناء تحديث الفاتورة: {ex.Message}");
                }
            }

            // Reload view data if validation fails
            var customers = await _context.Customers.ToListAsync();
            var products = await _context.Products
                .Where(p => p.Quantity > 0)
                .Select(p => new { p.Id, p.Name, p.Price, p.Quantity })
                .ToListAsync();

            ViewBag.Customers = customers;
            ViewBag.Products = products;

            return View(viewModel);
        }

        // GET: Invoice/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var invoice = await _context.Invoices
                .Include(i => i.Customer)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (invoice == null)
            {
                return NotFound();
            }

            return View(invoice);
        }

        // POST: Invoice/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Items)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (invoice != null)
            {
                // لا نرجع المنتجات للمخزون عند حذف الفواتير
                // لأن الفواتير تمثل مبيعات فعلية تمت بالفعل

                _context.Invoices.Remove(invoice);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "تم حذف الفاتورة بنجاح!";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Invoice/Print/5
        public async Task<IActionResult> Print(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var invoice = await _context.Invoices
                .Include(i => i.Customer)
                .Include(i => i.Items)
                .ThenInclude(ii => ii.Product)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (invoice == null)
            {
                return NotFound();
            }

            return View(invoice);
        }


        // GET: Invoice/SendWhatsApp/5
        public async Task<IActionResult> SendWhatsApp(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var invoice = await _context.Invoices
                .Include(i => i.Customer)
                .Include(i => i.Items)
                .ThenInclude(ii => ii.Product)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (invoice == null)
            {
                return NotFound();
            }

            // Validate phone number
            if (string.IsNullOrWhiteSpace(invoice.Customer.PhoneNumber))
            {
                TempData["ErrorMessage"] = "رقم هاتف العميل مفقود. يرجى إضافة رقم هاتف أولاً.";
                return RedirectToAction(nameof(Details), new { id = id });
            }

            try
            {
                // استخدام WhatsAppService لإرسال الرسالة
                var whatsappService = HttpContext.RequestServices.GetRequiredService<IWhatsAppService>();
                
                // إنشاء رسالة نصية مفصلة
                var message = await whatsappService.GenerateWhatsAppMessage(invoice);
                
                // إنشاء رابط WhatsApp مع الرسالة
                var whatsappUrl = whatsappService.GetWhatsAppUrl(invoice.Customer.PhoneNumber, message);

                // Return JavaScript to open WhatsApp in new tab
                var script = $@"
                    <script>
                        window.open('{whatsappUrl}', '_blank');
                        window.history.back();
                    </script>";
                return Content(script, "text/html");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"خطأ في إرسال الفاتورة عبر الواتساب: {ex.Message}";
                return RedirectToAction(nameof(Details), new { id = id });
            }
        }

        // Helper methods
        private string GenerateInvoiceNumber()
        {
            // البحث عن أعلى رقم فاتورة في النظام
            var maxInvoiceNumber = _context.Invoices
                .Where(i => !string.IsNullOrEmpty(i.InvoiceNumber))
                .Select(i => i.InvoiceNumber)
                .ToList()
                .Where(invNum => int.TryParse(invNum, out _))
                .Select(invNum => int.Parse(invNum))
                .DefaultIfEmpty(0)
                .Max();
            
            // الرقم التالي هو أعلى رقم + 1
            int nextNumber = maxInvoiceNumber + 1;
            
            // إرجاع الرقم مع 4 أصفار في البداية
            return nextNumber.ToString("D4");
        }

        private string GenerateOrderNumber()
        {
            // البحث عن أعلى رقم أمر في النظام
            var maxOrderNumber = _context.Invoices
                .Where(i => !string.IsNullOrEmpty(i.OrderNumber))
                .Select(i => i.OrderNumber)
                .ToList()
                .Where(orderNum => int.TryParse(orderNum, out _))
                .Select(orderNum => int.Parse(orderNum))
                .DefaultIfEmpty(0)
                .Max();
            
            // الرقم التالي هو أعلى رقم + 1
            int nextNumber = maxOrderNumber + 1;
            
            // إرجاع الرقم مع 4 أصفار في البداية
            return nextNumber.ToString("D4");
        }

        private string FormatPhoneNumberForWhatsApp(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return string.Empty;

            // Remove all non-digit characters
            var digitsOnly = new string(phoneNumber.Where(char.IsDigit).ToArray());

            // Handle Egyptian phone numbers
            if (digitsOnly.Length == 11 && digitsOnly.StartsWith("01"))
            {
                return "2" + digitsOnly;
            }
            else if (digitsOnly.Length == 10 && digitsOnly.StartsWith("1"))
            {
                return "20" + digitsOnly;
            }
            else if (digitsOnly.Length == 12 && digitsOnly.StartsWith("201"))
            {
                return digitsOnly;
            }
            else if (digitsOnly.Length == 9 && digitsOnly.StartsWith("1"))
            {
                return "201" + digitsOnly;
            }

            return string.Empty;
        }

        private string GenerateWhatsAppMessage(Invoice invoice)
        {
            var message = $"🌱 *الشركة المصرية للمبيدات والأسمدة الزراعية*\n";
            message += $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n";

            message += $"🧾 *فاتورة رقم:* {invoice.InvoiceNumber}\n";
            message += $"📋 *رقم الطلب:* {invoice.OrderNumber}\n";
            if (!string.IsNullOrEmpty(invoice.PolicyNumber))
            {
                message += $"📄 *رقم البوليصة:* {invoice.PolicyNumber}\n";
            }
            message += $"📅 *تاريخ الفاتورة:* {invoice.InvoiceDate:dd/MM/yyyy}\n";
            message += $"🌐 *مصدر الطلب:* {invoice.OrderOrigin.GetDisplayName()}\n";
            message += $"🏷️ *نوع الفاتورة:* {invoice.Type.GetDisplayName()}\n\n";

            message += $"👤 *معلومات العميل:*\n";
            message += $"• الاسم: *{invoice.Customer.Name}*\n";
            message += $"• رقم الهاتف: `{invoice.Customer.PhoneNumber}`\n\n";

            message += $"📦 *المنتجات:*\n";
            foreach (var item in invoice.Items)
            {
                message += $"• {item.Product.Name} - {item.Quantity} قطعة - {item.TotalPrice:N2} جنيه\n";
            }

            message += $"\n💰 *ملخص الفاتورة:*\n";
            message += $"• إجمالي المنتجات: {invoice.SubTotal:N2} جنيه\n";
            message += $"• سعر الشحن: {invoice.ShippingCost:N2} جنيه\n";
            message += $"• الخصم: {invoice.Discount:N2} جنيه\n";
            message += $"• الإجمالي الكلي: *{invoice.GrandTotal:N2}* جنيه\n";
            message += $"• المدفوع: {invoice.AmountPaid:N2} جنيه\n";
            message += $"• المتبقي: *{invoice.RemainingAmount:N2}* جنيه\n\n";

            message += $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n";
            message += $"💡 للاستفسار: 01097972975\n";
            message += $"🙏 *شكراً لثقتكم بنا*\n";
            message += $"🌱 *الشركة المصرية للمبيدات والأسمدة الزراعية*";

            return message;
        }
    }
}
