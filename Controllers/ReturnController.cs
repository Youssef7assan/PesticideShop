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
    public class ReturnController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly FinancialService _financialService;

        public ReturnController(ApplicationDbContext context, FinancialService financialService)
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

        // GET: Return
        public async Task<IActionResult> Index()
        {
            var returns = await _context.ReturnTrackings
                .Include(r => r.Product)
                .OrderByDescending(r => r.ReturnDate)
                .ToListAsync();

            return View(returns);
        }

        // GET: Return/Create
        public async Task<IActionResult> Create(string? originalInvoiceNumber = null)
        {
            ViewBag.OriginalInvoiceNumber = originalInvoiceNumber;
            await LoadProductsAsync();

            return View();
        }

        // POST: Return/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ReturnRequest request)
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

                // Validate product exists in original invoice
                var originalItem = originalInvoice.Items
                    .FirstOrDefault(i => i.ProductId == request.ProductId);

                if (originalItem == null)
                {
                    ModelState.AddModelError("ProductId", "المنتج المحدد غير موجود في الفاتورة الأصلية");
                    await LoadProductsAsync();
                    return View(request);
                }

                // Check if quantity is available for return
                var previousReturns = await _context.ReturnTrackings
                    .Where(r => r.OriginalInvoiceNumber == request.OriginalInvoiceNumber 
                               && r.ProductId == request.ProductId)
                    .SumAsync(r => r.ReturnedQuantity);

                var availableForReturn = Math.Abs(originalItem.Quantity) - previousReturns;
                if (request.ReturnedQuantity > availableForReturn)
                {
                    ModelState.AddModelError("ReturnedQuantity", 
                        $"الكمية المتاحة للإرجاع: {availableForReturn}");
                    await LoadProductsAsync();
                    return View(request);
                }

                // Get product details
                var product = await _context.Products.FindAsync(request.ProductId);

                if (product == null)
                {
                    ModelState.AddModelError("", "منتج غير صحيح");
                    await LoadProductsAsync();
                    return View(request);
                }

                // Use return invoice number from request (already includes RTN prefix)
                var returnInvoiceNumber = request.ReturnInvoiceNumber;

                // Create return tracking record
                var returnTracking = new ReturnTracking
                {
                    OriginalInvoiceNumber = request.OriginalInvoiceNumber,
                    ReturnInvoiceNumber = returnInvoiceNumber,
                    ProductId = request.ProductId,
                    ReturnedQuantity = request.ReturnedQuantity,
                    ReturnReason = request.ReturnReason,
                    ReturnDate = DateTime.Now,
                    CreatedBy = User.Identity?.Name ?? "Unknown",
                    Notes = request.Notes
                };

                _context.ReturnTrackings.Add(returnTracking);

                // Create return invoice
                var returnInvoice = new Invoice
                {
                    CustomerId = originalInvoice.CustomerId,
                    InvoiceNumber = returnInvoiceNumber,
                    OrderNumber = $"RTN-{originalInvoice.OrderNumber}",
                    OrderOrigin = originalInvoice.OrderOrigin,
                    InvoiceDate = DateTime.Now,
                    Type = InvoiceType.Return,
                    Status = InvoiceStatus.Paid,
                    AmountPaid = 0,
                    TotalAmount = 0,
                    RemainingAmount = 0,
                    OriginalInvoiceNumber = request.OriginalInvoiceNumber,
                    ReturnReason = request.ReturnReason,
                    CashierName = User.Identity?.Name ?? "نظام",
                    CreatedAt = DateTime.Now,
                    Notes = $"إرجاع {request.ReturnedQuantity} من {product.Name}"
                };

                _context.Invoices.Add(returnInvoice);
                await _context.SaveChangesAsync();

                // Calculate the actual price paid by customer (after discount)
                var actualPricePaid = originalItem.UnitPrice - (originalItem.Discount / Math.Abs(originalItem.Quantity));
                
                // Add invoice items (negative quantity for return)
                var returnItem = new InvoiceItem
                {
                    InvoiceId = returnInvoice.Id,
                    ProductId = request.ProductId,
                    Quantity = -request.ReturnedQuantity,
                    UnitPrice = actualPricePaid, // السعر الفعلي الذي دفعه العميل
                    TotalPrice = -actualPricePaid * request.ReturnedQuantity, // السعر الفعلي للإرجاع
                    CreatedAt = DateTime.Now,
                    Notes = "منتج مرتجع"
                };

                _context.InvoiceItems.Add(returnItem);

                // Create CustomerTransaction for return (negative quantity)
                var customerTransaction = new CustomerTransaction
                {
                    CustomerId = originalInvoice.CustomerId,
                    ProductId = request.ProductId,
                    Quantity = -request.ReturnedQuantity, // Negative quantity for return
                    Price = actualPricePaid, // السعر الفعلي الذي دفعه العميل
                    TotalPrice = -actualPricePaid * request.ReturnedQuantity, // السعر الفعلي للإرجاع
                    Discount = 0, // No discount on returns - original discount is not considered a loss
                    ShippingCost = 0,
                    AmountPaid = 0,
                    Date = DateTime.Now,
                    Notes = $"إرجاع {request.ReturnedQuantity} من {product.Name}"
                };

                _context.CustomerTransactions.Add(customerTransaction);

                // Update inventory
                product.Quantity += request.ReturnedQuantity; // Return product to inventory

                await _context.SaveChangesAsync();

                // Log activity
                var activityLog = new ActivityLog
                {
                    Action = "Return Product",
                    Details = $"إرجاع {request.ReturnedQuantity} من {product.Name} للفاتورة {request.OriginalInvoiceNumber}",
                    UserId = User.Identity?.Name ?? "Unknown",
                    Timestamp = DateTime.Now
                };
                _context.ActivityLogs.Add(activityLog);
                await _context.SaveChangesAsync();

                // Update financial statistics and daily inventory
                await _financialService.UpdateFinancialsAfterReturnAsync(returnTracking);

                TempData["SuccessMessage"] = "تم الإرجاع بنجاح وتحديث الإحصائيات";
                return RedirectToAction("Details", "Invoice", new { id = returnInvoice.Id });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"حدث خطأ: {ex.Message}");
                await LoadProductsAsync();
                return View(request);
            }
        }

        // GET: Return/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var returnTracking = await _context.ReturnTrackings
                .Include(r => r.Product)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (returnTracking == null)
            {
                return NotFound();
            }

            return View(returnTracking);
        }

        // API: Get invoice items for validation
        [HttpGet]
        public async Task<IActionResult> GetInvoiceItems(string invoiceNumber)
        {
            try
            {
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
                    productName = i.Product?.Name,
                    quantity = i.Quantity,
                    unitPrice = i.UnitPrice,
                    color = i.Color,
                    size = i.Size,
                    canReturn = i.Quantity > 0 // Only positive quantities can be returned
                }).ToList();

                return Json(new { success = true, items });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // DELETE: Delete all returns
        [HttpDelete]
        public async Task<IActionResult> DeleteAll()
        {
            try
            {
                // Get all returns with products
                var returns = await _context.ReturnTrackings
                    .Include(r => r.Product)
                    .ToListAsync();

                if (!returns.Any())
                {
                    return Json(new { success = true, message = "لا توجد مرتجعات للحذف" });
                }

                var returnCount = returns.Count;
                int inventoryUpdated = 0;

                foreach (var returnTracking in returns)
                {
                    // إرجاع المخزون إلى وضعه الطبيعي (عكس الإرجاع)
                    var product = await _context.Products.FindAsync(returnTracking.ProductId);
                    if (product != null)
                    {
                        product.Quantity -= returnTracking.ReturnedQuantity;
                        inventoryUpdated++;
                    }

                    // حذف فاتورة الإرجاع إن وجدت
                    var returnInvoice = await _context.Invoices
                        .FirstOrDefaultAsync(i => i.InvoiceNumber == returnTracking.ReturnInvoiceNumber);
                    if (returnInvoice != null)
                    {
                        _context.Invoices.Remove(returnInvoice);
                    }

                    // حذف المعاملات المرتبطة
                    var customerTransactions = await _context.CustomerTransactions
                        .Where(ct => ct.Notes != null && ct.Notes.Contains(returnTracking.ReturnInvoiceNumber))
                        .ToListAsync();
                    if (customerTransactions.Any())
                    {
                        _context.CustomerTransactions.RemoveRange(customerTransactions);
                    }
                }

                // Delete all returns
                _context.ReturnTrackings.RemoveRange(returns);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = $"تم حذف جميع المرتجعات بنجاح ({returnCount} مرتجع) وإرجاع المخزون لـ {inventoryUpdated} منتج" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"فشل في حذف المرتجعات: {ex.Message}" });
            }
        }

        // DELETE: Delete single return
        [HttpDelete]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var returnTracking = await _context.ReturnTrackings
                    .Include(r => r.Product)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (returnTracking == null)
                {
                    return Json(new { success = false, message = "المرتجع غير موجود" });
                }

                // إرجاع المخزون إلى وضعه الطبيعي (عكس الإرجاع)
                var product = await _context.Products.FindAsync(returnTracking.ProductId);
                if (product != null)
                {
                    product.Quantity -= returnTracking.ReturnedQuantity;
                }

                // حذف فاتورة الإرجاع إن وجدت
                var returnInvoice = await _context.Invoices
                    .FirstOrDefaultAsync(i => i.InvoiceNumber == returnTracking.ReturnInvoiceNumber);
                if (returnInvoice != null)
                {
                    _context.Invoices.Remove(returnInvoice);
                }

                // حذف المعاملات المرتبطة
                var customerTransactions = await _context.CustomerTransactions
                    .Where(ct => ct.Notes != null && ct.Notes.Contains(returnTracking.ReturnInvoiceNumber))
                    .ToListAsync();
                if (customerTransactions.Any())
                {
                    _context.CustomerTransactions.RemoveRange(customerTransactions);
                }

                _context.ReturnTrackings.Remove(returnTracking);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "تم حذف المرتجع بنجاح وإرجاع المخزون" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"فشل في حذف المرتجع: {ex.Message}" });
            }
        }

        // GET: Return/MultiDelete
        public async Task<IActionResult> MultiDelete()
        {
            var returns = await _context.ReturnTrackings
                .Include(r => r.Product)
                .OrderByDescending(r => r.ReturnDate)
                .ToListAsync();

            var returnItems = returns.Select(r => new ReturnDeleteItem
            {
                ReturnId = r.Id,
                ReturnInvoiceNumber = r.ReturnInvoiceNumber,
                OriginalInvoiceNumber = r.OriginalInvoiceNumber,
                ProductId = r.ProductId,
                ProductName = r.Product?.Name ?? "غير معروف",
                ReturnedQuantity = r.ReturnedQuantity,
                ReturnReason = r.ReturnReason,
                ReturnDate = r.ReturnDate,
                CreatedBy = r.CreatedBy,
                IsSelected = false
            }).ToList();

            ViewBag.ReturnItems = returnItems;
            return View();
        }

        // POST: Return/MultiDelete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MultiDelete(MultiDeleteReturnRequest request)
        {
            try
            {
                if (!ModelState.IsValid || !request.ReturnIds.Any())
                {
                    TempData["ErrorMessage"] = "يجب اختيار مرتجع واحد على الأقل للحذف";
                    return RedirectToAction(nameof(MultiDelete));
                }

                var returns = await _context.ReturnTrackings
                    .Include(r => r.Product)
                    .Where(r => request.ReturnIds.Contains(r.Id))
                    .ToListAsync();

                if (!returns.Any())
                {
                    TempData["ErrorMessage"] = "لم يتم العثور على المرتجعات المحددة";
                    return RedirectToAction(nameof(MultiDelete));
                }

                int deletedCount = 0;
                int inventoryUpdated = 0;
                var errorMessages = new List<string>();

                foreach (var returnTracking in returns)
                {
                    try
                    {
                        // إرجاع المخزون إلى وضعه الطبيعي (عكس الإرجاع)
                        var product = await _context.Products.FindAsync(returnTracking.ProductId);
                        if (product != null)
                        {
                            product.Quantity -= returnTracking.ReturnedQuantity;
                            inventoryUpdated++;
                        }

                        // حذف فاتورة الإرجاع إن وجدت
                        var returnInvoice = await _context.Invoices
                            .FirstOrDefaultAsync(i => i.InvoiceNumber == returnTracking.ReturnInvoiceNumber);
                        if (returnInvoice != null)
                        {
                            _context.Invoices.Remove(returnInvoice);
                        }

                        // حذف المعاملات المرتبطة
                        var customerTransactions = await _context.CustomerTransactions
                            .Where(ct => ct.Notes != null && ct.Notes.Contains(returnTracking.ReturnInvoiceNumber))
                            .ToListAsync();
                        if (customerTransactions.Any())
                        {
                            _context.CustomerTransactions.RemoveRange(customerTransactions);
                        }

                        _context.ReturnTrackings.Remove(returnTracking);
                        deletedCount++;
                    }
                    catch (Exception ex)
                    {
                        errorMessages.Add($"خطأ في حذف المرتجع {returnTracking.ReturnInvoiceNumber}: {ex.Message}");
                    }
                }

                await _context.SaveChangesAsync();

                // Log activity
                var activityLog = new ActivityLog
                {
                    Action = "Multi Delete Returns",
                    Details = $"حذف متعدد: {deletedCount} مرتجع، تحديث المخزون: {inventoryUpdated} منتج",
                    UserId = User.Identity?.Name ?? "Unknown",
                    Timestamp = DateTime.Now
                };
                _context.ActivityLogs.Add(activityLog);
                await _context.SaveChangesAsync();

                if (errorMessages.Any())
                {
                    TempData["WarningMessage"] = $"تم حذف {deletedCount} مرتجع بنجاح، لكن حدثت بعض الأخطاء: {string.Join(", ", errorMessages)}";
                }
                else
                {
                    TempData["SuccessMessage"] = $"تم حذف {deletedCount} مرتجع بنجاح وإرجاع المخزون لـ {inventoryUpdated} منتج";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"حدث خطأ في الحذف المتعدد: {ex.Message}";
                return RedirectToAction(nameof(MultiDelete));
            }
        }

        // API: Get return items for multi-delete
        [HttpGet]
        public async Task<IActionResult> GetReturnItems(List<int> returnIds)
        {
            try
            {
                var returns = await _context.ReturnTrackings
                    .Include(r => r.Product)
                    .Where(r => returnIds.Contains(r.Id))
                    .ToListAsync();

                var items = returns.Select(r => new
                {
                    returnId = r.Id,
                    returnInvoiceNumber = r.ReturnInvoiceNumber,
                    originalInvoiceNumber = r.OriginalInvoiceNumber,
                    productId = r.ProductId,
                    productName = r.Product?.Name,
                    returnedQuantity = r.ReturnedQuantity,
                    returnReason = r.ReturnReason,
                    returnDate = r.ReturnDate.ToString("yyyy-MM-dd HH:mm"),
                    createdBy = r.CreatedBy
                }).ToList();

                return Json(new { success = true, items });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // GET: Return/MultiReturn
        public IActionResult MultiReturn()
        {
            return View();
        }

        // POST: Return/MultiReturn
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MultiReturn(MultiReturnRequest request)
        {
            try
            {
                if (!ModelState.IsValid || !request.Items.Any())
                {
                    TempData["ErrorMessage"] = "يجب إضافة منتج واحد على الأقل للإرجاع";
                    return RedirectToAction(nameof(MultiReturn));
                }

                // Verify original invoice exists
                var originalInvoice = await _context.Invoices
                    .Include(i => i.Customer)
                    .Include(i => i.Items)
                    .ThenInclude(ii => ii.Product)
                    .FirstOrDefaultAsync(i => i.InvoiceNumber == request.OriginalInvoiceNumber);

                if (originalInvoice == null)
                {
                    TempData["ErrorMessage"] = "الفاتورة الأصلية غير موجودة";
                    return RedirectToAction(nameof(MultiReturn));
                }

                // Generate return invoice number
                var returnInvoiceNumber = $"RTN-{DateTime.Now:yyyyMMddHHmmss}";

                // Create return invoice
                var returnInvoice = new Invoice
                {
                    CustomerId = originalInvoice.CustomerId,
                    InvoiceNumber = returnInvoiceNumber,
                    OrderNumber = $"RTN-{originalInvoice.OrderNumber}",
                    InvoiceDate = DateTime.Now,
                    Type = InvoiceType.Return,
                    Status = InvoiceStatus.Paid,
                    TotalAmount = 0, // سيتم حسابه
                    AmountPaid = 0,
                    RemainingAmount = 0,
                    OriginalInvoiceNumber = request.OriginalInvoiceNumber,
                    ReturnReason = request.ReturnReason,
                    CashierName = User.Identity?.Name ?? "نظام",
                    CreatedAt = DateTime.Now,
                    Notes = $"إرجاع متعدد من فاتورة {request.OriginalInvoiceNumber}"
                };

                decimal totalReturnAmount = 0;
                int totalItemsProcessed = 0;

                foreach (var item in request.Items)
                {
                    // Find product
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product == null) continue;

                    // Find original invoice item to get actual price paid
                    var originalItem = originalInvoice.Items.FirstOrDefault(ii => ii.ProductId == item.ProductId);
                    if (originalItem == null) continue;

                    var actualPricePaid = originalItem.UnitPrice;

                    // Check if quantity is valid
                    var alreadyReturned = await _context.ReturnTrackings
                        .Where(r => r.OriginalInvoiceNumber == request.OriginalInvoiceNumber && r.ProductId == item.ProductId)
                        .SumAsync(r => r.ReturnedQuantity);

                    var availableForReturn = originalItem.Quantity - alreadyReturned;
                    if (item.ReturnedQuantity > availableForReturn)
                    {
                        TempData["ErrorMessage"] = $"الكمية المطلوبة للإرجاع ({item.ReturnedQuantity}) أكبر من المتاحة ({availableForReturn}) للمنتج {product.Name}";
                        return RedirectToAction(nameof(MultiReturn));
                    }

                    // Create return tracking
                    var returnTracking = new ReturnTracking
                    {
                        OriginalInvoiceNumber = request.OriginalInvoiceNumber,
                        ReturnInvoiceNumber = returnInvoiceNumber,
                        ProductId = item.ProductId,
                        ReturnedQuantity = item.ReturnedQuantity,
                        ReturnReason = item.ReturnReason ?? request.ReturnReason,
                        ReturnDate = DateTime.Now,
                        CreatedBy = User.Identity?.Name ?? "Unknown",
                        Notes = item.Notes
                    };
                    _context.ReturnTrackings.Add(returnTracking);

                    // Update product quantity
                    product.Quantity += item.ReturnedQuantity;

                    // Create invoice item
                    var returnItem = new InvoiceItem
                    {
                        Invoice = returnInvoice,
                        ProductId = item.ProductId,
                        Quantity = item.ReturnedQuantity,
                        UnitPrice = actualPricePaid,
                        TotalPrice = -actualPricePaid * item.ReturnedQuantity,
                        CreatedAt = DateTime.Now,
                        Notes = "منتج مرتجع"
                    };
                    _context.InvoiceItems.Add(returnItem);

                    // Create customer transaction
                    var customerTransaction = new CustomerTransaction
                    {
                        CustomerId = originalInvoice.CustomerId,
                        ProductId = item.ProductId,
                        Quantity = -item.ReturnedQuantity,
                        Price = actualPricePaid,
                        TotalPrice = -actualPricePaid * item.ReturnedQuantity,
                        AmountPaid = 0,
                        Date = DateTime.Now,
                        Notes = $"إرجاع متعدد - فاتورة {returnInvoiceNumber}"
                    };
                    _context.CustomerTransactions.Add(customerTransaction);

                    totalReturnAmount += actualPricePaid * item.ReturnedQuantity;
                    totalItemsProcessed++;
                }

                if (totalItemsProcessed == 0)
                {
                    TempData["ErrorMessage"] = "لم يتم معالجة أي منتج";
                    return RedirectToAction(nameof(MultiReturn));
                }

                returnInvoice.TotalAmount = -totalReturnAmount;
                returnInvoice.AmountPaid = -totalReturnAmount;
                _context.Invoices.Add(returnInvoice);

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"تم إرجاع {totalItemsProcessed} منتج بنجاح! رقم فاتورة الإرجاع: {returnInvoiceNumber}";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"حدث خطأ في الإرجاع المتعدد: {ex.Message}";
                return RedirectToAction(nameof(MultiReturn));
            }
        }

        // API: Search invoice for multi-return
        [HttpGet]
        public async Task<IActionResult> SearchInvoiceForReturn(string invoiceNumber)
        {
            try
            {
                var invoice = await _context.Invoices
                    .Include(i => i.Customer)
                    .Include(i => i.Items)
                    .ThenInclude(ii => ii.Product)
                    .FirstOrDefaultAsync(i => i.InvoiceNumber == invoiceNumber && i.Type == InvoiceType.Sale);

                if (invoice == null)
                {
                    return Json(new { success = false, message = "الفاتورة غير موجودة أو ليست فاتورة بيع" });
                }

                // Get already returned quantities
                var returnedQuantities = await _context.ReturnTrackings
                    .Where(r => r.OriginalInvoiceNumber == invoiceNumber)
                    .GroupBy(r => r.ProductId)
                    .Select(g => new { ProductId = g.Key, TotalReturned = g.Sum(r => r.ReturnedQuantity) })
                    .ToListAsync();

                var items = invoice.Items.Select(item =>
                {
                    var returned = returnedQuantities.FirstOrDefault(r => r.ProductId == item.ProductId)?.TotalReturned ?? 0;
                    var available = item.Quantity - returned;

                    return new
                    {
                        productId = item.ProductId,
                        productName = item.Product?.Name,
                        originalQuantity = item.Quantity,
                        returnedQuantity = returned,
                        availableForReturn = available,
                        unitPrice = item.UnitPrice,
                        totalPrice = item.TotalPrice
                    };
                }).Where(i => i.availableForReturn > 0).ToList();

                if (!items.Any())
                {
                    return Json(new { success = false, message = "جميع منتجات هذه الفاتورة تم إرجاعها بالفعل" });
                }

                return Json(new
                {
                    success = true,
                    invoice = new
                    {
                        invoiceNumber = invoice.InvoiceNumber,
                        customerName = invoice.Customer?.Name,
                        invoiceDate = invoice.InvoiceDate.ToString("yyyy-MM-dd"),
                        totalAmount = invoice.TotalAmount,
                        items
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"خطأ: {ex.Message}" });
            }
        }
    }

    // Return request model
    public class ReturnRequest
    {
        [Required(ErrorMessage = "رقم الفاتورة الأصلية مطلوب")]
        public string OriginalInvoiceNumber { get; set; } = "";

        [Required(ErrorMessage = "رقم فاتورة الإرجاع مطلوب")]
        public string ReturnInvoiceNumber { get; set; } = "";

        [Required(ErrorMessage = "المنتج مطلوب")]
        public int ProductId { get; set; }

        [Required(ErrorMessage = "الكمية مطلوبة")]
        [Range(1, int.MaxValue, ErrorMessage = "الكمية يجب أن تكون أكبر من صفر")]
        public int ReturnedQuantity { get; set; }

        public string? ReturnReason { get; set; }
        public string? Notes { get; set; }
    }

    // Multi-return request model
    public class MultiReturnRequest
    {
        [Required(ErrorMessage = "رقم الفاتورة الأصلية مطلوب")]
        public string OriginalInvoiceNumber { get; set; } = "";

        public string? ReturnReason { get; set; }

        [Required(ErrorMessage = "يجب إضافة منتج واحد على الأقل")]
        public List<MultiReturnItem> Items { get; set; } = new List<MultiReturnItem>();
    }

    public class MultiReturnItem
    {
        [Required]
        public int ProductId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "الكمية يجب أن تكون أكبر من صفر")]
        public int ReturnedQuantity { get; set; }

        public string? ReturnReason { get; set; }
        public string? Notes { get; set; }
    }
}
