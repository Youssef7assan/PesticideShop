using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PesticideShop.Models
{
    public class ExchangeTracking
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [Display(Name = "رقم الفاتورة الأصلية")]
        public string OriginalInvoiceNumber { get; set; } = "";
        
        [Required]
        [Display(Name = "رقم فاتورة الاستبدال")]
        public string ExchangeInvoiceNumber { get; set; } = "";
        
        [Required]
        [Display(Name = "المنتج القديم")]
        public int OldProductId { get; set; }
        public Product OldProduct { get; set; } = null!;
        
        [Required]
        [Display(Name = "المنتج الجديد")]
        public int NewProductId { get; set; }
        public Product NewProduct { get; set; } = null!;
        
        [Required]
        [Display(Name = "الكمية المستبدلة")]
        public int ExchangedQuantity { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "فرق السعر")]
        public decimal PriceDifference { get; set; }
        
        [Display(Name = "سبب الاستبدال")]
        public string? ExchangeReason { get; set; }
        
        [Required]
        [Display(Name = "تاريخ الاستبدال")]
        public DateTime ExchangeDate { get; set; } = DateTime.Now;
        
        [Required]
        [Display(Name = "أنشئ بواسطة")]
        public string CreatedBy { get; set; } = "";
        
        [Display(Name = "تاريخ الإنشاء")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }
        
        // Navigation properties
        [ForeignKey("OldProductId")]
        public virtual Product? OldProductNavigation { get; set; }
        
        [ForeignKey("NewProductId")]
        public virtual Product? NewProductNavigation { get; set; }
    }
}
