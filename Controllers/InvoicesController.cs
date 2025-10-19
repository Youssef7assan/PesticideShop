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
using Microsoft.Extensions.DependencyInjection;
using System.IO;

namespace PesticideShop.Controllers
{
	[Authorize]
	public class InvoicesController : Controller
	{
		private readonly ApplicationDbContext _context;
		private readonly IActivityService _activityService;
		private readonly IDailyInventoryService _dailyInventoryService;
		private readonly ILogger<InvoicesController> _logger;

		public InvoicesController(ApplicationDbContext context, IActivityService activityService, IDailyInventoryService dailyInventoryService, ILogger<InvoicesController> logger)
		{
			_context = context;
			_activityService = activityService;
			_dailyInventoryService = dailyInventoryService;
			_logger = logger;
		}

		// GET: Invoices
		public async Task<IActionResult> Index(
			int page = 1, 
			int pageSize = 10,
			string searchTerm = "",
			string statusFilter = "",
			string typeFilter = "",
			string originFilter = "",
			DateTime? dateFrom = null,
			DateTime? dateTo = null)
		{
			// Build query with filters
			var query = _context.Invoices
				.Include(i => i.Customer)
				.Include(i => i.Items)
				.ThenInclude(ii => ii.Product)
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

			// Get total count for pagination
			var totalInvoices = await query.CountAsync();
			var totalPages = (int)Math.Ceiling((double)totalInvoices / pageSize);

			// Get invoices for current page
			var invoices = await query
				.OrderByDescending(i => i.CreatedAt)
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
				.ToListAsync();

			// Pass filter values to view
			ViewBag.SearchTerm = searchTerm;
			ViewBag.StatusFilter = statusFilter;
			ViewBag.TypeFilter = typeFilter;
			ViewBag.OriginFilter = originFilter;
			ViewBag.DateFrom = dateFrom?.ToString("yyyy-MM-dd");
			ViewBag.DateTo = dateTo?.ToString("yyyy-MM-dd");

			// Create enhanced view model
			var invoiceViewModels = invoices.Select(i => new InvoiceListViewModelEnhanced
			{
				Id = i.Id,
				InvoiceNumber = i.InvoiceNumber,
				OrderNumber = i.OrderNumber,
				CustomerName = i.Customer?.Name ?? "غير محدد",
				CustomerPhone = i.Customer?.PhoneNumber ?? "",
				InvoiceDate = i.InvoiceDate,
				OrderOrigin = i.OrderOrigin,
				Type = i.Type,
				Status = i.Status,
				TotalAmount = i.TotalAmount,
				AmountPaid = i.AmountPaid,
				RemainingAmount = i.GrandTotal - i.AmountPaid,
				GrandTotal = i.GrandTotal,
				ItemsCount = i.Items?.Count ?? 0,
				SalesItemsCount = i.Items?.Count(item => item.Quantity > 0) ?? 0,
				ReturnsItemsCount = i.Items?.Count(item => item.Quantity < 0) ?? 0,
				SalesAmount = i.Items?.Where(item => item.Quantity > 0).Sum(item => item.TotalPrice) ?? 0,
				ReturnsAmount = Math.Abs(i.Items?.Where(item => item.Quantity < 0).Sum(item => item.TotalPrice) ?? 0),
				OriginalInvoiceNumber = i.OriginalInvoiceNumber,
				ReturnReason = i.ReturnReason,
				CreatedAt = i.CreatedAt,
				Notes = i.Notes
			}).ToList();

			// Pass pagination info to view
			ViewBag.CurrentPage = page;
			ViewBag.TotalPages = totalPages;
			ViewBag.PageSize = pageSize;
			ViewBag.TotalInvoices = totalInvoices;

			return View(invoiceViewModels);
		}

		// GET: Invoices/GenerateInvoice/5
		public async Task<IActionResult> GenerateInvoice(int? id)
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

