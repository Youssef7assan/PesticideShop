using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PesticideShop.Models
{
    public class Customer
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم العميل مطلوب")]
        [StringLength(100, ErrorMessage = "اسم العميل يجب أن يكون أقل من 100 حرف")]
        [Display(Name = "اسم العميل")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "رقم الموبايل مطلوب")]
        [StringLength(20, ErrorMessage = "رقم الموبايل يجب أن يكون أقل من 20 رقم")]
        [Display(Name = "رقم الموبايل")]
        public string PhoneNumber { get; set; } = string.Empty;

        [StringLength(20, ErrorMessage = "الرقم الإضافي يجب أن يكون أقل من 20 رقم")]
        [Display(Name = "رقم إضافي")]
        public string? AdditionalPhone { get; set; }

        [StringLength(50, ErrorMessage = "اسم المحافظة يجب أن يكون أقل من 50 حرف")]
        [Display(Name = "المحافظة")]
        public string? Governorate { get; set; }

        [StringLength(50, ErrorMessage = "اسم المنطقة يجب أن يكون أقل من 50 حرف")]
        [Display(Name = "المنطقة")]
        public string? District { get; set; }

        [StringLength(200, ErrorMessage = "العنوان يجب أن يكون أقل من 200 حرف")]
        [Display(Name = "العنوان التفصيلي")]
        public string? DetailedAddress { get; set; }

        [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صحيح")]
        [StringLength(100, ErrorMessage = "البريد الإلكتروني يجب أن يكون أقل من 100 حرف")]
        [Display(Name = "البريد الإلكتروني")]
        public string? Email { get; set; }

        [StringLength(200, ErrorMessage = "العنوان يجب أن يكون أقل من 200 حرف")]
        [Display(Name = "العنوان القديم (للتوافق)")]
        public string Address { get; set; } = string.Empty;

        [Display(Name = "تاريخ الإنشاء")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        public ICollection<CustomerTransaction>? Transactions { get; set; }

        // Computed properties
        [Display(Name = "العنوان الكامل")]
        public string FullAddress
        {
            get
            {
                var parts = new List<string>();
                
                if (!string.IsNullOrEmpty(Governorate))
                    parts.Add(Governorate);
                    
                if (!string.IsNullOrEmpty(District))
                    parts.Add(District);
                    
                if (!string.IsNullOrEmpty(DetailedAddress))
                    parts.Add(DetailedAddress);

                return parts.Count > 0 ? string.Join(" - ", parts) : Address;
            }
        }

        [Display(Name = "أرقام الهاتف")]
        public string PhoneNumbers
        {
            get
            {
                if (!string.IsNullOrEmpty(AdditionalPhone))
                    return $"{PhoneNumber} / {AdditionalPhone}";
                return PhoneNumber;
            }
        }

        [Display(Name = "عدد المعاملات")]
        public int TransactionsCount => Transactions?.Count ?? 0;

        [Display(Name = "إجمالي المشتريات")]
        public decimal TotalPurchases => Transactions?.Sum(t => t.Quantity > 0 ? (t.TotalPrice - t.Discount) : t.TotalPrice) ?? 0; // للإرجاع: لا نخصم الخصم

        [Display(Name = "إجمالي المدفوع")]
        public decimal TotalPaid => Transactions?.Sum(t => t.AmountPaid) ?? 0;

        [Display(Name = "الرصيد المتبقي")]
        public decimal RemainingBalance => TotalPurchases - TotalPaid;

        [Display(Name = "حالة الحساب")]
        public string AccountStatus
        {
            get
            {
                if (RemainingBalance <= 0) return "مدفوع بالكامل";
                if (RemainingBalance <= 1000) return "مستقر";
                return "مستحق";
            }
        }
    }
} 