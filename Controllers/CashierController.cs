using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PesticideShop.Data;
using PesticideShop.Models;
using PesticideShop.Services;

namespace PesticideShop.Controllers
{
	[Authorize]
	public class CashierController : Controller
	{
		private readonly ApplicationDbContext _context;
		private readonly IActivityService _activityService;
		private readonly IDailyInventoryService _dailyInventoryService;
		private readonly ICashierService _cashierService;
		private readonly ILogger<CashierController> _logger;

		public CashierController(
			ApplicationDbContext context, 
			IActivityService activityService, 
			IDailyInventoryService dailyInventoryService,
			ICashierService cashierService,
			ILogger<CashierController> logger)
		{
			_context = context;
			_activityService = activityService;
			_dailyInventoryService = dailyInventoryService;
			_cashierService = cashierService;
			_logger = logger;
		}

		// GET: Cashier
		public async Task<IActionResult> Index()
		{
			// Get all customers for the dropdown
			var customers = await _context.Customers
				.OrderBy(c => c.Name)
				.ToListAsync();

			ViewData["Customers"] = customers;
			return View();
		}



		// API endpoint to get product by QR code - Enhanced Version
		[HttpGet]
		public async Task<IActionResult> GetProductByQR(string qrCode)
		{
			if (string.IsNullOrEmpty(qrCode))
			{
				return Json(new { success = false, message = "رمز QR مطلوب" });
			}

			try
			{
				// البحث عن المنتج بالرمز الدقيق
			var product = await _context.Products
					.Include(p => p.Category)
				.FirstOrDefaultAsync(p => p.QRCode == qrCode);

			if (product == null)
			{
					// تسجيل محاولة البحث الفاشلة
					await _activityService.LogActivityAsync(
						User.Identity?.Name ?? "Unknown",
						"QR_SEARCH_FAILED",
						$"QR code not found: {qrCode}",
						"Product not found",
						"Warning"
					);
					
					return Json(new { success = false, message = "المنتج غير موجود" });
				}

				// تسجيل البحث الناجح
				await _activityService.LogActivityAsync(
					User.Identity?.Name ?? "Unknown",
					"QR_SEARCH_SUCCESS",
					$"QR code found: {qrCode}",
					$"Product: {product.Name}",
					"Info"
				);

				return Json(new
				{
				success = true,
					product = new
					{
					id = product.Id,
					name = product.Name,
					price = product.Price,
					quantity = product.Quantity,
					qrCode = product.QRCode,
					color = product.Color,
					size = product.Size.ToString(),
					hasMultipleColors = product.HasMultipleColors,
					hasMultipleSizes = product.HasMultipleSizes,
					availableColors = product.AvailableColors,
						availableSizes = product.AvailableSizes,
						categoryName = product.Category?.Name,
						stockStatus = product.Quantity > 0 ? "متوفر" : "نفد المخزون"
				}
			});
		}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error scanning QR code: {qrCode}");
				
						await _activityService.LogActivityAsync(
							User.Identity?.Name ?? "Unknown",
					"QR_SCAN_ERROR",
					$"Error scanning QR code: {qrCode}",
					ex.Message,
					"Error"
				);
				
				return Json(new { success = false, message = "خطأ في قراءة رمز QR" });
			}
		}

		// Search customer by phone number
		[HttpGet]
		public async Task<IActionResult> SearchCustomerByPhone(string phone)
		{
			if (string.IsNullOrEmpty(phone))
			{
				return Json(new { success = false, message = "رقم الهاتف مطلوب" });
			}

			try
			{
				var customer = await _context.Customers
					.FirstOrDefaultAsync(c => c.PhoneNumber == phone || c.AdditionalPhone == phone);

				if (customer == null)
				{
					return Json(new { success = false, message = "لم يتم العثور على عميل بهذا الرقم" });
				}

				return Json(new { 
					success = true,
					customer = new {
						id = customer.Id,
						name = customer.Name,
						phone = customer.PhoneNumber,
						additionalPhone = customer.AdditionalPhone,
						email = customer.Email,
						governorate = customer.Governorate,
						district = customer.District,
						detailedAddress = customer.DetailedAddress
					}
				});
			}
			catch (Exception ex)
			{
				await _activityService.LogActivityAsync(
					User.Identity?.Name ?? "Unknown",
					"CUSTOMER_SEARCH_ERROR",
					$"Error searching customer by phone: {phone}",
					ex.Message,
					"Error"
				);
				return Json(new { success = false, message = "خطأ في البحث عن العميل" });
			}
		}