			// البحث عن أحدث فاتورة للعميل
			var latestInvoice = await _context.Invoices
				.Where(i => i.CustomerId == customer.Id)
				.OrderByDescending(i => i.CreatedAt)
				.FirstOrDefaultAsync();

			// Calculate totals with discounts
			var totalPurchases = customer.Transactions?.Sum(t => t.Quantity > 0 ? (t.TotalPrice - t.Discount) : t.TotalPrice) ?? 0; // للإرجاع: لا نخصم الخصم
			var totalPaid = customer.Transactions?.Sum(t => t.AmountPaid) ?? 0;
			var remainingBalance = totalPurchases - totalPaid;

			ViewData["TotalPurchases"] = totalPurchases;
			ViewData["TotalPaid"] = totalPaid;
			ViewData["RemainingBalance"] = remainingBalance;
			ViewData["CashierName"] = latestInvoice?.CashierName;

			return View(customer);
		}

		// GET: Invoices/ModernInvoice/5
		public async Task<IActionResult> ModernInvoice(int? id)
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

			// البحث عن أحدث فاتورة للعميل
			var latestInvoice = await _context.Invoices
				.Where(i => i.CustomerId == customer.Id)
				.OrderByDescending(i => i.CreatedAt)
				.FirstOrDefaultAsync();
			
			_logger.LogInformation($"ModernInvoice - Customer ID: {customer.Id}, Latest Invoice ID: {latestInvoice?.Id}, CashierName: {latestInvoice?.CashierName}");
			
			// فحص جميع الفواتير للعميل
			var allInvoices = await _context.Invoices
				.Where(i => i.CustomerId == customer.Id)
				.ToListAsync();
			
			_logger.LogInformation($"ModernInvoice - All invoices for customer {customer.Id}: {allInvoices.Count} invoices");
			foreach (var inv in allInvoices)
			{
				_logger.LogInformation($"Invoice ID: {inv.Id}, CashierName: '{inv.CashierName}', CreatedAt: {inv.CreatedAt}");
			}

			// Calculate totals with discounts
			var totalPurchases = customer.Transactions?.Sum(t => t.Quantity > 0 ? (t.TotalPrice - t.Discount) : t.TotalPrice) ?? 0; // للإرجاع: لا نخصم الخصم
			var totalPaid = customer.Transactions?.Sum(t => t.AmountPaid) ?? 0;
			var totalDiscount = customer.Transactions?.Sum(t => t.Discount) ?? 0;
			var subtotal = customer.Transactions?.Sum(t => t.TotalPrice) ?? 0;
			var remainingBalance = totalPurchases - totalPaid;

			// تحديد نوع الفاتورة بناءً على المعاملات
			var invoiceType = "فاتورة بيع"; // افتراضي
			var showCashierName = true; // نعرض اسم الكاشير في جميع الفواتير
			
			if (customer.Transactions != null && customer.Transactions.Any())
			{
				var hasSales = customer.Transactions.Any(t => t.Quantity > 0);
				var hasReturns = customer.Transactions.Any(t => t.Quantity < 0);
				
				if (hasReturns && !hasSales)
				{
					invoiceType = "فاتورة إرجاع";
				}
				else if (hasReturns && hasSales)
				{
					invoiceType = "فاتورة استبدال";
				}
				else if (hasSales && !hasReturns)
				{
					invoiceType = "فاتورة بيع";
				}
			}

			ViewData["TotalPurchases"] = totalPurchases;
			ViewData["TotalPaid"] = totalPaid;
			ViewData["TotalDiscount"] = totalDiscount;
			ViewData["Subtotal"] = subtotal;
			ViewData["RemainingBalance"] = remainingBalance;
			ViewData["InvoiceType"] = invoiceType;
			// البحث عن اسم الكاشير من أي فاتورة للعميل
			var cashierName = latestInvoice?.CashierName;
			if (string.IsNullOrEmpty(cashierName))
			{
				// إذا لم نجد اسم الكاشير في أحدث فاتورة، ابحث في جميع الفواتير
				var anyInvoice = await _context.Invoices
					.Where(i => i.CustomerId == customer.Id && !string.IsNullOrEmpty(i.CashierName))
					.OrderByDescending(i => i.CreatedAt)
					.FirstOrDefaultAsync();
				cashierName = anyInvoice?.CashierName;
			}
			
