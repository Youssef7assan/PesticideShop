using Microsoft.EntityFrameworkCore;
using PesticideShop.Data;
using PesticideShop.Models;

namespace PesticideShop.Services
{
    public class CashierService : ICashierService
    {
        private readonly ApplicationDbContext _context;
        private readonly IActivityService _activityService;
        private readonly FinancialService _financialService;
        private readonly ILogger<CashierService> _logger;

        public CashierService(ApplicationDbContext context, IActivityService activityService, FinancialService financialService, ILogger<CashierService> logger)
        {
            _context = context;
            _activityService = activityService;
            _financialService = financialService;
            _logger = logger;
        }

        /// <summary>
        /// البحث عن المنتج بالـ ID أو بالاسم
        /// </summary>
        public async Task<Product?> FindProductAsync(int productId, string? productName = null)
        {
            await _activityService.LogActivityAsync(
                "System",
                "FindProduct",
                $"SEARCH: Looking for product with ID: {productId}, Name: '{productName ?? "null"}'",
                $"ProductId > 0: {productId > 0}, Name not empty: {!string.IsNullOrEmpty(productName)}",
                "ProductSearch"
            );
            
            if (productId > 0)
            {
                var productById = await _context.Products.FindAsync(productId);
                await _activityService.LogActivityAsync(
                    "System",
                    "FindProduct",
                    $"SEARCH BY ID: ProductId {productId} found: {productById != null}",
                    $"Product: {productById?.Name ?? "null"}",
                    "ProductSearch"
                );
                return productById;
            }
            
            if (!string.IsNullOrEmpty(productName))
            {
                var productByName = await _context.Products.FirstOrDefaultAsync(p => p.Name == productName);
                await _activityService.LogActivityAsync(
                    "System",
                    "FindProduct",
                    $"SEARCH BY NAME: Product '{productName}' found: {productByName != null}",
                    $"Product ID: {productByName?.Id ?? 0}",
                    "ProductSearch"
                );
                return productByName;
            }
            
            await _activityService.LogActivityAsync(
                "System",
                "FindProduct",
                "SEARCH FAILED: Neither ProductId nor ProductName provided",
                $"ProductId: {productId}, ProductName: '{productName ?? "null"}'",
                "ProductSearch"
            );
            
            return null;
        }

        /// <summary>
        /// التحقق من صحة بيانات العميل أو إنشاء عميل جديد
        /// </summary>
        public async Task<Customer?> ValidateOrCreateCustomerAsync(TransactionRequest request)
        {
            Customer? customer = null;

            // البحث بالـ ID أولاً
            if (request.CustomerId > 0)
            {
                customer = await _context.Customers.FindAsync(request.CustomerId);
                if (customer != null) return customer;
            }

            // التحقق من وجود العميل بالهاتف
            if (!string.IsNullOrEmpty(request.CustomerPhone))
            {
                customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.PhoneNumber == request.CustomerPhone);
                
                // إذا كان العميل موجود وطلب إنشاء عميل جديد، ارفض العملية
                if (customer != null && request.CustomerId == 0) // CustomerId = 0 يعني عميل جديد
                {
                    throw new InvalidOperationException($"رقم الهاتف '{request.CustomerPhone}' مسجل بالفعل للعميل '{customer.Name}'. لا يمكن تسجيل رقم هاتف مكرر.");
                }
            }

            // إنشاء عميل جديد إذا لم يوجد
            if (customer == null && !string.IsNullOrEmpty(request.CustomerName) && !string.IsNullOrEmpty(request.CustomerPhone))
            {
                await _activityService.LogActivityAsync(
                    "System",
                    "CustomerCreation",
                    $"Starting customer creation process",
                    $"Name: {request.CustomerName}, Phone: {request.CustomerPhone}",
                    "Customer"
                );

                customer = CreateNewCustomer(request);
                
                await _activityService.LogActivityAsync(
                    "System",
                    "CustomerCreation",
                    $"Customer object created, adding to context",
                    $"Customer ID before save: {customer.Id}",
                    "Customer"
                );

                _context.Customers.Add(customer);
                
                try
                {
                    await _context.SaveChangesAsync();
                    
                    await _activityService.LogActivityAsync(
                        "System",
                        "CustomerCreation",
                        $"SaveChanges completed successfully",
                        $"Customer ID after save: {customer.Id}",
                        "Customer"
                    );
                }
                catch (Exception ex)
                {
                    await _activityService.LogActivityAsync(
                        "System",
                        "CustomerCreation",
                        $"SaveChanges failed: {ex.Message}",
                        $"Inner Exception: {ex.InnerException?.Message}",
                        "Error"
                    );
                    throw;
                }

                // التأكد من أن العميل تم حفظه بنجاح
                var savedCustomer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.PhoneNumber == request.CustomerPhone);
                