		// Search products API - SIMPLE
		// API endpoint to get categories from database
		[HttpGet]
		public async Task<IActionResult> GetCategories()
		{
			try
			{
				var categories = await _context.Products
					.Include(p => p.Category)
					.Where(p => p.Category != null && !string.IsNullOrEmpty(p.Category.Name))
					.Select(p => p.Category.Name)
					.Distinct()
					.OrderBy(c => c)
					.ToListAsync();

				return Json(new { success = true, categories = categories });
			}
			catch (Exception ex)
			{
				Console.WriteLine($"❌ Error getting categories: {ex.Message}");
				return Json(new { success = false, message = "حدث خطأ في جلب الفئات" });
			}
		}

		// API endpoint to get all products grouped by category
		[HttpGet]
		public async Task<IActionResult> GetAllProductsByCategory()
		{
			try
			{
				var productsByCategory = await _context.Products
					.Include(p => p.Category)
					.Where(p => p.Category != null && p.Quantity > 0) // Only available products
					.OrderBy(p => p.Category.Name)
					.ThenBy(p => p.Name)
					.Select(p => new
					{
						id = p.Id,
						name = p.Name,
						price = p.Price,
						quantity = p.Quantity,
						categoryName = p.Category.Name,
						color = p.Color.ToString(),
						size = p.Size.ToString()
					})
					.ToListAsync();

				// Group products by category
				var groupedProducts = productsByCategory
					.GroupBy(p => p.categoryName)
					.ToDictionary(g => g.Key, g => g.ToList());

				return Json(new { success = true, productsByCategory = groupedProducts });
			}
			catch (Exception ex)
			{
				Console.WriteLine($"❌ Error getting products by category: {ex.Message}");
				return Json(new { success = false, message = "حدث خطأ في جلب المنتجات" });
			}
		}

		// API endpoint to get popular products from database
		[HttpGet]
		public async Task<IActionResult> GetPopularProducts(int limit = 6)
		{
			try
			{
				// Get products with highest sales (most sold items)
				var popularProducts = await _context.InvoiceItems
					.Include(ii => ii.Product)
					.Where(ii => ii.Product != null)
					.GroupBy(ii => ii.ProductId)
					.Select(g => new
					{
						ProductId = g.Key,
						TotalSold = g.Sum(ii => ii.Quantity),
						Product = g.First().Product
					})
					.OrderByDescending(x => x.TotalSold)
					.Take(limit)
					.Select(x => new
					{
						id = x.Product.Id,
						name = x.Product.Name,
						price = x.Product.Price,
						quantity = x.Product.Quantity,
						totalSold = x.TotalSold,
						stockStatus = x.Product.Quantity > 0 ? "متوفر" : "نفد المخزون"
					})
					.ToListAsync();

				// If no sales data, get random recent products
				if (!popularProducts.Any())
				{
					popularProducts = await _context.Products
						.Where(p => p.Quantity > 0)
						.OrderByDescending(p => p.Id)
						.Take(limit)
						.Select(p => new
						{
							id = p.Id,
							name = p.Name,
							price = p.Price,
							quantity = p.Quantity,
							totalSold = 0,
							stockStatus = "متوفر"
						})
						.ToListAsync();
				}

				return Json(new { success = true, products = popularProducts });
			}
			catch (Exception ex)
			{
				Console.WriteLine($"❌ Error getting popular products: {ex.Message}");
				return Json(new { success = false, message = "حدث خطأ في جلب المنتجات الشائعة" });
			}
		}

