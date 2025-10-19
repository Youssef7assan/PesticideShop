using System.ComponentModel.DataAnnotations;

namespace PesticideShop.Models
{
    public class MultiExchangeRequest
    {
        [Required(ErrorMessage = "رقم الفاتورة الأصلية مطلوب")]
        public string OriginalInvoiceNumber { get; set; } = "";

        [Required(ErrorMessage = "رقم فاتورة الاستبدال مطلوب")]
        public string ExchangeInvoiceNumber { get; set; } = "";

        [Required(ErrorMessage = "يجب إضافة منتج واحد على الأقل للاستبدال")]
        [MinLength(1, ErrorMessage = "يجب إضافة منتج واحد على الأقل للاستبدال")]
        public List<ExchangeItem> ExchangeItems { get; set; } = new List<ExchangeItem>();

        public string? ExchangeReason { get; set; }
        public string? Notes { get; set; }
    }

    public class ExchangeItem
    {
        [Required(ErrorMessage = "المنتج القديم مطلوب")]
        public int OldProductId { get; set; }

        [Required(ErrorMessage = "المنتج الجديد مطلوب")]
        public int NewProductId { get; set; }

        [Required(ErrorMessage = "الكمية مطلوبة")]
        [Range(1, int.MaxValue, ErrorMessage = "الكمية يجب أن تكون أكبر من صفر")]
        public int ExchangedQuantity { get; set; }

        // معلومات إضافية للعرض
        public string? OldProductName { get; set; }
        public string? NewProductName { get; set; }
        public decimal? OldProductPrice { get; set; }
        public decimal? NewProductPrice { get; set; }
        public decimal? PriceDifference { get; set; }
    }
}