			// إذا لم نجد اسم الكاشير في أي فاتورة، استخدم اسم المستخدم الحالي
			if (string.IsNullOrEmpty(cashierName))
			{
				cashierName = User.Identity?.Name ?? "نظام";
			}

			ViewData["ShowCashierName"] = showCashierName;
			ViewData["CashierName"] = cashierName;
			
			_logger.LogInformation($"ModernInvoice - InvoiceType: {invoiceType}, ShowCashierName: {showCashierName}, CashierName: '{cashierName}', LatestInvoice CashierName: '{latestInvoice?.CashierName}'");

			return View(customer);
		}

		// GET: Invoices/CheckInvoice/5 - فحص بيانات الفاتورة
		public async Task<IActionResult> CheckInvoice(int? id)
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

			var result = new
			{
				InvoiceId = invoice.Id,
				InvoiceNumber = invoice.InvoiceNumber,
				CustomerId = invoice.CustomerId,
				CustomerName = invoice.Customer?.Name,
				CashierName = invoice.CashierName,
				CreatedAt = invoice.CreatedAt,
				TotalAmount = invoice.TotalAmount
			};

			return Json(result);
		}

		// GET: Invoices/Details/5
		public async Task<IActionResult> Details(int? id)
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

			ViewData["CashierName"] = invoice.CashierName;
			return View(invoice);
		}

		// POST: Generate message for WhatsApp
		[HttpPost]
		public async Task<IActionResult> GenerateMessage(int id)
		{
			try
			{
				var invoice = await _context.Invoices
					.Include(i => i.Customer)
					.Include(i => i.Items)
					.ThenInclude(ii => ii.Product)
					.FirstOrDefaultAsync(i => i.Id == id);

				if (invoice == null)
				{
					return Json(new { success = false, message = "الفاتورة غير موجودة" });
				}

				// Generate message using WhatsApp service
				var whatsappService = HttpContext.RequestServices.GetRequiredService<IWhatsAppService>();
				var message = await whatsappService.GenerateWhatsAppMessage(invoice);

				// Log activity
				await _activityService.LogActivityAsync(
					User.Identity?.Name ?? "Unknown",
					"GenerateMessage",
					$"Generated message for invoice: {invoice.InvoiceNumber}",
					$"Message length: {message.Length} characters",
					"Invoice"
				);

				return Json(new { 
					success = true, 
					message = "تم إنشاء الرسالة بنجاح",
					messageContent = message,
					fileName = $"message_{invoice.InvoiceNumber}_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
				});
			}
			catch (Exception ex)
			{
				await _activityService.LogActivityAsync(
					User.Identity?.Name ?? "Unknown",
					"GenerateMessageError",
					$"Error generating message for invoice ID: {id}",
					ex.Message,
					"Error"
				);

				return Json(new { success = false, message = $"خطأ في إنشاء الرسالة: {ex.Message}" });
			}
		}


		// GET: Send WhatsApp message
		[HttpGet]
		public async Task<IActionResult> SendWhatsApp(int id)
		{
			try
			{
				var invoice = await _context.Invoices
					.Include(i => i.Customer)
					.FirstOrDefaultAsync(i => i.Id == id);

				if (invoice == null)
				{
					return NotFound();
				}

				if (string.IsNullOrWhiteSpace(invoice.Customer?.PhoneNumber))
				{
					TempData["ErrorMessage"] = "لا يوجد رقم هاتف للعميل لإرسال الفاتورة";
					return RedirectToAction("Details", "Orders", new { id });
				}

				// Generate message and send via WhatsApp
				var whatsappService = HttpContext.RequestServices.GetRequiredService<IWhatsAppService>();
				
				// إنشاء رسالة نصية مفصلة
				var message = await whatsappService.GenerateWhatsAppMessage(invoice);
				
				// إنشاء رابط WhatsApp مع الرسالة
				var whatsappUrl = whatsappService.GetWhatsAppUrl(invoice.Customer.PhoneNumber, message);

				// Log activity
				await _activityService.LogActivityAsync(
					User.Identity?.Name ?? "Unknown",
					"SendWhatsAppMessage",
					$"Sent WhatsApp message for invoice: {invoice.InvoiceNumber}",
					$"Customer: {invoice.Customer.Name}, Phone: {invoice.Customer.PhoneNumber}, Message length: {message.Length}",
					"Invoice"
				);

				// Return JavaScript to open WhatsApp in new tab
				var script = $@"
					<script>
						// فتح واتساب في تبويب جديد
						window.open('{whatsappUrl}', '_blank', 'noopener,noreferrer');
						
						// إظهار رسالة نجاح
						alert('تم فتح واتساب في تبويب جديد لإرسال الفاتورة للعميل: {invoice.Customer.Name}');
						
						// العودة للصفحة السابقة
						window.history.back();
					</script>";
				return Content(script, "text/html");
			}
			catch (Exception ex)
			{
				await _activityService.LogActivityAsync(
					User.Identity?.Name ?? "Unknown",
					"SendWhatsAppError",
					$"Error sending WhatsApp message for invoice ID: {id}",
					ex.Message,
					"Error"
				);

				// Return error script
				var errorScript = $@"
					<script>
						alert('حدث خطأ في إرسال الفاتورة عبر واتساب: {ex.Message}');
						window.history.back();
					</script>";
				return Content(errorScript, "text/html");
			}
		}

		// DELETE: Delete single invoice
		[HttpDelete]
		public async Task<IActionResult> Delete(int id)
		{
			try
			{
				var invoice = await _context.Invoices
					.Include(i => i.Items)
					.FirstOrDefaultAsync(i => i.Id == id);

				if (invoice == null)
				{
					return Json(new { success = false, message = "الفاتورة غير موجودة" });
				}

				// لا نرجع المنتجات للمخزون عند حذف الفواتير
				// لأن الفواتير تمثل مبيعات فعلية تمت بالفعل

				// Log the deletion activity
				await _activityService.LogActivityAsync(
					User.Identity?.Name ?? "Unknown",
					"DeleteInvoice",
					$"DELETED INVOICE: {invoice.InvoiceNumber}",
					$"Invoice ID: {id}, Customer: {invoice.Customer?.Name}, Total: {invoice.TotalAmount}",
					"Critical"
				);

				_context.Invoices.Remove(invoice);
				await _context.SaveChangesAsync();

				return Json(new { success = true, message = "تم حذف الفاتورة بنجاح" });
			}
			catch (Exception ex)
			{
				// Log the error
				await _activityService.LogActivityAsync(
					User.Identity?.Name ?? "Unknown",
					"DeleteInvoiceError",
					$"ERROR DELETING INVOICE: {ex.Message}",
					$"Invoice ID: {id}, User: {User.Identity?.Name}",
					"Error"
				);

				return Json(new { success = false, message = $"فشل في حذف الفاتورة: {ex.Message}" });
			}
		}

		// DELETE: Delete all invoices
		[HttpDelete]
		public async Task<IActionResult> DeleteAll()
		{
			try
			{
				// Get all invoices with their related data
				var invoices = await _context.Invoices
					.Include(i => i.Items)
					.ToListAsync();

				if (!invoices.Any())
				{
					return Json(new { success = true, message = "لا توجد فواتير للحذف" });
				}

				var invoiceCount = invoices.Count;

				// Log the deletion activity
				await _activityService.LogActivityAsync(
					User.Identity?.Name ?? "Unknown",
					"DeleteAllInvoices",
					$"DELETED ALL INVOICES: {invoiceCount} invoices deleted",
					$"User: {User.Identity?.Name}, Time: {DateTime.Now}",
					"Critical"
				);

				// لا نرجع المنتجات للمخزون عند حذف الفواتير
				// لأن الفواتير تمثل مبيعات فعلية تمت بالفعل

				// Delete all invoices (cascade delete will handle related data)
				_context.Invoices.RemoveRange(invoices);
				await _context.SaveChangesAsync();

				return Json(new { success = true, message = $"تم حذف جميع الفواتير بنجاح ({invoiceCount} فاتورة)" });
			}
			catch (Exception ex)
			{
				// Log the error
				await _activityService.LogActivityAsync(
					User.Identity?.Name ?? "Unknown",
					"DeleteAllInvoicesError",
					$"ERROR DELETING ALL INVOICES: {ex.Message}",
					$"User: {User.Identity?.Name}, Time: {DateTime.Now}",
					"Error"
				);

				return Json(new { success = false, message = $"فشل في حذف الفواتير: {ex.Message}" });
			}
		}

		[HttpPost]
		public async Task<IActionResult> UpdateInvoiceStatus(int id, int status)
		{
			try
			{
				var invoice = await _context.Invoices.FindAsync(id);
				if (invoice == null)
				{
					return Json(new { success = false, message = "الفاتورة غير موجودة" });
				}

				invoice.Status = (InvoiceStatus)status;
				invoice.UpdatedAt = DateTime.Now;
				
				await _context.SaveChangesAsync();
				
				return Json(new { success = true, message = "تم تحديث حالة الفاتورة بنجاح" });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "خطأ في تحديث حالة الفاتورة {InvoiceId}", id);
				return Json(new { success = false, message = "حدث خطأ في تحديث حالة الفاتورة" });
			}
		}

		// GET: Invoices/UpdatePayment/5
		public async Task<IActionResult> UpdatePayment(int? id)
		{
			if (id == null)
			{
				return NotFound();
			}

			var invoice = await _context.Invoices
				.Include(i => i.Customer)
				.Include(i => i.Items)
				.ThenInclude(item => item.Product)
				.FirstOrDefaultAsync(i => i.Id == id);

			if (invoice == null)
			{
				return NotFound();
			}

			return View(invoice);
		}

		// POST: Invoices/UpdatePayment/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> UpdatePayment(int id, decimal? additionalAmount, int? shippingType)
		{
			try
			{
				var invoice = await _context.Invoices.FindAsync(id);
				if (invoice == null)
				{
					return NotFound();
				}

				bool paymentUpdated = false;
				bool shippingUpdated = false;

				// إضافة المبلغ الجديد للمبلغ المدفوع (إذا تم تحديده)
				if (additionalAmount.HasValue && additionalAmount.Value > 0)
				{
					invoice.AmountPaid += additionalAmount.Value;
					paymentUpdated = true;
					
					// تحديث المدفوعات في المعاملات المرتبطة بالفاتورة
					await UpdateCustomerTransactionsPaymentAsync(invoice, additionalAmount.Value);
				}
				
				// تحديث نوع الشحن إذا تم تحديده
				if (shippingType.HasValue)
				{
					invoice.ShippingType = (ShippingType)shippingType.Value;
					shippingUpdated = true;
				}
				
				// تحديث المبلغ المتبقي
				// الشحن للعرض فقط - لا يدخل في العمليات الحسابية
				invoice.RemainingAmount = invoice.TotalAmount - invoice.AmountPaid;
				
				// تحديث حالة الفاتورة
				if (invoice.RemainingAmount <= 0)
				{
					invoice.Status = InvoiceStatus.Paid;
				}
				else if (invoice.AmountPaid > 0)
				{
					invoice.Status = InvoiceStatus.PartiallyPaid;
				}
				
				invoice.UpdatedAt = DateTime.Now;
				
				await _context.SaveChangesAsync();
				
				// تسجيل النشاط
				if (paymentUpdated)
				{
					await _activityService.LogActivityAsync(
						User.Identity?.Name ?? "System",
						"UpdatePayment",
						$"تم تحديث المدفوعات للفاتورة {invoice.InvoiceNumber}",
						$"مبلغ إضافي: {additionalAmount?.ToString("N2")} ج.م، العميل: {invoice.Customer?.Name}",
						"Invoice"
					);
				}
				
				// إنشاء رسالة النجاح المناسبة
				string successMessage = "";
				if (paymentUpdated && shippingUpdated)
				{
					successMessage = $"تم إضافة {additionalAmount?.ToString("N2")} ج.م للمبلغ المدفوع وتحديث نوع الشحن إلى {GetShippingTypeName((ShippingType)shippingType.Value)} بنجاح";
				}
				else if (paymentUpdated)
				{
					successMessage = $"تم إضافة {additionalAmount?.ToString("N2")} ج.م للمبلغ المدفوع بنجاح";
				}
				else if (shippingUpdated)
				{
					successMessage = $"تم تحديث نوع الشحن إلى {GetShippingTypeName((ShippingType)shippingType.Value)} بنجاح";
				}
				else
				{
					successMessage = "لم يتم إجراء أي تغييرات";
				}
				
				TempData["SuccessMessage"] = successMessage;
				return RedirectToAction("Details", "Orders", new { id = invoice.Id });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "خطأ في تحديث المبلغ المدفوع للفاتورة {InvoiceId}", id);
				TempData["ErrorMessage"] = "حدث خطأ في تحديث البيانات";
				return RedirectToAction(nameof(Index));
			}
		}
		
		/// <summary>
		/// تحديث المدفوعات في المعاملات المرتبطة بالفاتورة
		/// </summary>
		private async Task UpdateCustomerTransactionsPaymentAsync(Invoice invoice, decimal additionalAmount)
		{
			try
			{
				// إنشاء معاملة جديدة للمدفوعات الإضافية بتاريخ اليوم
				// استخدام أول منتج متاح كمنتج افتراضي للمدفوعات
				var defaultProduct = await _context.Products.FirstOrDefaultAsync();
				if (defaultProduct == null)
				{
					_logger.LogError("لا يوجد منتجات في قاعدة البيانات لإنشاء معاملة دفع");
					return;
				}

				// الدفعات الإضافية تذهب للمنتجات فقط (بدون الشحن)
				// الشحن لا يُحسب في المدفوعات - يبقى جزء من RemainingAmount
				var actualPaymentAmount = additionalAmount;
				
				// إنشاء معاملة للمبلغ المدفوع (بدون شحن)
				if (actualPaymentAmount > 0)
				{
					var paymentTransaction = new CustomerTransaction
					{
						CustomerId = invoice.CustomerId,
						ProductId = defaultProduct.Id, // استخدام منتج افتراضي
						Quantity = 0, // كمية صفر للمدفوعات
						Price = 0, // سعر صفر للمدفوعات
						TotalPrice = 0, // إجمالي صفر للمدفوعات
						Discount = 0, // بدون خصم
						ShippingCost = 0, // بدون شحن
						AmountPaid = actualPaymentAmount, // المبلغ المدفوع (بدون الشحن)
						Date = DateTime.Now, // تاريخ اليوم
						Notes = $"دفعة إضافية للفاتورة {invoice.InvoiceNumber}"
					};
					
					_context.CustomerTransactions.Add(paymentTransaction);
					
					// تحديث الجرد اليومي للمعاملة الجديدة
					try
					{
						await _context.SaveChangesAsync();
						await _dailyInventoryService.ProcessTransactionAsync(paymentTransaction);
						_logger.LogInformation("تم إنشاء معاملة جديدة للمدفوعات للفاتورة {InvoiceId} بمبلغ {Amount}", 
							invoice.Id, actualPaymentAmount);
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "خطأ في تحديث الجرد اليومي للمعاملة الجديدة");
					}
				}
				
				// ملاحظة: الشحن لا يتم إنشاء معاملة منفصلة له
				// الشحن يبقى جزء من RemainingAmount في الفاتورة
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "خطأ في تحديث المدفوعات للفاتورة {InvoiceId}", invoice.Id);
			}
		}
		
		// دالة مساعدة لتحويل نوع الشحن إلى نص
		private string GetShippingTypeName(ShippingType shippingType)
		{
			return shippingType switch
			{
				ShippingType.Bosta => "بوسطا",
				ShippingType.Cairo => "كايرو",
				ShippingType.NoShipping => "بدون شحن",
				_ => "غير محدد"
			};
		}
	}
	
	// Enhanced Invoice List View Model
	public class InvoiceListViewModelEnhanced
	{
		public int Id { get; set; }
		public string InvoiceNumber { get; set; } = "";
		public string OrderNumber { get; set; } = "";
		public string CustomerName { get; set; } = "";
		public string CustomerPhone { get; set; } = "";
		public DateTime InvoiceDate { get; set; }
		public OrderOrigin OrderOrigin { get; set; }
		public InvoiceType Type { get; set; }
		public InvoiceStatus Status { get; set; }
		public decimal TotalAmount { get; set; }
		public decimal AmountPaid { get; set; }
		public decimal RemainingAmount { get; set; }
		public decimal GrandTotal { get; set; }
		public int ItemsCount { get; set; }
		public int SalesItemsCount { get; set; }
		public int ReturnsItemsCount { get; set; }
		public decimal SalesAmount { get; set; }
		public decimal ReturnsAmount { get; set; }
		public string? OriginalInvoiceNumber { get; set; }
		public string? ReturnReason { get; set; }
		public DateTime CreatedAt { get; set; }
		public string? Notes { get; set; }
		
		// Calculated properties
		public string StatusDisplayText
		{
			get
			{
				return Status switch
				{
					InvoiceStatus.Draft => "📝 مسودة",
					InvoiceStatus.Sent => "📤 مرسل",
					InvoiceStatus.Paid => "✅ مدفوع",
					InvoiceStatus.PartiallyPaid => "💰 مدفوع جزئياً",
					InvoiceStatus.Overdue => "⏰ متأخر",
					InvoiceStatus.Cancelled => "❌ ملغي",
					InvoiceStatus.Pending => "⏳ مؤجل",
					InvoiceStatus.UnderDelivery => "🚚 تحت التسليم",
					InvoiceStatus.NotDelivered => "❌ لم يسلم",
					InvoiceStatus.Delivered => "✅ تم التسليم",
					_ => "غير محدد"
				};
			}
		}
		
		public string StatusColor
		{
			get
			{
				return Status switch
				{
					InvoiceStatus.Paid => "success",
					InvoiceStatus.Delivered => "success",
					InvoiceStatus.PartiallyPaid => "warning",
					InvoiceStatus.Pending => "warning",
					InvoiceStatus.Sent => "info",
					InvoiceStatus.UnderDelivery => "info",
					InvoiceStatus.Overdue => "danger",
					InvoiceStatus.NotDelivered => "danger",
					InvoiceStatus.Cancelled => "secondary",
					InvoiceStatus.Draft => "light",
					_ => "secondary"
				};
			}
		}
		
		public string TypeDisplayText
		{
			get
			{
				return Type switch
				{
					InvoiceType.Sale => "بيع",
					InvoiceType.Return => "إرجاع",
					InvoiceType.Exchange => "استبدال",
					InvoiceType.Maintenance => "صيانة",
					InvoiceType.Estimate => "تقديرية",
					_ => "غير محدد"
				};
			}
		}
		
		public string TypeColor
		{
			get
			{
				return Type switch
				{
					InvoiceType.Sale => "primary",
					InvoiceType.Return => "warning",
					InvoiceType.Exchange => "info",
					InvoiceType.Maintenance => "secondary",
					InvoiceType.Estimate => "light",
					_ => "secondary"
				};
			}
		}
		
		public bool IsFullyPaid => RemainingAmount <= 0;
		public bool IsPartiallyPaid => AmountPaid > 0 && RemainingAmount > 0;
		public bool IsUnpaid => AmountPaid <= 0;
	}
	}

