using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using PesticideShop.Data;
using PesticideShop.Models;
using PesticideShop.Services;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace PesticideShop.Controllers
{
    [Authorize]
    public class CustomersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IActivityService _activityService;
        private readonly ILogger<CustomersController> _logger;

        public CustomersController(ApplicationDbContext context, IActivityService activityService, ILogger<CustomersController> logger)
        {
            _context = context;
            _activityService = activityService;
            _logger = logger;
        }

        // GET: Customers
        public async Task<IActionResult> Index(string searchTerm, string searchType)
        {
            var query = _context.Customers
                .Include(c => c.Transactions)
                .ThenInclude(t => t.Product)
                .AsQueryable();

            // Apply search filter
            if (!string.IsNullOrEmpty(searchTerm))
            {
                searchTerm = searchTerm.Trim().ToLower();
                
                switch (searchType?.ToLower())
                {
                    case "name":
                        query = query.Where(c => c.Name.ToLower().Contains(searchTerm));
                        break;
                    case "phone":
                        query = query.Where(c => c.PhoneNumber.Contains(searchTerm));
                        break;
                    default: // "all"
                        query = query.Where(c => 
                            c.Name.ToLower().Contains(searchTerm) || 
                            c.PhoneNumber.Contains(searchTerm));
                        break;
                }
            }

            var customers = await query.ToListAsync();

            // Calculate remaining balance for each customer
            foreach (var customer in customers)
            {
                var totalPurchases = customer.Transactions?.Sum(t => t.TotalPrice) ?? 0;
                var totalPaid = customer.Transactions?.Sum(t => t.AmountPaid) ?? 0;
                ViewData[$"Balance_{customer.Id}"] = totalPurchases - totalPaid;
            }

            // Pass search parameters to view
            ViewData["SearchTerm"] = searchTerm;
            ViewData["SearchType"] = searchType;

            return View(customers);
        }

        // GET: Customers/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers
                .Include(c => c.Transactions)
                .ThenInclude(t => t.Product)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (customer == null)
            {
                return NotFound();
            }

            // Calculate balance
            var totalPurchases = customer.Transactions?.Sum(t => t.TotalPrice) ?? 0;
            var totalPaid = customer.Transactions?.Sum(t => t.AmountPaid) ?? 0;
            ViewData["RemainingBalance"] = totalPurchases - totalPaid;

            // Get related invoices for customer transactions
            var customerTransactions = customer.Transactions?.ToList() ?? new List<CustomerTransaction>();
            var invoices = await _context.Invoices
                .Include(i => i.Items)
                .Where(i => i.CustomerId == customer.Id)
                .ToListAsync();

            ViewData["Invoices"] = invoices;

            return View(customer);
        }

        // GET: Customers/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Customers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,PhoneNumber,AdditionalPhone,Email,Governorate,District,DetailedAddress")] Customer customer)
        {
            if (ModelState.IsValid)
            {
                _context.Add(customer);
                await _context.SaveChangesAsync();
                
                // Log activity
                await _activityService.LogActivityAsync("create", "customer", customer.Name, $"تم إنشاء عميل جديد: {customer.Name}");
                
                TempData["SuccessMessage"] = $"Customer '{customer.Name}' created successfully!";
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
            return View(customer);
        }

        // GET: Customers/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
            {
                return NotFound();
            }
            return View(customer);
        }

        // POST: Customers/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,PhoneNumber,AdditionalPhone,Email,Governorate,District,DetailedAddress")] Customer customer)
        {
            if (id != customer.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(customer);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CustomerExists(customer.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                TempData["SuccessMessage"] = $"Customer '{customer.Name}' updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            return View(customer);
        }

        // GET: Customers/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers
                .FirstOrDefaultAsync(m => m.Id == id);
            if (customer == null)
            {
                return NotFound();
            }

            return View(customer);
        }

        // POST: Customers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var customer = await _context.Customers
                    .Include(c => c.Transactions)
                    .FirstOrDefaultAsync(c => c.Id == id);
                    
                if (customer == null)
                {
                    return NotFound();
                }

                // حفظ اسم العميل وعدد المعاملات قبل الحذف
                var customerName = customer.Name;
                var transactionsCount = customer.Transactions?.Count ?? 0;

                // حذف جميع معاملات العميل أولاً
                if (customer.Transactions != null && customer.Transactions.Any())
                {
                    _context.CustomerTransactions.RemoveRange(customer.Transactions);
                    await _context.SaveChangesAsync(); // حفظ التغييرات
                }

                // حذف جميع السجلات المرتبطة بالعميل في DailyCustomerSummaries
                var customerSummaries = await _context.DailyCustomerSummaries
                    .Where(dcs => dcs.CustomerId == id)
                    .ToListAsync();
                
                if (customerSummaries.Any())
                {
                    _context.DailyCustomerSummaries.RemoveRange(customerSummaries);
                    await _context.SaveChangesAsync(); // حفظ التغييرات
                }

                // حذف جميع السجلات المرتبطة بالعميل في DailySaleTransactions
                var dailySaleTransactions = await _context.DailySaleTransactions
                    .Where(dst => dst.CustomerId == id)
                    .ToListAsync();
                
                if (dailySaleTransactions.Any())
                {
                    _context.DailySaleTransactions.RemoveRange(dailySaleTransactions);
                    await _context.SaveChangesAsync(); // حفظ التغييرات
                }

                // حذف جميع الفواتير المرتبطة بالعميل
                var customerInvoices = await _context.Invoices
                    .Include(i => i.Items)
                    .Where(i => i.CustomerId == id)
                    .ToListAsync();
                
                if (customerInvoices.Any())
                {
                    // حذف عناصر الفواتير أولاً
                    foreach (var invoice in customerInvoices)
                    {
                        if (invoice.Items != null && invoice.Items.Any())
                        {
                            _context.InvoiceItems.RemoveRange(invoice.Items);
                        }
                    }
                    await _context.SaveChangesAsync(); // حفظ التغييرات
                    
                    // حذف الفواتير
                    _context.Invoices.RemoveRange(customerInvoices);
                    await _context.SaveChangesAsync(); // حفظ التغييرات
                }

                // حذف العميل
                _context.Customers.Remove(customer);
                await _context.SaveChangesAsync();
                
                var message = $"تم حذف العميل '{customerName}' بنجاح!";
                if (transactionsCount > 0)
                {
                    message += $" (تم حذف {transactionsCount} معاملة مرتبطة)";
                }
                TempData["SuccessMessage"] = message;
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"حدث خطأ أثناء حذف العميل: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Customers/AddTransaction/5
        public async Task<IActionResult> AddTransaction(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
            {
                return NotFound();
            }

            // Create a new transaction model with default values
            var transaction = new CustomerTransaction
            {
                CustomerId = id.Value,
                Date = DateTime.Now
            };

            ViewData["CustomerId"] = id;
            ViewData["CustomerName"] = customer.Name;
            ViewData["CustomerPhone"] = customer.PhoneNumber;
            ViewData["ProductId"] = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                _context.Products.Select(p => new { p.Id, DisplayName = $"{p.Name} - ${p.Price:N2} (Qty: {p.Quantity})" }), 
                "Id", "DisplayName");
            return View(transaction);
        }

        // POST: Customers/AddTransaction/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddTransaction(int id)
        {
            var transaction = new CustomerTransaction();
            
            // Manually bind the form data
            if (Request.Form.ContainsKey("ProductId") && int.TryParse(Request.Form["ProductId"], out int productId))
                transaction.ProductId = productId;
            
            if (Request.Form.ContainsKey("Quantity") && int.TryParse(Request.Form["Quantity"], out int quantity))
                transaction.Quantity = quantity;
            
            if (Request.Form.ContainsKey("TotalPrice") && decimal.TryParse(Request.Form["TotalPrice"], out decimal totalPrice))
                transaction.TotalPrice = totalPrice;
            
            if (Request.Form.ContainsKey("AmountPaid") && decimal.TryParse(Request.Form["AmountPaid"], out decimal amountPaid))
                transaction.AmountPaid = amountPaid;
            
            if (Request.Form.ContainsKey("Date") && DateTime.TryParse(Request.Form["Date"], out DateTime date))
                transaction.Date = date;
            
            // Also try to bind CustomerId from form
            if (Request.Form.ContainsKey("CustomerId") && int.TryParse(Request.Form["CustomerId"], out int customerId))
                transaction.CustomerId = customerId;
            else
                transaction.CustomerId = id;
            
            // Log the received data for debugging
            System.Diagnostics.Debug.WriteLine($"Received data: ProductId={transaction.ProductId}, Quantity={transaction.Quantity}, TotalPrice={transaction.TotalPrice}, AmountPaid={transaction.AmountPaid}, Date={transaction.Date}, CustomerId={transaction.CustomerId}");
            
            // Manual validation
            bool isValid = true;
            var errors = new List<string>();
            
            if (transaction.ProductId <= 0)
            {
                errors.Add("يجب اختيار منتج");
                isValid = false;
            }
            
            if (transaction.Quantity <= 0)
            {
                errors.Add("يجب أن تكون الكمية أكبر من صفر");
                isValid = false;
            }
            
            if (transaction.TotalPrice <= 0)
            {
                errors.Add("يجب أن يكون السعر الإجمالي أكبر من صفر");
                isValid = false;
            }
            
            if (transaction.AmountPaid < 0)
            {
                errors.Add("يجب أن يكون المبلغ المدفوع صحيح");
                isValid = false;
            }
            
            if (transaction.Date == default)
            {
                errors.Add("يجب إدخال تاريخ صحيح");
                isValid = false;
            }
            
            if (isValid)
            {
                // Check if product has enough quantity
                var product = await _context.Products.FindAsync(transaction.ProductId);
                if (product == null)
                {
                    TempData["ErrorMessage"] = "المنتج غير موجود!";
                    return RedirectToAction(nameof(Details), new { id = id });
                }

                // التحقق من صحة الكمية (السماح بالكميات السالبة للاسترجاع)
                if (transaction.Quantity == 0)
                {
                    TempData["ErrorMessage"] = "يجب أن تكون الكمية مختلفة عن صفر!";
                    return RedirectToAction(nameof(Details), new { id = id });
                }

                // التحقق من صحة السعر
                if (transaction.TotalPrice <= 0)
                {
                    TempData["ErrorMessage"] = "يجب أن يكون السعر الإجمالي أكبر من صفر!";
                    return RedirectToAction(nameof(Details), new { id = id });
                }

                // التحقق من أن المبلغ المدفوع لا يتجاوز السعر الإجمالي
                if (transaction.AmountPaid > transaction.TotalPrice)
                {
                    TempData["ErrorMessage"] = "المبلغ المدفوع لا يمكن أن يتجاوز السعر الإجمالي!";
                    return RedirectToAction(nameof(Details), new { id = id });
                }

                // التحقق من توفر الكمية المطلوبة (فقط للبيع)
                if (transaction.Quantity > 0 && product.Quantity < transaction.Quantity)
                {
                    TempData["ErrorMessage"] = $"الكمية المطلوبة غير متوفرة! الكمية المتاحة: {product.Quantity} قطعة";
                    return RedirectToAction(nameof(Details), new { id = id });
                }

                try
                {
                    // تحديث كمية المنتج
                    var oldQuantity = product.Quantity;
                    
                    if (transaction.Quantity > 0)
                    {
                        // بيع: تقليل المخزون
                        product.Quantity -= transaction.Quantity;
                        
                        await _activityService.LogActivityAsync(
                            "System",
                            "InventoryUpdate",
                            $"CUSTOMER SALE: Product: {product.Name}, Qty: -{transaction.Quantity}, Old: {oldQuantity}, New: {product.Quantity}",
                            $"Customer ID: {id}, Product ID: {product.Id}",
                            "Inventory"
                        );
                    }
                    else if (transaction.Quantity < 0)
                    {
                        // إرجاع: زيادة المخزون
                        var returnQuantity = Math.Abs(transaction.Quantity);
                        product.Quantity += returnQuantity;
                        
                        await _activityService.LogActivityAsync(
                            "System",
                            "InventoryUpdate",
                            $"CUSTOMER RETURN: Product: {product.Name}, Qty: +{returnQuantity}, Old: {oldQuantity}, New: {product.Quantity}",
                            $"Customer ID: {id}, Product ID: {product.Id}",
                            "Inventory"
                        );
                    }
                    
                    _context.Products.Update(product);
                    
                    transaction.CustomerId = id;
                    _context.Add(transaction);
                    await _context.SaveChangesAsync();
                    
                    // تحديث الجرد اليومي بعد إضافة المعاملة
                    try
                    {
                        var dailyInventoryService = HttpContext.RequestServices.GetRequiredService<IDailyInventoryService>();
                        await dailyInventoryService.ProcessTransactionAsync(transaction);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"خطأ في تحديث الجرد اليومي بعد إضافة المعاملة {transaction.Id}");
                    }
                    
                    // Get customer name for activity log
                    var customer = await _context.Customers.FindAsync(id);
                    
                    // Log activity
                    await _activityService.LogActivityAsync(
                        "transaction", 
                        "customer", 
                        customer?.Name, 
                        $"معاملة جديدة للعميل {customer?.Name}: {product?.Name} - {transaction.Quantity} قطعة - {transaction.TotalPrice:N2} جنيه"
                    );
                    
                    var operationType = transaction.Quantity > 0 ? "بيع" : "إرجاع";
                    var quantityChange = transaction.Quantity > 0 ? $"خصم {transaction.Quantity}" : $"إضافة {Math.Abs(transaction.Quantity)}";
                    TempData["SuccessMessage"] = $"تم إضافة معاملة {operationType} بنجاح و{quantityChange} قطعة من مخزون {product.Name}!";
                    return RedirectToAction(nameof(Details), new { id = id });
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"حدث خطأ أثناء حفظ المعاملة: {ex.Message}";
                    return RedirectToAction(nameof(Details), new { id = id });
                }
            }
            else
            {
                TempData["ErrorMessage"] = $"خطأ في التحقق: {string.Join(", ", errors)}";
                
                var existingCustomer = await _context.Customers.FindAsync(id);
                ViewData["CustomerId"] = id;
                ViewData["CustomerName"] = existingCustomer?.Name;
                ViewData["CustomerPhone"] = existingCustomer?.PhoneNumber;
                ViewData["ProductId"] = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                    _context.Products.Where(p => p.Quantity > 0).Select(p => new { p.Id, DisplayName = $"{p.Name} - ${p.Price:N2} (Qty: {p.Quantity})" }), 
                    "Id", "DisplayName", transaction.ProductId);
                
                // Ensure the model has the correct CustomerId
                transaction.CustomerId = id;
                
                // Log the error for debugging
                System.Diagnostics.Debug.WriteLine($"Validation errors: {string.Join(", ", errors)}");
                
                return View(transaction);
            }
        }

        // GET: Customers/EditTransaction/5
        public async Task<IActionResult> EditTransaction(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var transaction = await _context.CustomerTransactions
                .Include(t => t.Customer)
                .Include(t => t.Product)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (transaction == null)
            {
                return NotFound();
            }

            ViewData["CustomerId"] = transaction.CustomerId;
            ViewData["CustomerName"] = transaction.Customer?.Name;
            ViewData["ProductId"] = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                _context.Products.Select(p => new { p.Id, DisplayName = $"{p.Name} - {p.Price:N2} جنيه" }), 
                "Id", "DisplayName", transaction.ProductId);
            return View(transaction);
        }

        // POST: Customers/EditTransaction/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTransaction(int id, [Bind("Id,CustomerId,ProductId,Quantity,TotalPrice,AmountPaid,Date,ShippingType")] CustomerTransaction transaction)
        {
            if (id != transaction.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Get the original transaction to compare quantities
                    var originalTransaction = await _context.CustomerTransactions
                        .AsNoTracking()
                        .FirstOrDefaultAsync(t => t.Id == id);
                    
                    if (originalTransaction == null)
                    {
                        return NotFound();
                    }

                    // Get the product to check availability
                    var product = await _context.Products.FindAsync(transaction.ProductId);
                    if (product == null)
                    {
                        TempData["ErrorMessage"] = "المنتج غير موجود!";
                        return RedirectToAction(nameof(Details), new { id = transaction.CustomerId });
                    }

                    // Calculate the difference in quantity
                    var quantityDifference = transaction.Quantity - originalTransaction.Quantity;
                    var oldQuantity = product.Quantity;
                    
                    // تحديث كمية المنتج بناءً على الفرق
                    if (quantityDifference > 0)
                    {
                        // زيادة الكمية (بيع أكثر أو إرجاع أقل)
                        if (product.Quantity < quantityDifference)
                        {
                            TempData["ErrorMessage"] = $"الكمية المطلوبة غير متوفرة! الكمية المتاحة: {product.Quantity} قطعة";
                            return RedirectToAction(nameof(Details), new { id = transaction.CustomerId });
                        }
                        product.Quantity -= quantityDifference;
                    }
                    else if (quantityDifference < 0)
                    {
                        // تقليل الكمية (بيع أقل أو إرجاع أكثر)
                        product.Quantity += Math.Abs(quantityDifference);
                    }
                    
                    _context.Products.Update(product);
                    
                    await _activityService.LogActivityAsync(
                        "System",
                        "TransactionEdit",
                        $"EDITED TRANSACTION: Product: {product.Name}, Qty Change: {quantityDifference}, Old: {oldQuantity}, New: {product.Quantity}",
                        $"Customer ID: {transaction.CustomerId}, Product ID: {product.Id}",
                        "Inventory"
                    );
                    
                    // تسجيل نوع التغيير
                    var changeType = quantityDifference > 0 ? "زيادة في البيع" : "تقليل في البيع أو زيادة في الإرجاع";
                    await _activityService.LogActivityAsync(
                        "System",
                        "TransactionEdit",
                        $"TRANSACTION CHANGE TYPE: {changeType}",
                        $"Product: {product.Name}, Quantity Difference: {quantityDifference}",
                        "Inventory"
                    );

                    _context.Update(transaction);
                    await _context.SaveChangesAsync();
                    
                    // تحديث الجرد اليومي بعد تعديل المعاملة
                    try
                    {
                        var dailyInventoryService = HttpContext.RequestServices.GetRequiredService<IDailyInventoryService>();
                        await dailyInventoryService.RecalculateInventoryAsync(transaction.Date.Date);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"خطأ في تحديث الجرد اليومي بعد تعديل المعاملة {transaction.Id}");
                    }
                    
                    // Get customer and product names for activity log
                    var customer = await _context.Customers.FindAsync(transaction.CustomerId);
                    
                    // Log activity
                    await _activityService.LogActivityAsync(
                        "update", 
                        "customer_transaction", 
                        customer?.Name, 
                        $"تم تحديث معاملة العميل {customer?.Name}: {product?.Name} - {transaction.Quantity} قطعة - {transaction.TotalPrice:N2} جنيه"
                    );
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CustomerTransactionExists(transaction.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                TempData["SuccessMessage"] = "تم تحديث المعاملة بنجاح!";
                return RedirectToAction(nameof(Details), new { id = transaction.CustomerId });
            }

            var existingCustomer = await _context.Customers.FindAsync(transaction.CustomerId);
            ViewData["CustomerId"] = transaction.CustomerId;
            ViewData["CustomerName"] = existingCustomer?.Name;
            ViewData["ProductId"] = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                _context.Products.Select(p => new { p.Id, DisplayName = $"{p.Name} - {p.Price:N2} جنيه" }), 
                "Id", "DisplayName", transaction.ProductId);
            return View(transaction);
        }

        // POST: Customers/DeleteTransaction/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTransaction(int id)
        {
            var transaction = await _context.CustomerTransactions
                .Include(t => t.Customer)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (transaction == null)
            {
                return NotFound();
            }

            var customerId = transaction.CustomerId;
            var customerName = transaction.Customer?.Name;
            
            // Get product and update quantity based on transaction type
            var product = await _context.Products.FindAsync(transaction.ProductId);
            if (product != null)
            {
                var oldQuantity = product.Quantity;
                
                if (transaction.Quantity > 0)
                {
                    // إذا كانت معاملة بيع، نعيد الكمية للمخزون
                    product.Quantity += transaction.Quantity;
                }
                else if (transaction.Quantity < 0)
                {
                    // إذا كانت معاملة إرجاع، نخصم الكمية من المخزون
                    var returnQuantity = Math.Abs(transaction.Quantity);
                    if (product.Quantity >= returnQuantity)
                    {
                        product.Quantity -= returnQuantity;
                    }
                }
                
                _context.Products.Update(product);
                
                await _activityService.LogActivityAsync(
                    "System",
                    "TransactionDeletion",
                    $"DELETED TRANSACTION: Product: {product.Name}, Qty: {transaction.Quantity}, Old: {oldQuantity}, New: {product.Quantity}",
                    $"Customer: {customerName}, Product ID: {product.Id}",
                    "Inventory"
                );
                
                // تسجيل نوع العملية المحذوفة
                var deletedOperationType = transaction.Quantity > 0 ? "بيع" : "إرجاع";
                await _activityService.LogActivityAsync(
                    "System",
                    "TransactionDeletion",
                    $"DELETED OPERATION TYPE: {deletedOperationType}",
                    $"Product: {product.Name}, Original Quantity: {transaction.Quantity}",
                    "Inventory"
                );
            }
            
            _context.CustomerTransactions.Remove(transaction);
            await _context.SaveChangesAsync();
            
            // تحديث الجرد اليومي بعد حذف المعاملة
            try
            {
                var dailyInventoryService = HttpContext.RequestServices.GetRequiredService<IDailyInventoryService>();
                await dailyInventoryService.RecalculateInventoryAsync(transaction.Date.Date);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"خطأ في تحديث الجرد اليومي بعد حذف المعاملة {transaction.Id}");
            }
            
            // Log activity
            await _activityService.LogActivityAsync(
                "delete", 
                "customer_transaction", 
                customerName, 
                $"تم حذف معاملة العميل {customerName}: {product?.Name} - {transaction.Quantity} قطعة - {transaction.TotalPrice:N2} جنيه"
            );
            
            TempData["SuccessMessage"] = $"تم حذف المعاملة بنجاح وإضافة {transaction.Quantity} قطعة مرة أخرى لمخزون {product?.Name}!";
            return RedirectToAction(nameof(Details), new { id = customerId });
        }

        // POST: Customers/DeleteAllTransactions/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAllTransactions(int id)
        {
            var customer = await _context.Customers
                .Include(c => c.Transactions)
                .FirstOrDefaultAsync(c => c.Id == id);
                
            if (customer == null)
            {
                return NotFound();
            }

            if (customer.Transactions != null && customer.Transactions.Any())
            {
                var transactionCount = customer.Transactions.Count;
                _context.CustomerTransactions.RemoveRange(customer.Transactions);
                await _context.SaveChangesAsync();
                
                // Log activity
                await _activityService.LogActivityAsync(
                    "delete", 
                    "customer_transactions", 
                    customer.Name, 
                    $"تم حذف {transactionCount} معاملة للعميل: {customer.Name}"
                );
                
                TempData["SuccessMessage"] = $"تم حذف {transactionCount} معاملة للعميل '{customer.Name}' بنجاح!";
            }
            else
            {
                TempData["InfoMessage"] = "لا توجد معاملات لحذفها.";
            }
            
            return RedirectToAction(nameof(Details), new { id = id });
        }

        private bool CustomerExists(int id)
        {
            return _context.Customers.Any(e => e.Id == id);
        }

        private bool CustomerTransactionExists(int id)
        {
            return _context.CustomerTransactions.Any(e => e.Id == id);
        }
    }
} 