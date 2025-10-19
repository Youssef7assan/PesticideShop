using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PesticideShop.Models
{
    public class OrderViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "رقم الأوردر مطلوب")]
        [Display(Name = "رقم الأوردر")]
        public string OrderNumber { get; set; }

        [Required(ErrorMessage = "اسم العميل مطلوب")]
        [Display(Name = "اسم العميل")]
        public string CustomerName { get; set; }

        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        [Display(Name = "رقم الهاتف")]
        public string CustomerPhone { get; set; }

        [Display(Name = "رقم هاتف إضافي")]
        public string AdditionalPhone { get; set; }

        [Display(Name = "العنوان")]
        public string Address { get; set; }

        [Display(Name = "المحافظة")]
        public string Governorate { get; set; }

        [Display(Name = "المنطقة")]
        public string District { get; set; }

        [Display(Name = "العنوان التفصيلي")]
        public string DetailedAddress { get; set; }

        [Display(Name = "البريد الإلكتروني")]
        [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صالح")]
        public string Email { get; set; }

        [Display(Name = "تاريخ الطلب")]
        [DataType(DataType.Date)]
        public DateTime OrderDate { get; set; } = DateTime.Now;

        [Display(Name = "حالة الطلب")]
        public OrderStatus Status { get; set; } = OrderStatus.New;



        [Display(Name = "المبلغ الإجمالي")]
        [DataType(DataType.Currency)]
        public decimal TotalAmount { get; set; }

        [Display(Name = "الخصم")]
        [DataType(DataType.Currency)]
        public decimal Discount { get; set; }

        [Display(Name = "تكلفة الشحن")]
        [DataType(DataType.Currency)]
        public decimal ShippingCost { get; set; }

        [Display(Name = "المبلغ المدفوع")]
        [DataType(DataType.Currency)]
        public decimal AmountPaid { get; set; }

        [Display(Name = "المبلغ المتبقي")]
        [DataType(DataType.Currency)]
        public decimal RemainingAmount { get; set; }

        [Display(Name = "ملاحظات")]
        public string Notes { get; set; }

        [Display(Name = "منتجات الطلب")]
        public List<OrderItemViewModel> Items { get; set; } = new List<OrderItemViewModel>();
    }

    public class OrderItemViewModel
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string ProductCode { get; set; }
        public string CategoryName { get; set; }
        public string Color { get; set; }
        public string Size { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal Discount { get; set; }
        public decimal TotalPrice { get; set; }
    }

    public enum OrderStatus
    {
        [Display(Name = "جديد")]
        New,
        
        [Display(Name = "قيد المعالجة")]
        Processing,
        
        [Display(Name = "تم الشحن")]
        Shipped,
        
        [Display(Name = "تم التسليم")]
        Delivered,
        
        [Display(Name = "ملغي")]
        Cancelled,
        
        [Display(Name = "مرتجع")]
        Returned
    }


}