		// API endpoint to search by category
		[HttpGet]
		public async Task<IActionResult> SearchByCategory(string category, bool includeOutOfStock = true, int limit = 20)
		{
			try
			{
				Console.WriteLine($"🏷️ CATEGORY SEARCH: '{category}'");

				if (string.IsNullOrWhiteSpace(category))
				{
					return Json(new { success = false, message = "يرجى تحديد الفئة" });
				}

				var query = _context.Products.Include(p => p.Category).AsQueryable();

				// Search by category (case insensitive)
				query = query.Where(p => p.Category != null && p.Category.Name.ToLower().Contains(category.ToLower()));

				if (!includeOutOfStock)
				{
					query = query.Where(p => p.Quantity > 0);
				}

				var products = await query
					.OrderBy(p => p.Name)
					.Take(limit)
					.Select(p => new
					{
						id = p.Id,
						name = p.Name,
						price = p.Price,
						quantity = p.Quantity,
						category = p.Category != null ? p.Category.Name : null,
						stockStatus = p.Quantity > 0 ? "متوفر" : "نفد المخزون"
					})
					.ToListAsync();

				Console.WriteLine($"📦 CATEGORY RESULTS: {products.Count} products");

				return Json(new
				{
					success = true,
					products = products,
					searchInfo = new
					{
						category = category,
						resultsCount = products.Count,
						includeOutOfStock = includeOutOfStock
					}
				});
			}
			catch (Exception ex)
			{
				Console.WriteLine($"❌ Category search error: {ex.Message}");
				return Json(new { success = false, message = "حدث خطأ في البحث بالفئة" });
			}
		}

		// API endpoint for advanced search with filters
		[HttpGet]
		public async Task<IActionResult> AdvancedSearch(
			string term = "", 
			string category = "", 
			string color = "", 
			string size = "", 
			int limit = 50)
		{
			try
			{
				Console.WriteLine($"🔍 SIMPLIFIED SEARCH: term='{term}', category='{category}', color='{color}', size='{size}'");

				var query = _context.Products.Include(p => p.Category).AsQueryable();

				// Main search term
				if (!string.IsNullOrWhiteSpace(term))
				{
						query = query.Where(p => 
						p.Name.Contains(term) ||
						p.Id.ToString() == term);
				}

				// Category filter
				if (!string.IsNullOrWhiteSpace(category))
				{
					query = query.Where(p => p.Category != null && 
						EF.Functions.Like(p.Category.Name, $"%{category}%"));
				}

				// Color filter
				if (!string.IsNullOrWhiteSpace(color))
				{
					query = query.Where(p =>
						EF.Functions.Like(p.Name, $"%{color}%") ||
						(p.Color != null && EF.Functions.Like(p.Color, $"%{color}%")));
				}

				// Size filter
				if (!string.IsNullOrWhiteSpace(size))
				{
					query = query.Where(p =>
						EF.Functions.Like(p.Name, $"%{size}%") ||
						(p.Size != null && EF.Functions.Like(p.Size.ToString(), $"%{size}%")));
				}

				// Simple sorting by name
				query = query.OrderBy(p => p.Name);

				var products = await query
					.Take(limit)
					.Select(p => new
					{
						id = p.Id,
						name = p.Name,
						price = p.Price,
						quantity = p.Quantity,
						category = p.Category != null ? p.Category.Name : null,
						color = p.Color,
						size = p.Size,
						stockStatus = p.Quantity > 0 ? "متوفر" : "نفد المخزون"
					})
					.ToListAsync();

				Console.WriteLine($"📦 SIMPLIFIED SEARCH RESULTS: {products.Count} products");

				return Json(new
				{
					success = true,
					products = products,
					searchInfo = new
					{
						term = term,
							category = category,
							color = color,
							size = size,
						resultsCount = products.Count
					}
				});
			}
			catch (Exception ex)
			{
				Console.WriteLine($"❌ Advanced search error: {ex.Message}");
				return Json(new { success = false, message = "حدث خطأ في البحث المتقدم" });
			}
		}

