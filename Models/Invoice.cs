using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PesticideShop.Models
{
    public class Invoice
    {
        public int Id { get; set; }

        [Required]
        public int CustomerId { get; set; }
        public Customer? Customer { get; set; }

        [Required]
        [Display(Name = "رقم الفاتورة")]
        public string InvoiceNumber { get; set; } = string.Empty;

        [Required]
        [Display(Name = "رقم الطلب")]
        public string OrderNumber { get; set; } = string.Empty;

        [Display(Name = "رقم البوليصة")]
        public string? PolicyNumber { get; set; }

        [Display(Name = "طريقة الدفع")]
        public string? PaymentMethod { get; set; }

        [Required]
        [Display(Name = "مصدر الطلب")]
        public OrderOrigin OrderOrigin { get; set; }

        [Required]
        [Display(Name = "تاريخ الفاتورة")]
        public DateTime InvoiceDate { get; set; }

        [Display(Name = "تاريخ الاستحقاق")]
        public DateTime? DueDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "إجمالي المبلغ")]
        public decimal TotalAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "الخصم")]
        public decimal Discount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "سعر الشحن")]
        public decimal ShippingCost { get; set; }

        [Display(Name = "نوع الشحن")]
        public ShippingType? ShippingType { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "المبلغ المدفوع")]
        public decimal AmountPaid { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "المبلغ المتبقي")]
        public decimal RemainingAmount { get; set; }

        [Display(Name = "حالة الفاتورة")]
        public InvoiceStatus Status { get; set; }

        [Display(Name = "نوع الفاتورة")]
        public InvoiceType Type { get; set; }

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        [Display(Name = "رقم الفاتورة الأصلية")]
        public string? OriginalInvoiceNumber { get; set; }

        [Display(Name = "سبب الإرجاع/الاستبدال")]
        public string? ReturnReason { get; set; }

        [Display(Name = "اسم الكاشير")]
        public string? CashierName { get; set; }

        [Display(Name = "تاريخ الإنشاء")]
        public DateTime CreatedAt { get; set; }

        [Display(Name = "تاريخ التحديث")]
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();

        // Calculated properties
        [NotMapped]
        public decimal SubTotalBeforeDiscount => Items?.Sum(item => item.UnitPrice * item.Quantity) ?? 0;

        [NotMapped]
        public decimal SubTotal => Items?.Sum(item => item.TotalPrice) ?? 0;

        [NotMapped]
        // الإجمالي = SubTotal فقط (بدون الشحن)
        // الشحن للعرض فقط - لا يدخل في العمليات الحسابية
        public decimal GrandTotal => SubTotal;
    }

    public enum OrderOrigin
    {
        [Display(Name = "الموقع الإلكتروني")]
        Website = 1,
        
        [Display(Name = "انستجرام")]
        Instagram = 2,
        
        [Display(Name = "فيسبوك")]
        Facebook = 3,
        
        [Display(Name = "واتساب")]
        WhatsApp = 4,
        
        [Display(Name = "هاتف")]
        Phone = 5,
        
        [Display(Name = "حضور شخصي")]
        WalkIn = 6,
        
        [Display(Name = "متجر فعلي")]
        PhysicalStore = 7,
        
        [Display(Name = "أخرى")]
        Other = 8
    }

    public enum InvoiceStatus
    {
        [Display(Name = "مسودة")]
        Draft = 1,
        
        [Display(Name = "مرسل")]
        Sent = 2,
        
        [Display(Name = "مدفوع")]
        Paid = 3,
        
        [Display(Name = "متأخر")]
        Overdue = 4,
        
        [Display(Name = "ملغي")]
        Cancelled = 5,
        
        [Display(Name = "مدفوع جزئياً")]
        PartiallyPaid = 6,
        
        [Display(Name = "مؤجل")]
        Pending = 7,
        
        [Display(Name = "تحت التسليم")]
        UnderDelivery = 8,
        
        [Display(Name = "لم يسلم")]
        NotDelivered = 9,
        
        [Display(Name = "تم التسليم")]
        Delivered = 10
    }

    public enum InvoiceType
    {
        [Display(Name = "فاتورة بيع")]
        Sale = 1,
        
        [Display(Name = "فاتورة تبديل")]
        Exchange = 2,
        
        [Display(Name = "فاتورة مرتجع")]
        Return = 3,
        
        [Display(Name = "فاتورة صيانة")]
        Maintenance = 4,
        
        [Display(Name = "فاتورة تقديرية")]
        Estimate = 5
    }

    public enum ShippingType
    {
        [Display(Name = "بوسطا")]
        Bosta = 1,
        
        [Display(Name = "كايرو")]
        Cairo = 2,
        
        [Display(Name = "بدون شحن")]
        NoShipping = 3
    }
}
