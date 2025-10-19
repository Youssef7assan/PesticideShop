using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq; // Added for .Sum()

namespace PesticideShop.Models
{
    public class InvoiceViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "يجب اختيار العميل")]
        [Display(Name = "العميل")]
        public int CustomerId { get; set; }

        [Required(ErrorMessage = "رقم الفاتورة مطلوب")]
        [Display(Name = "رقم الفاتورة")]
        public string InvoiceNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "رقم الطلب مطلوب")]
        [Display(Name = "رقم الطلب")]
        public string OrderNumber { get; set; } = string.Empty;

        [Display(Name = "رقم البوليصة")]
        public string? PolicyNumber { get; set; }

        [Required(ErrorMessage = "مصدر الطلب مطلوب")]
        [Display(Name = "مصدر الطلب")]
        public OrderOrigin OrderOrigin { get; set; }

        [Required(ErrorMessage = "تاريخ الفاتورة مطلوب")]
        [Display(Name = "تاريخ الفاتورة")]
        [DataType(DataType.Date)]
        public DateTime InvoiceDate { get; set; } = DateTime.Now;

        [Display(Name = "تاريخ الاستحقاق")]
        [DataType(DataType.Date)]
        public DateTime? DueDate { get; set; }

        [Display(Name = "الخصم")]
        [Range(0, double.MaxValue, ErrorMessage = "الخصم يجب أن يكون صفر أو أكثر")]
        public decimal Discount { get; set; }

        [Display(Name = "سعر الشحن")]
        [Range(0, double.MaxValue, ErrorMessage = "سعر الشحن يجب أن يكون صفر أو أكثر")]
        public decimal ShippingCost { get; set; }

        [Display(Name = "المبلغ المدفوع")]
        [Range(0, double.MaxValue, ErrorMessage = "المبلغ المدفوع يجب أن يكون صفر أو أكثر")]
        public decimal AmountPaid { get; set; }

        [Display(Name = "نوع الفاتورة")]
        public InvoiceType Type { get; set; } = InvoiceType.Sale;

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        // Navigation properties
        public Customer? Customer { get; set; }
        public List<InvoiceItemViewModel> Items { get; set; } = new List<InvoiceItemViewModel>();

        // Calculated properties
        public decimal SubTotal => Items?.Sum(item => item.TotalPrice) ?? 0;
        public decimal GrandTotal => SubTotal + ShippingCost - Discount;
        public decimal RemainingAmount => GrandTotal - AmountPaid;
    }

    public class InvoiceItemViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "يجب اختيار المنتج")]
        [Display(Name = "المنتج")]
        public int ProductId { get; set; }

        [Required(ErrorMessage = "الكمية مطلوبة")]
        [Range(1, int.MaxValue, ErrorMessage = "الكمية يجب أن تكون 1 أو أكثر")]
        [Display(Name = "الكمية")]
        public int Quantity { get; set; }

        [Required(ErrorMessage = "سعر الوحدة مطلوب")]
        [Range(0.01, double.MaxValue, ErrorMessage = "سعر الوحدة يجب أن يكون أكبر من صفر")]
        [Display(Name = "سعر الوحدة")]
        public decimal UnitPrice { get; set; }

        [Display(Name = "الخصم")]
        [Range(0, double.MaxValue, ErrorMessage = "الخصم يجب أن يكون صفر أو أكثر")]
        public decimal Discount { get; set; }

        [Display(Name = "السعر الإجمالي")]
        public decimal TotalPrice { get; set; }

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        // Product info for display
        public string? ProductName { get; set; }
        public int AvailableQuantity { get; set; }

        // Calculated properties
        public decimal NetPrice => UnitPrice - Discount;
        public decimal ItemTotal => NetPrice * Quantity;
    }

    public class InvoiceListViewModel
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public string OrderNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public OrderOrigin OrderOrigin { get; set; }
        public InvoiceType Type { get; set; }
        public InvoiceStatus Status { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal RemainingAmount { get; set; }
        public int ItemsCount { get; set; }
    }
}