		[HttpGet]
		public async Task<IActionResult> SearchProducts(string term = "", bool includeOutOfStock = false, int limit = 20)
		{
			try
			{
				Console.WriteLine($"🔍 SIMPLE SEARCH: '{term}'");

				if (string.IsNullOrWhiteSpace(term))
				{
					return Json(new { success = false, message = "رجاءً أدخل رقم أو اسم المنتج", products = new List<object>() });
				}

				var cleanTerm = term.Trim();
				var query = _context.Products.AsQueryable();

				query = query.Where(p => 
					p.Name.Contains(cleanTerm) || 
					p.Id.ToString() == cleanTerm);

				if (!includeOutOfStock)
				{
					query = query.Where(p => p.Quantity > 0);
				}

				var products = await query
					.OrderBy(p => p.Name)
					.Take(limit)
					.Select(p => new
					{
						id = p.Id,
						name = p.Name,
						price = p.Price,
						quantity = p.Quantity,
						color = p.Color,
						size = p.Size.ToString(),
						hasMultipleColors = p.HasMultipleColors,
						hasMultipleSizes = p.HasMultipleSizes,
						availableColors = p.AvailableColors,
						availableSizes = p.AvailableSizes,
						stockStatus = p.Quantity > 0 ? "متوفر" : "نفد المخزون",
					})
					.ToListAsync();

				Console.WriteLine($"📦 FOUND: {products.Count} products for term '{cleanTerm}'");

				{
					await _activityService.LogActivityAsync(
						User.Identity?.Name ?? "Unknown",
						$"Found {products.Count} products",
						"Warning"
					);
				}

				return Json(new
				{
					success = true,
					products = products,
					searchInfo = new
					{
						term = cleanTerm,
						resultsCount = products.Count,
						includeOutOfStock = includeOutOfStock,
					}
				});
			}
			catch (Exception ex)
			{
				await _activityService.LogActivityAsync(
					User.Identity?.Name ?? "Unknown",
					"PRODUCT_SEARCH_ERROR",
					$"Error searching products with term: {term}",
					ex.Message,
					"Error"
				);

				return Json(new { success = false, message = "خطأ في البحث عن المنتجات" });
			}
		}