                if (savedCustomer != null)
                {
                    customer = savedCustomer; // استخدام العميل المحفوظ من قاعدة البيانات
                    
                    await _activityService.LogActivityAsync(
                        "System", 
                        "Customer", 
                        customer.Name, 
                        $"تم إضافة عميل جديد بنجاح: {customer.Name} - {customer.PhoneNumber} (ID: {customer.Id})"
                    );
                }
                else
                {
                    await _activityService.LogActivityAsync(
                        "System", 
                        "Customer", 
                        request.CustomerName, 
                        $"فشل في حفظ العميل الجديد: {request.CustomerName} - {request.CustomerPhone}"
                    );
                    throw new InvalidOperationException("فشل في حفظ العميل الجديد في قاعدة البيانات");
                }
            }

            return customer;
        }

        /// <summary>
        /// التحقق من صحة طلب الإرجاع (مع حماية من الإرجاع الزائد)
        /// </summary>
        public async Task<(bool isValid, string? errorMessage)> ValidateReturnRequestAsync(TransactionRequest request)
        {
            // التحقق من وجود منتجات بكميات سالبة (إرجاع)
            var returnItems = request.Items.Where(i => i.Quantity < 0).ToList();
            
            if (!returnItems.Any())
            {
                return (true, null); // لا توجد عمليات إرجاع
            }

            // التحقق من وجود رقم الفاتورة الأصلية للربط
            if (!string.IsNullOrEmpty(request.OriginalInvoiceNumber))
            {
                var originalInvoice = await _context.Invoices
                    .Include(i => i.Items)
                    .ThenInclude(ii => ii.Product)
                    .FirstOrDefaultAsync(i => i.InvoiceNumber == request.OriginalInvoiceNumber);

                if (originalInvoice == null)
                {
                    return (false, "الفاتورة الأصلية غير موجودة");
                }

                // التحقق من أن كميات الإرجاع لا تتجاوز المتاح
                foreach (var returnItem in returnItems)
                {
                    var originalItem = originalInvoice.Items.FirstOrDefault(i => 
                        i.ProductId == returnItem.ProductId || 
                        (i.Product != null && i.Product.Name == returnItem.ProductName));

                    if (originalItem == null)
                    {
                        return (false, $"المنتج '{returnItem.ProductName}' غير موجود في الفاتورة الأصلية");
                    }

                    // حساب الكمية المرتجعة سابقاً
                    var previouslyReturned = await _context.ReturnTrackings
                        .Where(r => r.OriginalInvoiceNumber == request.OriginalInvoiceNumber && 
                                   r.ProductId == returnItem.ProductId)
                        .SumAsync(r => r.ReturnedQuantity);

                    var requestedReturnQuantity = Math.Abs(returnItem.Quantity);
                    var totalReturnQuantity = previouslyReturned + requestedReturnQuantity;

                    if (totalReturnQuantity > originalItem.Quantity)
                    {
                        return (false, $"لا يمكن إرجاع {requestedReturnQuantity} من '{returnItem.ProductName}'. " +
                                     $"الكمية الأصلية: {originalItem.Quantity}, " +
                                     $"مرتجع سابقاً: {previouslyReturned}, " +
                                     $"متاح للإرجاع: {originalItem.Quantity - previouslyReturned}");
                    }

                    await _activityService.LogActivityAsync(
                        "System",
                        "ReturnValidation",
                        $"RETURN VALIDATION: {returnItem.ProductName} - Requested: {requestedReturnQuantity}",
                        $"Original: {originalItem.Quantity}, Previously returned: {previouslyReturned}, Available: {originalItem.Quantity - previouslyReturned}",
                        "Validation"
                    );
                }
            }

            return (true, null);
        }

        /// <summary>
        /// معالجة عناصر المعاملة وإنشاء المعاملات
        /// </summary>
        public async Task<List<CustomerTransaction>> ProcessTransactionItemsAsync(TransactionRequest request, Customer customer)
        {
            var transactions = new List<CustomerTransaction>();

            await _activityService.LogActivityAsync(
                "System",
                "ProcessTransactionItems",
                $"STARTING: Processing {request.Items.Count} items for customer {customer.Name}",
                $"Customer ID: {customer.Id}",
                "Transaction"
            );

            foreach (var item in request.Items)
            {
                await _activityService.LogActivityAsync(
                    "System",
                    "ProcessTransactionItems",
                    $"PROCESSING ITEM: ProductId={item.ProductId}, ProductName='{item.ProductName}', Quantity={item.Quantity}",
                    $"Customer: {customer.Name}",
                    "Transaction"
                );

                var product = await FindProductAsync(item.ProductId, item.ProductName);
                if (product == null)
                {
                    await _activityService.LogActivityAsync(
                        "System",
                        "ProcessTransactionItems",
                        $"PRODUCT NOT FOUND: ProductId={item.ProductId}, ProductName='{item.ProductName}'",
                        $"Customer: {customer.Name}",
                        "Error"
                    );
                    throw new InvalidOperationException($"المنتج غير موجود: {item.ProductName ?? item.ProductId.ToString()}");
                }

                await _activityService.LogActivityAsync(
                    "System",
                    "ProcessTransactionItems",
                    $"PRODUCT FOUND: {product.Name} (ID: {product.Id}), Current Qty: {product.Quantity}",
                    $"Will process quantity: {item.Quantity}",
                    "Transaction"
                );

                // معالجة المخزون - مع الحفظ الفوري
                await ProcessInventoryAsync(product, item, true);

                // إنشاء المعاملة
                var transaction = CreateTransaction(customer, product, item);
                transactions.Add(transaction);

                await _activityService.LogActivityAsync(
                    "System",
                    "ProcessTransactionItems",
                    $"ITEM COMPLETED: {product.Name} processed successfully",
                    $"Final quantity: {product.Quantity}, Transaction: {transaction.TotalPrice}",
                    "Transaction"
                );
            }

            await _activityService.LogActivityAsync(
                "System",
                "ProcessTransactionItems",
                $"COMPLETED: Processed all {transactions.Count} transactions",
                $"Customer: {customer.Name}",
                "Transaction"
            );

            return transactions;
        }

        /// <summary>
        /// إنشاء الفاتورة
        /// </summary>
        public async Task<Invoice> CreateInvoiceAsync(TransactionRequest request, Customer customer, List<CustomerTransaction> transactions, string? cashierName = null)
        {
            _logger.LogInformation($"CashierService.CreateInvoiceAsync called with cashierName: {cashierName}");
            var subtotalAmount = transactions.Sum(t => t.TotalPrice);
            var totalDiscount = transactions.Sum(t => t.Discount);
            
            // إجمالي الفاتورة = المبيعات فقط (بدون الشحن)
            // الشحن للعرض فقط - لا يدخل في أي عمليات حسابية
            var finalTotal = subtotalAmount;

            var invoice = new Invoice
            {
                CustomerId = customer.Id,
                InvoiceNumber = !string.IsNullOrEmpty(request.InvoiceNumber) ? request.InvoiceNumber : GenerateInvoiceNumber(),
                OrderNumber = !string.IsNullOrEmpty(request.OrderNumber) ? request.OrderNumber : GenerateOrderNumber(),
                PolicyNumber = request.PolicyNumber,
                Status = (InvoiceStatus)request.InvoiceStatus,
                PaymentMethod = request.PaymentMethod,
                OrderOrigin = (OrderOrigin)request.OrderOrigin,
                InvoiceDate = DateTime.Now,
                TotalAmount = finalTotal, // المبيعات فقط (بدون الشحن)
                Discount = totalDiscount,
                ShippingCost = subtotalAmount >= 0 ? request.ShippingCost : 0, // للعرض فقط
                ShippingType = request.ShippingType.HasValue ? (ShippingType)request.ShippingType.Value : null,
                AmountPaid = finalTotal < 0 ? finalTotal : request.AmountPaid,
                RemainingAmount = finalTotal < 0 ? 0 : (finalTotal - request.AmountPaid), // المتبقي من المبيعات فقط
                Type = DetermineInvoiceType(finalTotal, request.InvoiceType),
                Notes = BuildInvoiceNotes(request),
                CashierName = cashierName,
                CreatedAt = DateTime.Now
            };

            // تسجيل تفاصيل الفاتورة والعميل
            await _activityService.LogActivityAsync(
                "System",
                "InvoiceCreation",
                $"Creating invoice for customer: {customer.Name} (ID: {customer.Id})",
                $"Invoice Number: {invoice.InvoiceNumber}, Total: {finalTotal}, Customer Phone: {customer.PhoneNumber}",
                "Invoice"
            );

            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation($"Invoice created with ID: {invoice.Id}, CashierName: {invoice.CashierName}");

            // إنشاء عناصر الفاتورة
            await CreateInvoiceItemsAsync(invoice.Id, request.Items);

            // ملاحظة: الشحن يظهر فقط في الفاتورة ولا يتم إنشاء CustomerTransaction منفصلة له
            // لأن الشحن ليس جزءاً من المبيعات أو المدفوعات الفعلية

            return invoice;
        }

        // تم إزالة دالة CreateShippingTransactionAsync
        // الشحن يظهر فقط في الفاتورة ولا يتم إنشاء CustomerTransaction منفصلة له

        /// <summary>
        /// حفظ تتبع المرتجعات والاستبدالات (محدث)
        /// </summary>
        public async Task SaveReturnTrackingAsync(TransactionRequest request)
        {
            // حفظ فقط العناصر ذات الكميات السالبة (المرتجعات)
            var returnItems = request.Items.Where(i => i.Quantity < 0).ToList();
            
            if (!returnItems.Any())
                return;

            foreach (var item in returnItems)
            {
                var product = await FindProductAsync(item.ProductId, item.ProductName);
                if (product != null)
                {
                    var returnTracking = new ReturnTracking
                    {
                        OriginalInvoiceNumber = request.OriginalInvoiceNumber ?? "",
                        ProductId = product.Id,
                        ReturnedQuantity = Math.Abs(item.Quantity),
                        ReturnInvoiceNumber = request.InvoiceNumber ?? GenerateInvoiceNumber(),
                        ReturnDate = DateTime.Now,
                        ReturnReason = request.Notes ?? "إرجاع منتج",
                        CreatedBy = "System",
                        CreatedAt = DateTime.Now
                    };

                    _context.ReturnTrackings.Add(returnTracking);
                    
                    await _activityService.LogActivityAsync(
                        "System",
                        "ReturnTracking",
                        $"RETURN TRACKING CREATED: {product.Name} - Qty: {returnTracking.ReturnedQuantity}",
                        $"Original Invoice: {returnTracking.OriginalInvoiceNumber}, Return Invoice: {returnTracking.ReturnInvoiceNumber}",
                        "ReturnTracking"
                    );
                }
            }
            
            // حفظ سجلات تتبع المرتجعات
            await _context.SaveChangesAsync();
            
            // تحديث الإحصائيات المالية لكل عملية إرجاع
            var returnTrackings = await _context.ReturnTrackings
                .Include(rt => rt.Product)
                .Where(rt => returnItems.Select(ri => ri.ProductId).Contains(rt.ProductId) && 
                            rt.OriginalInvoiceNumber == request.OriginalInvoiceNumber)
                .ToListAsync();
            
            foreach (var returnTracking in returnTrackings)
            {
                await _financialService.UpdateFinancialsAfterReturnAsync(returnTracking);
            }
            
            await _activityService.LogActivityAsync(
                "System",
                "ReturnTracking",
                $"RETURN TRACKING SAVED: {returnItems.Count} return tracking records saved and financials updated",
                $"Original Invoice: {request.OriginalInvoiceNumber}",
                "ReturnTracking"
            );
        }

        /// <summary>
        /// حفظ تتبع الاستبدالات (جديد)
        /// </summary>
        public async Task SaveExchangeTrackingAsync(TransactionRequest request)
        {
            // البحث عن عمليات الاستبدال (منتج مرتجع + منتج مباع في نفس الفاتورة)
            var returnItems = request.Items.Where(i => i.Quantity < 0).ToList();
            var saleItems = request.Items.Where(i => i.Quantity > 0).ToList();
            
            if (!returnItems.Any() || !saleItems.Any())
                return;

            foreach (var returnItem in returnItems)
            {
                var oldProduct = await FindProductAsync(returnItem.ProductId, returnItem.ProductName);
                if (oldProduct == null) continue;

                foreach (var saleItem in saleItems)
                {
                    var newProduct = await FindProductAsync(saleItem.ProductId, saleItem.ProductName);
                    if (newProduct == null) continue;

                    // إنشاء سجل استبدال
                    var exchangeTracking = new ExchangeTracking
                    {
                        OriginalInvoiceNumber = request.OriginalInvoiceNumber ?? "",
                        ExchangeInvoiceNumber = request.InvoiceNumber ?? GenerateInvoiceNumber(),
                        OldProductId = oldProduct.Id,
                        NewProductId = newProduct.Id,
                        ExchangedQuantity = Math.Min(Math.Abs(returnItem.Quantity), saleItem.Quantity),
                        PriceDifference = (saleItem.Price * saleItem.Quantity) - (returnItem.Price * Math.Abs(returnItem.Quantity)),
                        ExchangeReason = request.Notes ?? "استبدال منتج",
                        ExchangeDate = DateTime.Now,
                        CreatedBy = "System",
                        CreatedAt = DateTime.Now
                    };

                    _context.ExchangeTrackings.Add(exchangeTracking);
                    
                    await _activityService.LogActivityAsync(
                        "System",
                        "ExchangeTracking",
                        $"EXCHANGE TRACKING CREATED: {oldProduct.Name} → {newProduct.Name} - Qty: {exchangeTracking.ExchangedQuantity}",
                        $"Original Invoice: {exchangeTracking.OriginalInvoiceNumber}, Exchange Invoice: {exchangeTracking.ExchangeInvoiceNumber}",
                        "ExchangeTracking"
                    );
                }
            }
            
            // حفظ سجلات تتبع الاستبدالات
            await _context.SaveChangesAsync();
            
            await _activityService.LogActivityAsync(
                "System",
                "ExchangeTracking",
                $"EXCHANGE TRACKING SAVED: {returnItems.Count * saleItems.Count} exchange tracking records saved",
                $"Original Invoice: {request.OriginalInvoiceNumber}",
                "ExchangeTracking"
            );
        }

        /// <summary>
        /// توليد رقم فاتورة (يبدأ من 0001)
        /// </summary>
        public string GenerateInvoiceNumber()
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

        /// <summary>
        /// توليد رقم أمر (يبدأ من 0001)
        /// </summary>
        public string GenerateOrderNumber()
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

        #region Private Methods

        /// <summary>
        /// إنشاء عميل جديد
        /// </summary>
        private Customer CreateNewCustomer(TransactionRequest request)
        {
            var customer = new Customer
            {
                Name = request.CustomerName,
                PhoneNumber = request.CustomerPhone,
                AdditionalPhone = request.CustomerAdditionalPhone,
                Email = request.CustomerEmail,
                Governorate = request.CustomerGovernorate,
                District = request.CustomerDistrict,
                DetailedAddress = request.CustomerDetailedAddress,
                Address = request.CustomerAddress ?? "",
                CreatedAt = DateTime.Now
            };

            // تسجيل تفاصيل العميل الجديد
            _activityService.LogActivityAsync(
                "System",
                "CreateNewCustomer",
                $"Creating new customer: {customer.Name}",
                $"Phone: {customer.PhoneNumber}, Email: {customer.Email}, Governorate: {customer.Governorate}",
                "Customer"
            ).Wait(); // استخدام Wait() لأن هذه دالة private

            return customer;
        }





        /// <summary>
        /// معالجة المخزون بناءً على الكمية (محدث)
        /// </summary>
        private async Task ProcessInventoryAsync(Product product, TransactionItem item, bool saveImmediately = false)
        {
            var oldQuantity = product.Quantity;
            
            await _activityService.LogActivityAsync(
                "System",
                "ProcessInventory",
                $"PROCESSING: Product '{product.Name}', Item Qty: {item.Quantity}, Old Product Qty: {oldQuantity}",
                $"ProductId: {product.Id}, SaveImmediately: {saveImmediately}",
                "Inventory"
            );
            
            if (item.Quantity > 0)
            {
                // بيع: تقليل المخزون
                if (product.Quantity < item.Quantity)
                {
                    throw new InvalidOperationException($"كمية غير كافية للمنتج: {product.Name}");
                }
                product.Quantity -= item.Quantity;
                
                await _activityService.LogActivityAsync(
                    "System",
                    "SaleTransaction",
                    $"SALE: Product: {product.Name}, Sold Qty: -{item.Quantity}, Old Qty: {oldQuantity}, New Qty: {product.Quantity}",
                    $"Product ID: {product.Id}",
                    "Inventory"
                );
            }
            else if (item.Quantity < 0)
            {
                // إرجاع: زيادة المخزون
                var returnQuantity = Math.Abs(item.Quantity);
                product.Quantity += returnQuantity;
                
                await _activityService.LogActivityAsync(
                    "System",
                    "ReturnTransaction",
                    $"RETURN: Product: {product.Name}, Returned Qty: +{returnQuantity}, Old Qty: {oldQuantity}, New Qty: {product.Quantity}",
                    $"Product ID: {product.Id} - تم إضافة الكمية المرتجعة للمخزون",
                    "Inventory"
                );
            }
            
            // Force Entity Framework to track changes
            if (item.Quantity != 0)
            {
                _context.Entry(product).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                _context.Products.Update(product);
                
                await _activityService.LogActivityAsync(
                    "System",
                    "InventoryUpdate",
                    $"FORCED UPDATE: Product: {product.Name} marked as modified. Old: {oldQuantity}, New: {product.Quantity}, Change: {product.Quantity - oldQuantity}",
                    $"Product ID: {product.Id}, Item Quantity: {item.Quantity}",
                    "Inventory"
                );

                // حفظ فوري إذا مطلوب
                if (saveImmediately)
                {
                    var saveResult = await _context.SaveChangesAsync();
                    
                    await _activityService.LogActivityAsync(
                        "System",
                        "ImmediateSave",
                        $"IMMEDIATE SAVE: Product {product.Name} saved to DB. Records affected: {saveResult}",
                        $"Product ID: {product.Id}, New Quantity: {product.Quantity}",
                        "Database"
                    );

                    // تحقق من الحفظ الفعلي
                    var verifyProduct = await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == product.Id);
                    if (verifyProduct != null)
                    {
                        await _activityService.LogActivityAsync(
                            "System",
                            "SaveVerification",
                            $"SAVE VERIFIED: Product {verifyProduct.Name} quantity in DB is now: {verifyProduct.Quantity}",
                            $"Product ID: {verifyProduct.Id}, Expected: {product.Quantity}, Actual: {verifyProduct.Quantity}",
                            "Verification"
                        );
                    }
                }
            }
        }

        /// <summary>
        /// إنشاء معاملة (مبسط)
        /// </summary>
        private CustomerTransaction CreateTransaction(Customer customer, Product product, TransactionItem item)
        {
            var totalPrice = (item.Price - item.Discount) * item.Quantity;
            var notes = item.Notes ?? "";
            
            // Add color and size to notes if provided
            if (!string.IsNullOrEmpty(item.Color) || !string.IsNullOrEmpty(item.Size))
            {
                var colorSizeInfo = new List<string>();
                if (!string.IsNullOrEmpty(item.Color))
                    colorSizeInfo.Add($"اللون: {item.Color}");
                if (!string.IsNullOrEmpty(item.Size))
                    colorSizeInfo.Add($"المقاس: {item.Size}");
                
                if (!string.IsNullOrEmpty(notes))
                    notes += " | ";
                notes += string.Join(" - ", colorSizeInfo);
            }

            return new CustomerTransaction
            {
                CustomerId = customer.Id,
                ProductId = product.Id,
                Quantity = item.Quantity, // الكمية كما هي (موجبة أو سالبة)
                Price = item.Price,
                TotalPrice = totalPrice, // سيكون سالب إذا كانت الكمية سالبة
                Discount = item.Discount, // حفظ الخصم كما هو
                ShippingCost = 0, // سيتم تحديده لاحقاً في الكاشير
                AmountPaid = 0, // سيتم تحديده لاحقاً
                Date = DateTime.Now,
                // Use selected color/size if provided, otherwise use product default
                Color = !string.IsNullOrEmpty(item.Color) ? item.Color : product.Color,
                Size = !string.IsNullOrEmpty(item.Size) ? item.Size : product.Size.ToString(),
                Notes = notes
            };
        }

        /// <summary>
        /// تحديد حالة الفاتورة (مبسط)
        /// </summary>
        private InvoiceStatus DetermineInvoiceStatus(decimal totalAmount, decimal amountPaid)
        {
            if (totalAmount < 0)
                return InvoiceStatus.Paid; // فاتورة إرجاع
            
            if (amountPaid <= 0)
                return InvoiceStatus.Sent; // غير مدفوع
            
            if (amountPaid >= totalAmount)
                return InvoiceStatus.Paid; // مدفوع بالكامل
            
            return InvoiceStatus.PartiallyPaid; // مدفوع جزئياً
        }

        /// <summary>
        /// تحديد نوع الفاتورة (مبسط)
        /// </summary>
        private InvoiceType DetermineInvoiceType(decimal totalAmount, int requestedType)
        {
            if (totalAmount < 0)
                return InvoiceType.Return; // فاتورة إرجاع
                
            return (InvoiceType)requestedType;
        }

        /// <summary>
        /// بناء ملاحظات الفاتورة (مبسط)
        /// </summary>
        private string BuildInvoiceNotes(TransactionRequest request)
        {
            var notes = new List<string>();

            // إضافة نوع الفاتورة
            var hasReturns = request.Items.Any(i => i.Quantity < 0);
            var hasSales = request.Items.Any(i => i.Quantity > 0);
            
            if (hasReturns && hasSales)
            {
                notes.Add("نوع العملية: بيع واسترداد");
            }
            else if (hasReturns)
            {
                notes.Add("نوع العملية: استرداد");
            }
            else
            {
                notes.Add("نوع العملية: بيع");
            }

            // إضافة رقم الفاتورة الأصلية إن وجد
            if (!string.IsNullOrEmpty(request.OriginalInvoiceNumber))
            {
                notes.Add($"مرتبطة بالفاتورة: {request.OriginalInvoiceNumber}");
            }

            // إضافة ملاحظات عامة
            if (!string.IsNullOrEmpty(request.Notes))
            {
                notes.Add($"ملاحظات: {request.Notes}");
            }

            // إضافة ملاحظات المنتجات
            var itemNotes = request.Items
                .Where(item => !string.IsNullOrEmpty(item.Notes))
                .Select(item => $"{item.ProductName}: {item.Notes}")
                .ToList();

            if (itemNotes.Any())
            {
                notes.Add($"تفاصيل: {string.Join(", ", itemNotes)}");
            }

            return string.Join(" | ", notes);
        }

        /// <summary>
        /// إنشاء عناصر الفاتورة (بدون تحديث المخزون - يتم في ProcessTransactionItemsAsync)
        /// </summary>
        private async Task CreateInvoiceItemsAsync(int invoiceId, List<TransactionItem> items)
        {
            foreach (var item in items)
            {
                var product = await FindProductAsync(item.ProductId, item.ProductName);
                if (product != null)
                {
                    // تسجيل العملية فقط بدون تحديث المخزون (تم التحديث مسبقاً)
                    await _activityService.LogActivityAsync(
                        "System",
                        "InvoiceItemCreated",
                        $"INVOICE ITEM: Product: {product.Name}, Qty: {item.Quantity}, Current Qty: {product.Quantity}",
                        $"Invoice ID: {invoiceId}, Product ID: {product.Id}",
                        "Invoice"
                    );
                    
                    var notes = item.Notes ?? "";
                    
                    // Add color and size to notes if provided
                    if (!string.IsNullOrEmpty(item.Color) || !string.IsNullOrEmpty(item.Size))
                    {
                        var colorSizeInfo = new List<string>();
                        if (!string.IsNullOrEmpty(item.Color))
                            colorSizeInfo.Add($"اللون: {item.Color}");
                        if (!string.IsNullOrEmpty(item.Size))
                            colorSizeInfo.Add($"المقاس: {item.Size}");
                        
                        if (!string.IsNullOrEmpty(notes))
                            notes += " | ";
                        notes += string.Join(" - ", colorSizeInfo);
                    }
                    
                    var invoiceItem = new InvoiceItem
                    {
                        InvoiceId = invoiceId,
                        ProductId = product.Id,
                        Quantity = item.Quantity,
                        UnitPrice = item.Price,
                        Discount = item.Discount,
                        TotalPrice = (item.Price - item.Discount) * item.Quantity,
                        // Use selected color/size if provided, otherwise use product default
                        Color = !string.IsNullOrEmpty(item.Color) ? item.Color : product.Color,
                        Size = !string.IsNullOrEmpty(item.Size) ? item.Size : product.Size.ToString(),
                        Notes = notes
                    };
                    _context.InvoiceItems.Add(invoiceItem);
                }
            }

            await _context.SaveChangesAsync();
        }



        #endregion
    }
}
