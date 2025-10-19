using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PesticideShop.Data;
using PesticideShop.Models;
using PesticideShop.Services;
using System.ComponentModel.DataAnnotations;

namespace PesticideShop.Controllers
{
    [Authorize]
    public class ExchangeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly FinancialService _financialService;

        public ExchangeController(ApplicationDbContext context, FinancialService financialService)
        {
            _context = context;
            _financialService = financialService;
        }

        private async Task LoadProductsAsync()
        {
            ViewBag.Products = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.Quantity > 0)
                .OrderBy(p => p.Name)
                .ToListAsync();
        }

        // GET: Exchange
        public async Task<IActionResult> Index()
        {
            var exchanges = await _context.ExchangeTrackings
                .Include(e => e.OldProduct)
                .Include(e => e.NewProduct)
                .OrderByDescending(e => e.ExchangeDate)
                .ToListAsync();

            return View(exchanges);
        }

        // GET: Exchange/Create
        public async Task<IActionResult> Create(string? originalInvoiceNumber = null)
        {
            ViewBag.OriginalInvoiceNumber = originalInvoiceNumber;
            await LoadProductsAsync();

            return View();
        }

        // POST: Exchange/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ExchangeRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    await LoadProductsAsync();
                    return View(request);
                }

                // Validate original invoice exists
                var originalInvoice = await _context.Invoices
                    .Include(i => i.Items)
                    .ThenInclude(ii => ii.Product)
                    .FirstOrDefaultAsync(i => i.InvoiceNumber == request.OriginalInvoiceNumber);

                if (originalInvoice == null)
                {
                    ModelState.AddModelError("OriginalInvoiceNumber", "الفاتورة الأصلية غير موجودة");
                    await LoadProductsAsync();
                    return View(request);
                }

                // Validate old product exists in original invoice
                var originalItem = originalInvoice.Items
                    .FirstOrDefault(i => i.ProductId == request.OldProductId);

                if (originalItem == null)
                {
                    ModelState.AddModelError("OldProductId", "المنتج المحدد غير موجود في الفاتورة الأصلية");
                    await LoadProductsAsync();
                    return View(request);
                }

                // Check if quantity is available for exchange
                var previousExchanges = await _context.ExchangeTrackings
                    .Where(e => e.OriginalInvoiceNumber == request.OriginalInvoiceNumber 
                               && e.OldProductId == request.OldProductId)
                    .SumAsync(e => e.ExchangedQuantity);

                var availableForExchange = Math.Abs(originalItem.Quantity) - previousExchanges;
                if (request.ExchangedQuantity > availableForExchange)
                {
                    ModelState.AddModelError("ExchangedQuantity", 
                        $"الكمية المتاحة للاستبدال: {availableForExchange}");
                    await LoadProductsAsync();
                    return View(request);
                }

                // Get product details
                var oldProduct = await _context.Products.FindAsync(request.OldProductId);
                var newProduct = await _context.Products.FindAsync(request.NewProductId);

                if (oldProduct == null || newProduct == null)
                {
                    ModelState.AddModelError("", "منتج غير صحيح");
                    await LoadProductsAsync();
                    return View(request);
                }

                // Check new product availability
                if (newProduct.Quantity < request.ExchangedQuantity)
                {
                    ModelState.AddModelError("NewProductId", 
                        $"الكمية المتاحة من المنتج الجديد: {newProduct.Quantity}");
                    await LoadProductsAsync();
                    return View(request);
                }

                // Calculate price difference
                var priceDifference = (newProduct.Price - oldProduct.Price) * request.ExchangedQuantity;

                // Use exchange invoice number from request (already includes EXC prefix)
                var exchangeInvoiceNumber = request.ExchangeInvoiceNumber;

                // Create exchange tracking record
                var exchangeTracking = new ExchangeTracking
                {
                    OriginalInvoiceNumber = request.OriginalInvoiceNumber,
                    ExchangeInvoiceNumber = exchangeInvoiceNumber,
                    OldProductId = request.OldProductId,
                    NewProductId = request.NewProductId,
                    ExchangedQuantity = request.ExchangedQuantity,
                    PriceDifference = priceDifference,
                    ExchangeReason = request.ExchangeReason,
                    ExchangeDate = DateTime.Now,
                    CreatedBy = User.Identity?.Name ?? "Unknown",
                    Notes = request.Notes
                };

                _context.ExchangeTrackings.Add(exchangeTracking);

                // Create exchange invoice
                var exchangeInvoice = new Invoice
                {
                    CustomerId = originalInvoice.CustomerId,
                    InvoiceNumber = exchangeInvoiceNumber,
                    OrderNumber = $"EXC-{originalInvoice.OrderNumber}",
                    OrderOrigin = originalInvoice.OrderOrigin,
                    InvoiceDate = DateTime.Now,
                    Type = InvoiceType.Exchange,
                    Status = priceDifference <= 0 ? InvoiceStatus.Paid : InvoiceStatus.Sent,
                    AmountPaid = priceDifference <= 0 ? Math.Abs(priceDifference) : 0,
                    TotalAmount = Math.Abs(priceDifference),
                    RemainingAmount = priceDifference > 0 ? priceDifference : 0,
                    OriginalInvoiceNumber = request.OriginalInvoiceNumber,
                    ReturnReason = request.ExchangeReason,
                    CashierName = User.Identity?.Name ?? "نظام",
                    CreatedAt = DateTime.Now,
                    Notes = $"استبدال {request.ExchangedQuantity} من {oldProduct.Name} بـ {newProduct.Name}"
                };

                _context.Invoices.Add(exchangeInvoice);
                await _context.SaveChangesAsync();

                // Calculate the actual price paid by customer for old product (after discount)
                var actualOldPricePaid = originalItem.UnitPrice - (originalItem.Discount / Math.Abs(originalItem.Quantity));
                
                // Add invoice items (negative for old product, positive for new product)
                var oldItem = new InvoiceItem
                {
                    InvoiceId = exchangeInvoice.Id,
                    ProductId = request.OldProductId,
                    Quantity = -request.ExchangedQuantity,
                    UnitPrice = actualOldPricePaid, // السعر الفعلي الذي دفعه العميل
                    TotalPrice = -actualOldPricePaid * request.ExchangedQuantity, // السعر الفعلي للاستبدال
                    CreatedAt = DateTime.Now,
                    Notes = "منتج مستبدل (خروج)"
                };

                var newItem = new InvoiceItem
                {
                    InvoiceId = exchangeInvoice.Id,
                    ProductId = request.NewProductId,
                    Quantity = request.ExchangedQuantity,
                    UnitPrice = newProduct.Price,
                    TotalPrice = newProduct.Price * request.ExchangedQuantity,
                    CreatedAt = DateTime.Now,
                    Notes = "منتج جديد (دخول)"
                };

                _context.InvoiceItems.AddRange(oldItem, newItem);

                // Create CustomerTransaction records for exchange
                // Old product (negative quantity - return)
                var oldProductTransaction = new CustomerTransaction
                {
                    CustomerId = originalInvoice.CustomerId,
                    ProductId = request.OldProductId,
                    Quantity = -request.ExchangedQuantity, // Negative quantity for return
                    Price = actualOldPricePaid, // السعر الفعلي الذي دفعه العميل
                    TotalPrice = -actualOldPricePaid * request.ExchangedQuantity, // السعر الفعلي للاستبدال
                    Discount = 0, // No discount on returns - original discount is not considered a loss
                    ShippingCost = 0,
                    AmountPaid = 0,
                    Date = DateTime.Now,
                    Notes = $"استبدال {request.ExchangedQuantity} من {oldProduct.Name} (خروج)"
                };

                // New product (positive quantity - sale)
                var newProductTransaction = new CustomerTransaction
                {
                    CustomerId = originalInvoice.CustomerId,
                    ProductId = request.NewProductId,
                    Quantity = request.ExchangedQuantity, // Positive quantity for sale
                    Price = newProduct.Price,
                    TotalPrice = newProduct.Price * request.ExchangedQuantity, // Positive total
                    Discount = 0, // No discount on exchanges
                    ShippingCost = 0,
                    AmountPaid = priceDifference > 0 ? priceDifference : 0,
                    Date = DateTime.Now,
                    Notes = $"استبدال {request.ExchangedQuantity} من {newProduct.Name} (دخول)"
                };

                _context.CustomerTransactions.AddRange(oldProductTransaction, newProductTransaction);

                // Update inventory
                oldProduct.Quantity += request.ExchangedQuantity; // Return old product to inventory
                newProduct.Quantity -= request.ExchangedQuantity; // Take new product from inventory

                await _context.SaveChangesAsync();

                // Log activity
                var activityLog = new ActivityLog
                {
                    Action = "Exchange Product",
                    Details = $"استبدال {request.ExchangedQuantity} من {oldProduct.Name} بـ {newProduct.Name} للفاتورة {request.OriginalInvoiceNumber}",
                    UserId = User.Identity?.Name ?? "Unknown",
                    Timestamp = DateTime.Now
                };
                _context.ActivityLogs.Add(activityLog);
                await _context.SaveChangesAsync();

                // Update financial statistics and daily inventory
                await _financialService.UpdateFinancialsAfterExchangeAsync(exchangeTracking);

                TempData["SuccessMessage"] = "تم الاستبدال بنجاح وتحديث الإحصائيات";
                return RedirectToAction("Details", "Invoice", new { id = exchangeInvoice.Id });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"حدث خطأ: {ex.Message}");
                await LoadProductsAsync();
                return View(request);
            }
        }

        // GET: Exchange/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var exchange = await _context.ExchangeTrackings
                .Include(e => e.OldProduct)
                .Include(e => e.NewProduct)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (exchange == null)
            {
                return NotFound();
            }

            return View(exchange);
        }

        // API: Get invoice items for validation
        [HttpGet]
        public async Task<IActionResult> GetInvoiceItems(string invoiceNumber)
        {
            try
            {
                if (string.IsNullOrEmpty(invoiceNumber))
                {
                    return Json(new { success = false, message = "رقم الفاتورة مطلوب" });
                }

                var invoice = await _context.Invoices
                    .Include(i => i.Items)
                    .ThenInclude(ii => ii.Product)
                    .FirstOrDefaultAsync(i => i.InvoiceNumber == invoiceNumber);

                if (invoice == null)
                {
                    return Json(new { success = false, message = "الفاتورة غير موجودة" });
                }

                var items = invoice.Items.Select(i => new
                {
                    productId = i.ProductId,
                    productName = i.Product?.Name ?? "منتج غير محدد",
                    quantity = i.Quantity,
                    unitPrice = i.UnitPrice,
                    color = i.Color ?? "",
                    size = i.Size ?? "",
                    canExchange = i.Quantity > 0 // Only positive quantities can be exchanged
                }).ToList();

                return Json(new { success = true, items, invoiceId = invoice.Id, totalItems = items.Count });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // DELETE: Delete all exchanges
        [HttpDelete]
        public async Task<IActionResult> DeleteAll()
        {
            try
            {
                // Get all exchanges
                var exchanges = await _context.ExchangeTrackings.ToListAsync();

                if (!exchanges.Any())
                {
                    return Json(new { success = true, message = "لا توجد استبدالات للحذف" });
                }

                var exchangeCount = exchanges.Count;

                // Delete all exchanges
                _context.ExchangeTrackings.RemoveRange(exchanges);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = $"تم حذف جميع الاستبدالات بنجاح ({exchangeCount} استبدال)" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"فشل في حذف الاستبدالات: {ex.Message}" });
            }
        }

        // DELETE: Delete single exchange
        [HttpDelete]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var exchange = await _context.ExchangeTrackings.FindAsync(id);

                if (exchange == null)
                {
                    return Json(new { success = false, message = "الاستبدال غير موجود" });
                }

                _context.ExchangeTrackings.Remove(exchange);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "تم حذف الاستبدال بنجاح" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"فشل في حذف الاستبدال: {ex.Message}" });
            }
        }

        // GET: Exchange/MultiCreate
        public async Task<IActionResult> MultiCreate(string? originalInvoiceNumber = null)
        {
            ViewBag.OriginalInvoiceNumber = originalInvoiceNumber;
            await LoadProductsAsync();

            var request = new MultiExchangeRequest
            {
                OriginalInvoiceNumber = originalInvoiceNumber ?? "",
                ExchangeInvoiceNumber = $"EXC-{DateTime.Now:yyyyMMdd}-{DateTime.Now:HHmmss}",
                ExchangeItems = new List<ExchangeItem>()
            };

            return View(request);
        }

        // POST: Exchange/MultiCreate
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MultiCreate(MultiExchangeRequest request)
        {
            try
            {
                if (!ModelState.IsValid || !request.ExchangeItems.Any())
                {
                    await LoadProductsAsync();
                    return View(request);
                }

                // Validate original invoice exists
                var originalInvoice = await _context.Invoices
                    .Include(i => i.Items)
                    .ThenInclude(ii => ii.Product)
                    .FirstOrDefaultAsync(i => i.InvoiceNumber == request.OriginalInvoiceNumber);

                if (originalInvoice == null)
                {
                    ModelState.AddModelError("OriginalInvoiceNumber", "الفاتورة الأصلية غير موجودة");
                    await LoadProductsAsync();
                    return View(request);
                }

                // Validate all exchange items
                foreach (var item in request.ExchangeItems)
                {
                    // Validate old product exists in original invoice
                    var originalItem = originalInvoice.Items
                        .FirstOrDefault(i => i.ProductId == item.OldProductId);

                    if (originalItem == null)
                    {
                        ModelState.AddModelError("", $"المنتج {item.OldProductName} غير موجود في الفاتورة الأصلية");
                        await LoadProductsAsync();
                        return View(request);
                    }

                    // Validate quantity is positive
                    if (item.ExchangedQuantity <= 0)
                    {
                        ModelState.AddModelError("", $"الكمية يجب أن تكون أكبر من صفر للمنتج {item.OldProductName}");
                        await LoadProductsAsync();
                        return View(request);
                    }

                    // Check if quantity is available for exchange
                    var previousExchanges = await _context.ExchangeTrackings
                        .Where(e => e.OriginalInvoiceNumber == request.OriginalInvoiceNumber 
                                   && e.OldProductId == item.OldProductId)
                        .SumAsync(e => e.ExchangedQuantity);

                    var availableForExchange = Math.Abs(originalItem.Quantity) - previousExchanges;
                    if (item.ExchangedQuantity > availableForExchange)
                    {
                        ModelState.AddModelError("", $"الكمية المدخلة ({item.ExchangedQuantity}) تتجاوز الكمية المتاحة للاستبدال من {item.OldProductName} ({availableForExchange})");
                        await LoadProductsAsync();
                        return View(request);
                    }

                    // Check new product availability
                    var newProduct = await _context.Products.FindAsync(item.NewProductId);
                    if (newProduct == null)
                    {
                        ModelState.AddModelError("", $"المنتج الجديد {item.NewProductName} غير موجود");
                        await LoadProductsAsync();
                        return View(request);
                    }

                    if (newProduct.Quantity < item.ExchangedQuantity)
                    {
                        ModelState.AddModelError("", $"الكمية المتاحة من {item.NewProductName}: {newProduct.Quantity}");
                        await LoadProductsAsync();
                        return View(request);
                    }
                }

                // All validations passed, proceed with multi-exchange
                var exchangeTrackings = new List<ExchangeTracking>();
                var invoiceItems = new List<InvoiceItem>();
                var customerTransactions = new List<CustomerTransaction>();
                var totalPriceDifference = 0m;

                foreach (var item in request.ExchangeItems)
                {
                    var oldProduct = await _context.Products.FindAsync(item.OldProductId);
                    var newProduct = await _context.Products.FindAsync(item.NewProductId);
                    var originalItem = originalInvoice.Items.First(i => i.ProductId == item.OldProductId);

                    // Calculate price difference
                    var actualOldPricePaid = originalItem.UnitPrice - (originalItem.Discount / Math.Abs(originalItem.Quantity));
                    var priceDifference = (newProduct.Price - actualOldPricePaid) * item.ExchangedQuantity;
                    totalPriceDifference += priceDifference;

                    // Create exchange tracking record
                    var exchangeTracking = new ExchangeTracking
                    {
                        OriginalInvoiceNumber = request.OriginalInvoiceNumber,
                        ExchangeInvoiceNumber = request.ExchangeInvoiceNumber,
                        OldProductId = item.OldProductId,
                        NewProductId = item.NewProductId,
                        ExchangedQuantity = item.ExchangedQuantity,
                        PriceDifference = priceDifference,
                        ExchangeReason = request.ExchangeReason,
                        ExchangeDate = DateTime.Now,
                        CreatedBy = User.Identity?.Name ?? "Unknown",
                        Notes = request.Notes
                    };

                    exchangeTrackings.Add(exchangeTracking);

                    // Add invoice items
                    var oldItem = new InvoiceItem
                    {
                        InvoiceId = 0, // Will be set after invoice creation
                        ProductId = item.OldProductId,
                        Quantity = -item.ExchangedQuantity,
                        UnitPrice = actualOldPricePaid,
                        TotalPrice = -actualOldPricePaid * item.ExchangedQuantity,
                        CreatedAt = DateTime.Now,
                        Notes = $"منتج مستبدل (خروج): {oldProduct.Name}"
                    };

                    var newItem = new InvoiceItem
                    {
                        InvoiceId = 0, // Will be set after invoice creation
                        ProductId = item.NewProductId,
                        Quantity = item.ExchangedQuantity,
                        UnitPrice = newProduct.Price,
                        TotalPrice = newProduct.Price * item.ExchangedQuantity,
                        CreatedAt = DateTime.Now,
                        Notes = $"منتج جديد (دخول): {newProduct.Name}"
                    };

                    invoiceItems.AddRange(new[] { oldItem, newItem });

                    // Add customer transactions
                    var oldProductTransaction = new CustomerTransaction
                    {
                        CustomerId = originalInvoice.CustomerId,
                        ProductId = item.OldProductId,
                        Quantity = -item.ExchangedQuantity,
                        Price = actualOldPricePaid,
                        TotalPrice = -actualOldPricePaid * item.ExchangedQuantity,
                        Discount = 0,
                        ShippingCost = 0,
                        AmountPaid = 0,
                        Date = DateTime.Now,
                        Notes = $"استبدال {item.ExchangedQuantity} من {oldProduct.Name} (خروج)"
                    };

                    var newProductTransaction = new CustomerTransaction
                    {
                        CustomerId = originalInvoice.CustomerId,
                        ProductId = item.NewProductId,
                        Quantity = item.ExchangedQuantity,
                        Price = newProduct.Price,
                        TotalPrice = newProduct.Price * item.ExchangedQuantity,
                        Discount = 0,
                        ShippingCost = 0,
                        AmountPaid = priceDifference > 0 ? priceDifference : 0,
                        Date = DateTime.Now,
                        Notes = $"استبدال {item.ExchangedQuantity} من {newProduct.Name} (دخول)"
                    };

                    customerTransactions.AddRange(new[] { oldProductTransaction, newProductTransaction });

                    // Update inventory
                    oldProduct.Quantity += item.ExchangedQuantity;
                    newProduct.Quantity -= item.ExchangedQuantity;
                }

                // Create exchange invoice
                var exchangeInvoice = new Invoice
                {
                    CustomerId = originalInvoice.CustomerId,
                    InvoiceNumber = request.ExchangeInvoiceNumber,
                    OrderNumber = $"EXC-{originalInvoice.OrderNumber}",
                    OrderOrigin = originalInvoice.OrderOrigin,
                    InvoiceDate = DateTime.Now,
                    Type = InvoiceType.Exchange,
                    Status = totalPriceDifference <= 0 ? InvoiceStatus.Paid : InvoiceStatus.Sent,
                    AmountPaid = totalPriceDifference <= 0 ? Math.Abs(totalPriceDifference) : 0,
                    TotalAmount = Math.Abs(totalPriceDifference),
                    RemainingAmount = totalPriceDifference > 0 ? totalPriceDifference : 0,
                    OriginalInvoiceNumber = request.OriginalInvoiceNumber,
                    ReturnReason = request.ExchangeReason,
                    CashierName = User.Identity?.Name ?? "نظام",
                    CreatedAt = DateTime.Now,
                    Notes = $"استبدال متعدد: {request.ExchangeItems.Count} منتج"
                };

                _context.Invoices.Add(exchangeInvoice);
                await _context.SaveChangesAsync();

                // Update invoice items with correct invoice ID
                foreach (var item in invoiceItems)
                {
                    item.InvoiceId = exchangeInvoice.Id;
                }

                // Add all records to context
                _context.ExchangeTrackings.AddRange(exchangeTrackings);
                _context.InvoiceItems.AddRange(invoiceItems);
                _context.CustomerTransactions.AddRange(customerTransactions);

                await _context.SaveChangesAsync();

                // Log activity
                var activityLog = new ActivityLog
                {
                    Action = "Multi Exchange Products",
                    Details = $"استبدال متعدد: {request.ExchangeItems.Count} منتج للفاتورة {request.OriginalInvoiceNumber}",
                    UserId = User.Identity?.Name ?? "Unknown",
                    Timestamp = DateTime.Now
                };
                _context.ActivityLogs.Add(activityLog);
                await _context.SaveChangesAsync();

                // Update financial statistics
                foreach (var tracking in exchangeTrackings)
                {
                    await _financialService.UpdateFinancialsAfterExchangeAsync(tracking);
                }

                TempData["SuccessMessage"] = $"تم الاستبدال المتعدد بنجاح ({request.ExchangeItems.Count} منتج) وتحديث الإحصائيات";
                return RedirectToAction("Details", "Orders", new { id = exchangeInvoice.Id });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"حدث خطأ: {ex.Message}");
                await LoadProductsAsync();
                return View(request);
            }
        }

        // API: Add exchange item to multi-exchange
        [HttpPost]
        public async Task<IActionResult> AddExchangeItem([FromBody] ExchangeItem item)
        {
            try
            {
                var oldProduct = await _context.Products.FindAsync(item.OldProductId);
                var newProduct = await _context.Products.FindAsync(item.NewProductId);

                if (oldProduct == null || newProduct == null)
                {
                    return Json(new { success = false, message = "منتج غير موجود" });
                }

                item.OldProductName = oldProduct.Name;
                item.NewProductName = newProduct.Name;
                item.OldProductPrice = oldProduct.Price;
                item.NewProductPrice = newProduct.Price;
                item.PriceDifference = (newProduct.Price - oldProduct.Price) * item.ExchangedQuantity;

                return Json(new { success = true, item });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }

    // Exchange request model
    public class ExchangeRequest
    {
        [Required(ErrorMessage = "رقم الفاتورة الأصلية مطلوب")]
        public string OriginalInvoiceNumber { get; set; } = "";

        [Required(ErrorMessage = "رقم فاتورة الاستبدال مطلوب")]
        public string ExchangeInvoiceNumber { get; set; } = "";

        [Required(ErrorMessage = "المنتج القديم مطلوب")]
        public int OldProductId { get; set; }

        [Required(ErrorMessage = "المنتج الجديد مطلوب")]
        public int NewProductId { get; set; }

        [Required(ErrorMessage = "الكمية مطلوبة")]
        [Range(1, int.MaxValue, ErrorMessage = "الكمية يجب أن تكون أكبر من صفر")]
        public int ExchangedQuantity { get; set; }

        public string? ExchangeReason { get; set; }
        public string? Notes { get; set; }
    }
}