		[HttpGet]
		public async Task<IActionResult> GetAllProductsDebug()
		{
			try
			{
				var products = await _context.Products
					.Select(p => new
					{
						id = p.Id,
						name = p.Name,
						quantity = p.Quantity
					})
					.OrderBy(p => p.name)
					.ToListAsync();

				return Json(new
				{
					success = true,
					count = products.Count,
					products = products
				});
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = ex.Message });
			}
		}

		// POST: Process transaction (SIMPLIFIED VERSION)
		[HttpPost]
		public async Task<IActionResult> ProcessTransaction([FromBody] TransactionRequest request)
		{
			try
			{
				await _activityService.LogActivityAsync(
					User.Identity?.Name ?? "Unknown",
					"ProcessTransaction",
					$"RECEIVED REQUEST: {request != null}",
					$"Items: {request?.Items?.Count ?? 0}, Customer: {request?.CustomerName}, Email: {request?.CustomerEmail}",
					"Transaction"
				);

				// If request is not null, log more details
				if (request != null && request.Items != null)
				{
					foreach (var item in request.Items.Take(3)) // Log first 3 items
			{
				await _activityService.LogActivityAsync(
					User.Identity?.Name ?? "Unknown",
							"ProcessTransaction",
							$"ITEM: ProductId={item.ProductId}, Name={item.ProductName}, Qty={item.Quantity}",
							$"Price={item.Price}, Discount={item.Discount}",
							"TransactionItem"
						);
					}
				}

				// Basic validation
				if (request == null)
				{
					return Json(new { success = false, message = "طلب فارغ - فشل في ربط البيانات" });
				}

				if (request.Items == null || !request.Items.Any())
				{
					return Json(new { success = false, message = "لا توجد منتجات في المعاملة" });
				}

				// Log customer data received
				await _activityService.LogActivityAsync(
					User.Identity?.Name ?? "Unknown",
					"CustomerData",
					$"Customer Data: Name={request.CustomerName}, Phone={request.CustomerPhone}, Email={request.CustomerEmail}",
					$"Additional: {request.CustomerAdditionalPhone}, Governorate={request.CustomerGovernorate}, District={request.CustomerDistrict}, Address={request.CustomerDetailedAddress}",
					"CustomerDebug"
				);

				// Validate customer
				await _activityService.LogActivityAsync(
					User.Identity?.Name ?? "Unknown",
					"CashierTransaction",
					$"Starting customer validation/creation",
					$"Customer Name: {request.CustomerName}, Phone: {request.CustomerPhone}, ID: {request.CustomerId}",
					"Transaction"
				);

				Customer? customer = null;
				try
				{
					customer = await _cashierService.ValidateOrCreateCustomerAsync(request);
				}
				catch (InvalidOperationException ex)
				{
					// خطأ في تكرار رقم الهاتف
					await _activityService.LogActivityAsync(
						User.Identity?.Name ?? "Unknown",
						"CustomerValidationError",
						$"Duplicate phone number error: {ex.Message}",
						$"Request: Name={request.CustomerName}, Phone={request.CustomerPhone}",
						"Error"
					);
					return Json(new { success = false, message = ex.Message });
				}
				catch (Exception ex)
				{
					// خطأ عام في إنشاء العميل
					await _activityService.LogActivityAsync(
						User.Identity?.Name ?? "Unknown",
						"CustomerValidationError",
						$"Customer validation error: {ex.Message}",
						$"Request: Name={request.CustomerName}, Phone={request.CustomerPhone}",
						"Error"
					);
					return Json(new { success = false, message = "خطأ في بيانات العميل - تأكد من إدخال الاسم ورقم الهاتف" });
				}

				if (customer == null)
				{
					await _activityService.LogActivityAsync(
						User.Identity?.Name ?? "Unknown",
						"CustomerValidationError",
						$"Customer validation returned null",
						$"Request: Name={request.CustomerName}, Phone={request.CustomerPhone}",
						"Error"
					);
					return Json(new { success = false, message = "خطأ في بيانات العميل - تأكد من إدخال الاسم ورقم الهاتف" });
				}

				// التأكد من أن العميل له ID صحيح
				if (customer.Id <= 0)
				{
					await _activityService.LogActivityAsync(
						User.Identity?.Name ?? "Unknown",
						"CustomerValidationError",
						$"Customer has invalid ID: {customer.Id}",
						$"Customer Name: {customer.Name}, Phone: {customer.PhoneNumber}",
						"Error"
					);
					return Json(new { success = false, message = "خطأ في بيانات العميل - ID غير صحيح" });
				}

				await _activityService.LogActivityAsync(
					User.Identity?.Name ?? "Unknown",
					"CashierTransaction",
					$"Customer validated successfully",
					$"Customer: {customer.Name} (ID: {customer.Id}), Phone: {customer.PhoneNumber}",
					"Transaction"
				);

				// Validate return request if there are return items
				var returnValidation = await _cashierService.ValidateReturnRequestAsync(request);
				if (!returnValidation.isValid)
				{
				await _activityService.LogActivityAsync(
					User.Identity?.Name ?? "Unknown",
						"ReturnValidationError",
						$"Return validation failed: {returnValidation.errorMessage}",
						$"Original Invoice: {request.OriginalInvoiceNumber}",
						"Validation"
					);
					return Json(new { success = false, message = returnValidation.errorMessage });
				}

				// Process transaction items (يحدث تحديث المخزون هنا)
				var transactions = await _cashierService.ProcessTransactionItemsAsync(request, customer);
				
				// Calculate totals
				// الشحن للعرض فقط - لا يدخل في أي عمليات حسابية
				var subtotalAmount = transactions.Sum(t => t.Price * t.Quantity); // المبيعات قبل الخصم
				var totalDiscount = transactions.Sum(t => t.Discount);
				var finalTotal = subtotalAmount - totalDiscount; // الإجمالي (بدون الشحن)

				// توزيع المدفوعات على المعاملات
				var totalNetAmount = transactions.Sum(t => t.TotalPrice);
				var actualAmountPaid = request.AmountPaid; // المبلغ المدفوع كما هو
				
				// توزيع المدفوعات بناءً على النسبة الصحيحة (بدون الشحن)
				var totalAmountPaid = 0m;
				
				for (int i = 0; i < transactions.Count; i++)
				{
					var transaction = transactions[i];
					
					if (totalNetAmount > 0)
					{
						// حساب النسبة الصحيحة
						var ratio = transaction.TotalPrice / totalNetAmount;
						var proportionalAmount = ratio * actualAmountPaid;
						
						// للعملية الأخيرة، تأكد من أن المجموع يساوي المطلوب
						if (i == transactions.Count - 1)
						{
							transaction.AmountPaid = Math.Round(actualAmountPaid - totalAmountPaid, 2);
						}
						else
						{
							transaction.AmountPaid = Math.Round(proportionalAmount, 2);
							totalAmountPaid += transaction.AmountPaid;
						}
						
						// الشحن لا يوزع على المعاملات - يبقى صفر
						transaction.ShippingCost = 0;
					}
					else
					{
						transaction.AmountPaid = transaction.TotalPrice;
						transaction.ShippingCost = 0;
					}
				}

				// Save transactions
				_context.CustomerTransactions.AddRange(transactions);
				await _context.SaveChangesAsync();
				
				// Process for daily inventory (بدون تحديث المخزون - تم التحديث مسبقاً)
				foreach (var transaction in transactions)
				{
					await _dailyInventoryService.ProcessTransactionAsync(transaction);
				}
				
				// Create invoice (بدون تحديث المخزون - تم التحديث مسبقاً)
				// استخدام اسم الكاشير من الطلب، أو اسم المستخدم الحالي كبديل
				var cashierName = !string.IsNullOrEmpty(request.CashierName) ? request.CashierName : (User.Identity?.Name ?? "نظام");
				_logger.LogInformation($"Creating invoice with cashier name: {cashierName} (from request: {request.CashierName}, current user: {User.Identity?.Name})");
				var invoice = await _cashierService.CreateInvoiceAsync(request, customer, transactions, cashierName);

				                // Save return tracking for return items
                await _cashierService.SaveReturnTrackingAsync(request);
                
                // Save exchange tracking for exchange operations
                await _cashierService.SaveExchangeTrackingAsync(request);

				// Check for returned items and create warning message
				var returnedItems = request.Items.Where(i => i.Quantity < 0).ToList();
				var soldItems = request.Items.Where(i => i.Quantity > 0).ToList();
				var baseMessage = "تم إنجاز المعاملة بنجاح";
				
				var returnWarning = "";
				if (returnedItems.Any() && soldItems.Any())
				{
					returnWarning = $"\n🔄 تم تسجيل استبدال {returnedItems.Count} منتج بـ {soldItems.Count} منتج. تم تحديث المخزون تلقائياً.";
				}
				else if (returnedItems.Any())
				{
					returnWarning = $"\n📦 تم تسجيل إرجاع {returnedItems.Count} منتج. تم إضافة الكميات للمخزون تلقائياً.";
				}
				else if (soldItems.Any())
				{
					returnWarning = $"\n💰 تم تسجيل بيع {soldItems.Count} منتج. تم خصم الكميات من المخزون تلقائياً.";
				}

				// تسجيل نجاح المعاملة
				await _activityService.LogActivityAsync(
					User.Identity?.Name ?? "Unknown",
					"CashierTransaction",
					$"Transaction completed successfully",
					$"Invoice: {invoice.InvoiceNumber}, Customer: {customer.Name} (ID: {customer.Id}), Total: {finalTotal}",
					"Success"
				);

				return Json(new
				{
					success = true, 
					message = $"{baseMessage}{returnWarning}",
					hasReturns = returnedItems.Any(),
					hasSales = soldItems.Any(),
					hasExchange = returnedItems.Any() && soldItems.Any(),
					returnedItems = returnedItems.Select(i => new
					{
						productName = i.ProductName,
						quantity = Math.Abs(i.Quantity),
						operationType = "إرجاع"
					}).ToList(),
					soldItems = soldItems.Select(i => new
					{
						productName = i.ProductName,
						quantity = i.Quantity,
						operationType = "بيع"
					}).ToList(),
					invoiceId = invoice.Id,
					invoiceNumber = invoice.InvoiceNumber,
					totalAmount = finalTotal,
					customerName = customer.Name,
					customerId = customer.Id,
					itemsCount = request.Items.Count
				});
			}
			catch (Exception ex)
			{
				await _activityService.LogActivityAsync(
					User.Identity?.Name ?? "Unknown",
					"ProcessTransaction",
					$"ERROR: {ex.Message}",
					$"StackTrace: {ex.StackTrace}",
					"Error"
				);

				return Json(new { success = false, message = $"خطأ في معالجة المعاملة: {ex.Message}" });
			}
		}

		// Additional helper endpoints...
		/// <summary>
		/// توليد أرقام فريدة للفاتورة والأمر (مبسط)
		/// </summary>
		[HttpGet]
		public async Task<IActionResult> GenerateUniqueNumbers()
		{
			try
			{
				var invoiceNumber = _cashierService.GenerateInvoiceNumber();
				var orderNumber = _cashierService.GenerateOrderNumber();
				
				await _activityService.LogActivityAsync(
					User.Identity?.Name ?? "Unknown",
					"GenerateNumbers",
					$"Generated numbers: Invoice={invoiceNumber}, Order={orderNumber}",
					$"Timestamp: {DateTime.Now}",
					"System"
				);
				
				return Json(new
				{
					success = true,
					invoiceNumber = invoiceNumber,
					orderNumber = orderNumber,
					message = "تم توليد أرقام فريدة بنجاح"
				});
			}
			catch (Exception ex)
			{
				await _activityService.LogActivityAsync(
					User.Identity?.Name ?? "Unknown",
					"GenerateNumbersError",
					$"Error generating numbers: {ex.Message}",
					$"Timestamp: {DateTime.Now}",
					"Error"
				);
				
				return Json(new
				{
					success = false,
					message = "حدث خطأ في توليد الأرقام: " + ex.Message
				});
			}
		}

		/// <summary>
		/// التحقق من عدم تكرار رقم الفاتورة
		/// </summary>
		[HttpGet]
		public async Task<IActionResult> CheckInvoiceNumberDuplicate(string invoiceNumber)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(invoiceNumber))
				{
					return Json(new { exists = false, message = "الرقم فارغ" });
				}
				
				var exists = await _context.Invoices.AnyAsync(i => i.InvoiceNumber == invoiceNumber.Trim());
				
				await _activityService.LogActivityAsync(
					User.Identity?.Name ?? "Unknown",
					"CheckInvoiceNumber",
					$"Checked invoice number: {invoiceNumber} - Exists: {exists}",
					$"Timestamp: {DateTime.Now}",
					"Validation"
				);
				
				return Json(new { 
					exists = exists, 
					message = exists ? "الرقم مستخدم بالفعل" : "الرقم متاح للاستخدام" 
				});
			}
			catch (Exception ex)
			{
				await _activityService.LogActivityAsync(
					User.Identity?.Name ?? "Unknown",
					"CheckInvoiceNumberError",
					$"Error checking invoice number: {ex.Message}",
					$"Invoice Number: {invoiceNumber}",
					"Error"
				);
				
				return Json(new { exists = false, message = "خطأ في التحقق من الرقم" });
			}
		}

		/// <summary>
		/// التحقق من عدم تكرار رقم الأمر
		/// </summary>
		[HttpGet]
		public async Task<IActionResult> CheckOrderNumberDuplicate(string orderNumber)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(orderNumber))
				{
					return Json(new { exists = false, message = "الرقم فارغ" });
				}
				
				var exists = await _context.Invoices.AnyAsync(i => i.OrderNumber == orderNumber.Trim());
				
				await _activityService.LogActivityAsync(
					User.Identity?.Name ?? "Unknown",
					"CheckOrderNumber",
					$"Checked order number: {orderNumber} - Exists: {exists}",
					$"Timestamp: {DateTime.Now}",
					"Validation"
				);
				
				return Json(new { 
					exists = exists, 
					message = exists ? "الرقم مستخدم بالفعل" : "الرقم متاح للاستخدام" 
				});
			}
			catch (Exception ex)
			{
				await _activityService.LogActivityAsync(
					User.Identity?.Name ?? "Unknown",
					"CheckOrderNumberError",
					$"Error checking order number: {ex.Message}",
					$"Order Number: {orderNumber}",
					"Error"
				);
				
				return Json(new { exists = false, message = "خطأ في التحقق من الرقم" });
			}
		}

		[HttpGet]
		public async Task<IActionResult> GetCustomers(string term = "")
		{
			try
			{
				var query = _context.Customers.AsQueryable();

				if (!string.IsNullOrWhiteSpace(term))
				{
					query = query.Where(c => 
						c.Name.Contains(term) || 
						c.PhoneNumber.Contains(term) ||
						c.Email.Contains(term));
				}

				var customers = await query
					.OrderBy(c => c.Name)
					.Take(20)
					.Select(c => new
					{
						id = c.Id,
						name = c.Name,
						phone = c.PhoneNumber,
						email = c.Email,
						address = c.Address
					})
					.ToListAsync();

				return Json(new { success = true, customers = customers });
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = "خطأ في جلب بيانات العملاء" });
			}
		}

		[HttpGet]
		public async Task<IActionResult> SearchInvoices(string invoiceNumber)
		{
			if (string.IsNullOrWhiteSpace(invoiceNumber))
			{
				return Json(new { success = false, message = "رقم الفاتورة مطلوب" });
			}

			try
			{
				var invoices = await _context.Invoices
					.Include(i => i.Customer)
					.Include(i => i.Items)
					.ThenInclude(ii => ii.Product)
					.Where(i => i.InvoiceNumber.Contains(invoiceNumber))
					.OrderByDescending(i => i.CreatedAt)
					.Take(10)
					.ToListAsync();

				// Process each invoice to exclude already returned items
				var processedInvoices = new List<object>();
				
				foreach (var invoice in invoices)
				{
					// Get already returned items for this invoice
					var returnedItems = await _context.ReturnTrackings
						.Where(r => r.OriginalInvoiceNumber == invoice.InvoiceNumber)
							.ToListAsync();
						
					// Filter out items that have been fully returned
					var availableItems = new List<object>();
					
					foreach (var item in invoice.Items)
					{
						var totalReturned = returnedItems
							.Where(r => r.ProductId == item.ProductId)
							.Sum(r => r.ReturnedQuantity);

						var remainingQuantity = item.Quantity - totalReturned;
						
						// Only include items that still have quantity available for return
						if (remainingQuantity > 0)
						{
							availableItems.Add(new
							{
								productId = item.ProductId,
								productName = item.Product.Name,
								originalQuantity = item.Quantity,
								returnedQuantity = totalReturned,
								availableForReturn = remainingQuantity,
								unitPrice = item.UnitPrice,
								totalPrice = item.UnitPrice * remainingQuantity
						});
					}
				}

					// Only include invoice if it has items available for return
					if (availableItems.Any())
					{
						processedInvoices.Add(new
					{
						id = invoice.Id,
						invoiceNumber = invoice.InvoiceNumber,
							customerName = invoice.Customer.Name,
						totalAmount = invoice.TotalAmount,
							invoiceDate = invoice.InvoiceDate.ToString("yyyy-MM-dd"),
						status = invoice.Status.ToString(),
							items = availableItems
						});
					}
				}

				if (!processedInvoices.Any())
				{
					return Json(new { success = false, message = "لم يتم العثور على فواتير بها منتجات قابلة للإرجاع" });
				}

				return Json(new { success = true, invoices = processedInvoices });
			}
			catch (Exception ex)
			{
				await _activityService.LogActivityAsync(
					User.Identity?.Name ?? "Unknown",
					"SEARCH_INVOICES_ERROR",
					$"Error searching invoices: {invoiceNumber}",
					ex.Message,
					"Error"
				);

				return Json(new { success = false, message = "خطأ في البحث عن الفواتير" });
			}
		}

		// Debug endpoint to check return tracking records
		[HttpGet]
		public async Task<IActionResult> GetReturnTrackings(string? invoiceNumber = null)
		{
			try
			{
				var query = _context.ReturnTrackings.AsQueryable();
				
				if (!string.IsNullOrEmpty(invoiceNumber))
				{
					query = query.Where(r => r.OriginalInvoiceNumber == invoiceNumber || r.ReturnInvoiceNumber == invoiceNumber);
				}

				var trackings = await query
					.OrderByDescending(r => r.CreatedAt)
					.Take(50)
					.Select(r => new
					{
						id = r.Id,
						originalInvoiceNumber = r.OriginalInvoiceNumber,
						returnInvoiceNumber = r.ReturnInvoiceNumber,
						productId = r.ProductId,
						returnedQuantity = r.ReturnedQuantity,
						returnDate = r.ReturnDate.ToString("yyyy-MM-dd HH:mm"),
						returnReason = r.ReturnReason,
						createdBy = r.CreatedBy
					})
					.ToListAsync();

				return Json(new { success = true, trackings = trackings, count = trackings.Count });
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = $"خطأ في جلب سجلات الإرجاع: {ex.Message}" });
			}
		}
	}
}
