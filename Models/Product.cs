using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PesticideShop.Extensions;

namespace PesticideShop.Models
{
    public enum ProductSize
    {
        [Display(Name = "32")]
        Size32,
        [Display(Name = "33")]
        Size33,
        [Display(Name = "34")]
        Size34,
        [Display(Name = "35")]
        Size35,
        [Display(Name = "36")]
        Size36,
        [Display(Name = "37")]
        Size37,
        [Display(Name = "38")]
        Size38,
        [Display(Name = "39")]
        Size39,
        [Display(Name = "40")]
        Size40,
        [Display(Name = "41")]
        Size41,
        [Display(Name = "42")]
        Size42,
        [Display(Name = "43")]
        Size43,
        [Display(Name = "44")]
        Size44,
        [Display(Name = "XS")]
        XS,
        [Display(Name = "S")]
        S,
        [Display(Name = "M")]
        M,
        [Display(Name = "L")]
        L,
        [Display(Name = "XL")]
        XL,
        [Display(Name = "XXL")]
        XXL,
        [Display(Name = "XXXL")]
        XXXL,
        [Display(Name = "موحد")]
        OneSize
    }

    public class Product
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "رمز QR مطلوب")]
        [StringLength(50, ErrorMessage = "رمز QR يجب أن يكون أقل من 50 حرف")]
        [Display(Name = "رمز QR")]
        public string QRCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "اسم المنتج مطلوب")]
        [StringLength(100, ErrorMessage = "اسم المنتج يجب أن يكون أقل من 100 حرف")]
        [Display(Name = "اسم المنتج")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "الصنف مطلوب")]
        [Display(Name = "الصنف")]
        public int CategoryId { get; set; }

        [ForeignKey("CategoryId")]
        public Category? Category { get; set; }

        [Required(ErrorMessage = "اللون مطلوب")]
        [StringLength(30, ErrorMessage = "اللون يجب أن يكون أقل من 30 حرف")]
        [Display(Name = "اللون")]
        public string Color { get; set; } = string.Empty;

        [Required(ErrorMessage = "المقاس مطلوب")]
        [Display(Name = "المقاس")]
        public ProductSize Size { get; set; }

        [Required(ErrorMessage = "الكمية مطلوبة")]
        [Range(0, int.MaxValue, ErrorMessage = "الكمية يجب أن تكون أكبر من أو تساوي صفر")]
        [Display(Name = "الكمية المتوفرة")]
        public int Quantity { get; set; }

        [Required(ErrorMessage = "سعر البيع مطلوب")]
        [Range(0.01, double.MaxValue, ErrorMessage = "سعر البيع يجب أن يكون أكبر من صفر")]
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "سعر البيع")]
        public decimal Price { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "سعر التكلفة")]
        public decimal CostPrice { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "سعر الجمله يجب أن يكون أكبر من أو يساوي صفر")]
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "سعر الجمله (اختياري)")]
        public decimal? CartonPrice { get; set; }

        [Display(Name = "تاريخ الإضافة")]
        public DateTime DateAdded { get; set; } = DateTime.Now;

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        [Display(Name = "متوفر بألوان أخرى")]
        public bool HasMultipleColors { get; set; } = false;

        [Display(Name = "متوفر بمقاسات أخرى")]
        public bool HasMultipleSizes { get; set; } = false;

        [Display(Name = "الألوان المتوفرة")]
        [StringLength(200, ErrorMessage = "الألوان المتوفرة يجب أن تكون أقل من 200 حرف")]
        public string? AvailableColors { get; set; }

        [Display(Name = "المقاسات المتوفرة")]
        [StringLength(200, ErrorMessage = "المقاسات المتوفرة يجب أن تكون أقل من 200 حرف")]
        public string? AvailableSizes { get; set; }

        // Navigation properties
        public ICollection<CustomerTransaction>? CustomerTransactions { get; set; }

        // Computed properties
        [Display(Name = "حالة المخزون")]
        public string StockStatus
        {
            get
            {
                if (Quantity == 0)
                    return "نفذ من المخزون";
                else if (Quantity <= 10)
                    return "مخزون منخفض";
                else
                    return "متوفر";
            }
        }

        [Display(Name = "معلومات المخزون")]
        public string StockInfo
        {
            get
            {
                if (Quantity == 0)
                    return "لا توجد قطع متوفرة";
                else if (Quantity <= 10)
                    return $"متبقي {Quantity} قطع فقط";
                else
                    return $"متوفر {Quantity} قطعة";
            }
        }

        [Display(Name = "القيمة الإجمالية")]
        public decimal TotalValue => Quantity * Price;

        [Display(Name = "إجمالي القطع")]
        public int TotalPieces => Quantity;

        [Display(Name = "معلومات المنتج الكاملة")]
        public string FullProductInfo => $"{Name} - {Category?.Name} - {Color} - {Size.GetDisplayName()}";

        [Display(Name = "رمز المنتج")]
        public string ProductCode => $"P{Id:D6}";

        [Display(Name = "هامش الربح")]
        public decimal ProfitMargin => CartonPrice.HasValue && Quantity > 0 ? Price - (CartonPrice.Value / Quantity) : 0;

        [Display(Name = "نسبة الربح")]
        public decimal ProfitPercentage => (CartonPrice.HasValue && CartonPrice.Value > 0 && Price > 0 && Quantity > 0) ? ((Price - (CartonPrice.Value / Quantity)) / (CartonPrice.Value / Quantity)) * 100 : 0;
    }
} 