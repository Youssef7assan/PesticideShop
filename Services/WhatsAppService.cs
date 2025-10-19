using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using PesticideShop.Models;
using PesticideShop.Data;
using Microsoft.EntityFrameworkCore;

namespace PesticideShop.Services
{
    public interface IWhatsAppService
    {
        Task<string> GenerateWhatsAppMessage(Invoice invoice);
        string GetWhatsAppUrl(string phoneNumber, string message);
        Task<bool> SendInvoicePdfViaWhatsAppAsync(Invoice invoice, string phoneNumber);
    }

    public class WhatsAppService : IWhatsAppService
    {
        private readonly ApplicationDbContext _context;

        public WhatsAppService(ApplicationDbContext context)
        {
            _context = context;
        }


        /// <summary>
        /// إنشاء رسالة واتساب للفاتورة
        /// </summary>
        public async Task<string> GenerateWhatsAppMessage(Invoice invoice)
        {
            try
            {
                // تحميل بيانات العميل والمنتجات
                var customer = await _context.Customers.FindAsync(invoice.CustomerId);
                var invoiceItems = await _context.InvoiceItems
                    .Include(ii => ii.Product)
                    .Where(ii => ii.InvoiceId == invoice.Id)
                    .ToListAsync();

                // جلب أرقام المعاملات المرتبطة بنفس تاريخ الفاتورة ولنفس العميل
                var startDate = invoice.InvoiceDate.Date;
                var endDate = invoice.InvoiceDate.Date.AddDays(1).AddTicks(-1);
                var relatedTransactions = await _context.CustomerTransactions
                    .Where(t => t.CustomerId == invoice.CustomerId && t.Date >= startDate && t.Date <= endDate)
                    .OrderBy(t => t.Id)
                    .Select(t => t.Id)
                    .ToListAsync();

                var message = $"";
                
                message += $"مرحباً *{customer?.Name ?? "العميل العزيز"}*! 👋\n\n";
                message += $"🧾 *فاتورة رقم:* {invoice.InvoiceNumber}\n";
                message += $"📅 *التاريخ:* {invoice.InvoiceDate:dd/MM/yyyy}\n";
                message += $"⏰ *وقت الإنشاء:* {invoice.CreatedAt:dd/MM/yyyy HH:mm}\n";
                if (relatedTransactions.Any())
                {
                    var txNumbers = string.Join(", ", relatedTransactions.Select(id => "#" + id));
                    message += $"🔢 *أرقام المعاملات:* {txNumbers}\n";
                }
                
                message += $"\n🛍️ *المنتجات:*\n";
                message += "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n";
                
                foreach (var item in invoiceItems)
                {
                    var lineGross = item.UnitPrice * item.Quantity;
                    var lineNet = lineGross - item.Discount;

                    message += $"• *{item.Product?.Name ?? "غير محدد"}*\n";
                    if (!string.IsNullOrEmpty(item.Color) || !string.IsNullOrEmpty(item.Size))
                    {
                        var colorSize = "";
                        if (!string.IsNullOrEmpty(item.Color))
                            colorSize += $"🎨 لون: {item.Color}";
                        if (!string.IsNullOrEmpty(item.Size))
                        {
                            if (!string.IsNullOrEmpty(colorSize))
                                colorSize += " - ";
                            colorSize += $"📏 مقاس: {item.Size}";
                        }
                        message += $"  {colorSize}\n";
                    }
                    message += $"  📦 الكمية: {item.Quantity}\n";
                    message += $"  💰 سعر الوحدة: {item.UnitPrice:N2} EG\n";
                    message += $"  🧮 الإجمالي قبل الخصم: {lineGross:N2} EG\n";
                    if (item.Discount > 0)
                        message += $"  🎯 الخصم: {item.Discount:N2} EG\n";
                    message += $"  💵 الصافي: {lineNet:N2} EG\n\n";
                }
                
                var subTotalBeforeDiscount = invoiceItems.Sum(ii => ii.UnitPrice * ii.Quantity);
                var totalDiscount = invoiceItems.Sum(ii => ii.Discount);
                var finalTotal = subTotalBeforeDiscount - totalDiscount + invoice.ShippingCost;

                message += "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n";
                message += $"📊 *المجموع قبل الخصم:* {subTotalBeforeDiscount:N2} EG\n";
                if (totalDiscount > 0)
                    message += $"🎯 *إجمالي الخصم:* {totalDiscount:N2} EG\n";
                if (invoice.ShippingCost > 0)
                    message += $"🚚 *الشحن:* {invoice.ShippingCost:N2} EG\n";
                message += $"💰 *الإجمالي:* {finalTotal:N2} EG\n";
                message += $"✅ *المدفوع:* {invoice.AmountPaid:N2} EG\n";
                var remaining = finalTotal - invoice.AmountPaid;
                message += $"⏳ *المتبقي:* {remaining:N2} EG\n\n";
                
                if (!string.IsNullOrEmpty(invoice.PaymentMethod))
                    message += $"💳 *طريقة الدفع:* {invoice.PaymentMethod}\n";
                
                if (!string.IsNullOrEmpty(invoice.Notes))
                    message += $"📝 *ملاحظات:* {invoice.Notes}\n";
                
                message += $"\n📞 *للاستفسارات:*\n";
                message += $"01125011078 / 01015625250\n";
                message += $"🌐 *الموقع:* www.bypharaonic.com\n\n";
                message += $"🙏 *Quality is our priority*";

                return message;
            }
            catch (Exception ex)
            {
                // في حالة حدوث خطأ، إرجاع رسالة بسيطة
                return $"مرحباً! تم إنشاء فاتورة رقم {invoice.InvoiceNumber} بتاريخ {invoice.InvoiceDate:dd/MM/yyyy}. للاستفسارات: 01125011078";
            }
        }

        /// <summary>
        /// إنشاء رابط واتساب
        /// </summary>
        public string GetWhatsAppUrl(string phoneNumber, string message)
        {
            var cleanPhone = CleanPhoneNumber(phoneNumber);
            var encodedMessage = Uri.EscapeDataString(message);
            return $"https://wa.me/{cleanPhone}?text={encodedMessage}";
        }

        /// <summary>
        /// إرسال فاتورة عبر WhatsApp كرسالة نصية
        /// </summary>
        public async Task<bool> SendInvoicePdfViaWhatsAppAsync(Invoice invoice, string phoneNumber)
        {
            try
            {
                // إنشاء رسالة نصية مفصلة
                var message = await GenerateWhatsAppMessage(invoice);
                
                // إنشاء رابط WhatsApp مع الرسالة
                var whatsappUrl = GetWhatsAppUrl(phoneNumber, message);
                
                // في التطبيق الحقيقي، هنا يمكن استخدام WhatsApp Business API
                // أو إرسال الرسالة عبر خدمة خارجية مثل:
                // - WhatsApp Business API
                // - Twilio WhatsApp API
                // - MessageBird WhatsApp API
                
                // مثال على استخدام WhatsApp Business API:
                // await SendMessageViaWhatsAppBusinessAPI(phoneNumber, message);
                
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"خطأ في إرسال الرسالة عبر WhatsApp: {ex.Message}");
            }
        }


        #region Private Methods

        private string CleanPhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return string.Empty;

            // إزالة جميع الأحرف غير الرقمية
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

        #endregion
    }
}
