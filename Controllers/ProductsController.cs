using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using PesticideShop.Data;
using PesticideShop.Models;
using PesticideShop.Services;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System;

namespace PesticideShop.Controllers
{
    [Authorize]
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IActivityService _activityService;

        public ProductsController(ApplicationDbContext context, IActivityService activityService)
        {
            _context = context;
            _activityService = activityService;
        }

        // GET: Products
        public async Task<IActionResult> Index()
        {
            // Force reload from database to get latest quantities
            _context.ChangeTracker.Clear(); // Clear any cached entities
            
            var products = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.CustomerTransactions)
                .AsNoTracking() // Force fresh data from DB
                .ToListAsync();
                
            // Log for debugging
            await _activityService.LogActivityAsync(
                User.Identity?.Name ?? "Unknown",
                "ViewProducts",
                $"Loaded {products.Count} products from database",
                $"Timestamp: {DateTime.Now}",
                "ProductView"
            );
            
            return View(products);
        }
        
        // API: Get real-time product quantity
        [HttpGet]
        public async Task<IActionResult> GetProductQuantity(int productId)
        {
            try
            {
                // Force fresh data from database
                _context.ChangeTracker.Clear();
                
                var product = await _context.Products
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == productId);
                    
                if (product == null)
                {
                    return Json(new { success = false, message = "المنتج غير موجود" });
                }
                
                await _activityService.LogActivityAsync(
                    User.Identity?.Name ?? "Unknown",
                    "GetProductQuantity",
                    $"Product: {product.Name}, Current Quantity: {product.Quantity}",
                    $"ProductId: {productId}",
                    "ProductView"
                );
                
                return Json(new 
                { 
                    success = true, 
                    productId = product.Id,
                    productName = product.Name,
                    quantity = product.Quantity,
                    lastUpdated = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"خطأ: {ex.Message}" });
            }
        }
        
        // API: Refresh all products data
        [HttpGet]
        public async Task<IActionResult> RefreshProductsData()
        {
            try
            {
                // Force clear any cached data
                _context.ChangeTracker.Clear();
                
                var products = await _context.Products
                    .AsNoTracking()
                    .Select(p => new 
                    {
                        id = p.Id,
                        name = p.Name,
                        quantity = p.Quantity,
                        price = p.Price
                    })
                    .ToListAsync();
                    
                await _activityService.LogActivityAsync(
                    User.Identity?.Name ?? "Unknown",
                    "RefreshProductsData",
                    $"Refreshed data for {products.Count} products",
                    $"Timestamp: {DateTime.Now}",
                    "ProductView"
                );
                
                return Json(new 
                { 
                    success = true, 
                    products = products,
                    timestamp = DateTime.Now,
                    message = $"تم تحديث بيانات {products.Count} منتج"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"خطأ في التحديث: {ex.Message}" });
            }
        }

        // GET: Products/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        // GET: Products/Create
        public async Task<IActionResult> Create()
        {
            var categories = await _context.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();
            
            ViewData["Categories"] = new SelectList(categories, "Id", "Name");
            return View();
        }

        // POST: Products/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Quantity,Price,CartonPrice,CategoryId,Color,Size,QRCode,Notes")] Product product)
        {
            if (ModelState.IsValid)
            {
                product.DateAdded = DateTime.Now;
                
                // حساب سعر التكلفة من سعر الجملة
                if (product.CartonPrice.HasValue && product.Quantity > 0)
                {
                    product.CostPrice = product.CartonPrice.Value / product.Quantity;
                }
                else if (product.Quantity == 0)
                {
                    // إذا كانت الكمية 0، سعر التكلفة يكون 0
                    product.CostPrice = 0;
                }
                
                _context.Add(product);
                await _context.SaveChangesAsync();
                
                // Log activity
                await _activityService.LogActivityAsync("create", "product", product.Name, $"تم إنشاء منتج جديد: {product.Name} - {product.Price:N2} جنيه");
                
                TempData["SuccessMessage"] = $"Product '{product.Name}' created successfully!";
                return RedirectToAction(nameof(Index));
            }
            else
            {
                // Log validation errors for debugging
                foreach (var modelState in ModelState.Values)
                {
                    foreach (var error in modelState.Errors)
                    {
                        TempData["ErrorMessage"] = $"Validation Error: {error.ErrorMessage}";
                    }
                }
            }

            // Reload categories for the view
            var categories = await _context.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();
            ViewData["Categories"] = new SelectList(categories, "Id", "Name");
            
            return View(product);
        }

        // GET: Products/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id);
                
            if (product == null)
            {
                return NotFound();
            }

            var categories = await _context.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();
            
            ViewData["Categories"] = new SelectList(categories, "Id", "Name", product.CategoryId);
            
            // الحصول على المنتج السابق والتالي للتنقل
            var allProductIds = await _context.Products
                .OrderBy(p => p.Id)
                .Select(p => p.Id)
                .ToListAsync();
            
            var currentIndex = allProductIds.IndexOf(id.Value);
            
            // المنتج السابق
            ViewBag.PreviousProductId = currentIndex > 0 
                ? allProductIds[currentIndex - 1] 
                : (int?)null;
            
            // المنتج التالي
            ViewBag.NextProductId = currentIndex < allProductIds.Count - 1 
                ? allProductIds[currentIndex + 1] 
                : (int?)null;
            
            // معلومات إضافية للعرض
            ViewBag.CurrentPosition = currentIndex + 1;
            ViewBag.TotalProducts = allProductIds.Count;
            
            return View(product);
        }

        // POST: Products/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Quantity,Price,CartonPrice,CategoryId,Color,Size,QRCode,Notes")] Product product)
        {
            if (id != product.Id)
            {
                return NotFound();
            }

            // Debug logging for Edit
            await _activityService.LogActivityAsync(
                User.Identity?.Name ?? "Unknown",
                "DEBUG_EditAttempt",
                $"Edit attempt for product ID: {id}, Name: {product.Name}, Valid: {ModelState.IsValid}",
                $"Errors: {string.Join(", ", ModelState.SelectMany(x => x.Value.Errors.Select(e => e.ErrorMessage)))}",
                "Debug"
            );

            if (ModelState.IsValid)
            {
                try
                {
                    var existingProduct = await _context.Products.FindAsync(id);
                    if (existingProduct == null)
                    {
                        await _activityService.LogActivityAsync(
                            User.Identity?.Name ?? "Unknown",
                            "DEBUG_EditError",
                            "Product not found during edit",
                            $"ProductId: {id}",
                            "Debug"
                        );
                        return NotFound();
                    }

                    // Log what will be updated
                    await _activityService.LogActivityAsync(
                        User.Identity?.Name ?? "Unknown",
                        "DEBUG_EditUpdate",
                        $"Updating product: {existingProduct.Name} -> {product.Name}, Qty: {existingProduct.Quantity} -> {product.Quantity}",
                        $"Price: {existingProduct.Price} -> {product.Price}",
                        "Debug"
                    );

                    // Preserve the original date added
                    product.DateAdded = existingProduct.DateAdded;
                    
                    // حساب سعر التكلفة من سعر الجملة
                    if (product.CartonPrice.HasValue && product.Quantity > 0)
                    {
                        product.CostPrice = product.CartonPrice.Value / product.Quantity;
                    }
                    else if (product.Quantity == 0)
                    {
                        // إذا كانت الكمية 0، سعر التكلفة يكون 0
                        product.CostPrice = 0;
                    }
                    else
                    {
                        product.CostPrice = 0; // إعادة تعيين إلى صفر إذا لم يكن هناك سعر جملة
                    }

                    _context.Entry(existingProduct).CurrentValues.SetValues(product);
                    var saveResult = await _context.SaveChangesAsync();

                    // Log successful update
                    await _activityService.LogActivityAsync(
                        User.Identity?.Name ?? "Unknown",
                        "DEBUG_EditSuccess",
                        $"Product updated successfully. Records affected: {saveResult}",
                        $"ProductId: {id}, Name: {product.Name}",
                        "Debug"
                    );

                    // Log activity
                    await _activityService.LogActivityAsync("edit", "product", product.Name, $"تم تعديل المنتج: {product.Name}");

                    // إعادة حساب الجرد اليومي إذا تم تحديث سعر الجملة
                    if (existingProduct.CartonPrice != product.CartonPrice)
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

                            // إعادة حساب التكلفة والربح
                            inventory.TotalCost = transactions.Sum(t => (t.Product?.CostPrice ?? 0) * Math.Abs(t.Quantity));
                            
                            var hasValidCost = transactions.Any(t => t.Product?.CostPrice > 0);
                            if (hasValidCost)
                            {
                                inventory.NetProfit = inventory.TotalSales - inventory.TotalCost;
                            }
                            else
                            {
                                inventory.NetProfit = 0;
                            }
                        }
                        
                        await _context.SaveChangesAsync();
                        
                        // إعادة حساب الجرد السنوي أيضاً
                        var yearTransactions = await _context.CustomerTransactions
                            .Include(ct => ct.Product)
                            .Include(ct => ct.Customer)
                            .Where(ct => ct.Date.Year == DateTime.Now.Year)
                            .ToListAsync();
                        
                        // إعادة حساب التكلفة والربح للجرد السنوي
                        var yearCost = 0m;
                        var hasValidCostForYear = false;
                        foreach (var transaction in yearTransactions)
                        {
                            if (transaction.Product != null && transaction.Product.CostPrice > 0)
                            {
                                yearCost += transaction.Product.CostPrice * Math.Abs(transaction.Quantity);
                                hasValidCostForYear = true;
                            }
                        }
                        
                        // يمكن إضافة منطق إعادة حساب الجرد السنوي هنا إذا كان مطلوباً
                    }

                    TempData["SuccessMessage"] = $"Product '{product.Name}' updated successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProductExists(product.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            // Reload categories for the view
            var categories = await _context.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();
            ViewData["Categories"] = new SelectList(categories, "Id", "Name", product.CategoryId);
            
            return View(product);
        }

        // GET: Products/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products.FirstOrDefaultAsync(m => m.Id == id);
            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        // POST: Products/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var product = await _context.Products
                    .Include(p => p.CustomerTransactions)
                    .FirstOrDefaultAsync(p => p.Id == id);
                    
                if (product == null)
                {
                    return NotFound();
                }

                // حفظ اسم المنتج قبل الحذف
                var productName = product.Name;
                var transactionsCount = product.CustomerTransactions?.Count ?? 0;

                // حذف جميع السجلات المرتبطة بالمنتج في DailyProductSummaries أولاً
                var productSummaries = await _context.DailyProductSummaries
                    .Where(dps => dps.ProductId == id)
                    .ToListAsync();
                
                if (productSummaries.Any())
                {
                    _context.DailyProductSummaries.RemoveRange(productSummaries);
                    await _context.SaveChangesAsync(); // حفظ التغييرات
                }

                // حذف جميع السجلات المرتبطة بالمنتج في DailySaleTransactions
                var dailySaleTransactions = await _context.DailySaleTransactions
                    .Where(dst => dst.ProductId == id)
                    .ToListAsync();
                
                if (dailySaleTransactions.Any())
                {
                    _context.DailySaleTransactions.RemoveRange(dailySaleTransactions);
                    await _context.SaveChangesAsync(); // حفظ التغييرات
                }

                // حذف جميع عناصر الفواتير المرتبطة بالمنتج
                var invoiceItems = await _context.InvoiceItems
                    .Where(ii => ii.ProductId == id)
                    .ToListAsync();
                
                if (invoiceItems.Any())
                {
                    _context.InvoiceItems.RemoveRange(invoiceItems);
                    await _context.SaveChangesAsync(); // حفظ التغييرات
                }

                // حذف جميع سجلات الاستبدال المرتبطة بالمنتج
                var exchangeTrackings = await _context.ExchangeTrackings
                    .Where(et => et.OldProductId == id || et.NewProductId == id)
                    .ToListAsync();
                
                if (exchangeTrackings.Any())
                {
                    _context.ExchangeTrackings.RemoveRange(exchangeTrackings);
                    await _context.SaveChangesAsync(); // حفظ التغييرات
                }

                // حذف جميع معاملات العملاء المرتبطة بالمنتج
                if (product.CustomerTransactions != null && product.CustomerTransactions.Any())
                {
                    _context.CustomerTransactions.RemoveRange(product.CustomerTransactions);
                    await _context.SaveChangesAsync(); // حفظ التغييرات
                }

                // أخيراً، حذف المنتج نفسه
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
                
                var message = $"تم حذف المنتج '{productName}' بنجاح!";
                if (transactionsCount > 0)
                {
                    message += $" (تم حذف {transactionsCount} معاملة مرتبطة)";
                }
                TempData["SuccessMessage"] = message;
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"حدث خطأ أثناء حذف المنتج: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.Id == id);
        }

        // POST: Products/RecalculateCostPrices - إعادة حساب أسعار التكلفة لجميع المنتجات
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecalculateCostPrices()
        {
            try
            {
                var products = await _context.Products.ToListAsync();
                int updatedCount = 0;
                
                foreach (var product in products)
                {
                    if (product.CartonPrice.HasValue && product.Quantity > 0)
                    {
                        product.CostPrice = product.CartonPrice.Value / product.Quantity;
                        updatedCount++;
                    }
                    else
                    {
                        product.CostPrice = 0;
                    }
                }
                
                await _context.SaveChangesAsync();
                
                // إعادة حساب الجرد اليومي
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

                    // إعادة حساب التكلفة والربح
                    inventory.TotalCost = transactions.Sum(t => (t.Product?.CostPrice ?? 0) * Math.Abs(t.Quantity));
                    
                    var hasValidCost = transactions.Any(t => t.Product?.CostPrice > 0);
                    if (hasValidCost)
                    {
                        inventory.NetProfit = inventory.TotalSales - inventory.TotalCost;
                    }
                    else
                    {
                        inventory.NetProfit = 0;
                    }
                }
                
                await _context.SaveChangesAsync();
                
                TempData["SuccessMessage"] = $"تم إعادة حساب أسعار التكلفة لـ {updatedCount} منتج وإعادة حساب الجرد اليومي!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"حدث خطأ أثناء إعادة حساب الأسعار: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }
    }
} 