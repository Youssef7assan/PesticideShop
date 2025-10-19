namespace PesticideShop.Models
{
    public class TransactionRequest
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = "";
        public string CustomerPhone { get; set; } = "";
        public string? CustomerAdditionalPhone { get; set; }
        public string? CustomerEmail { get; set; }
        public string? CustomerGovernorate { get; set; }
        public string? CustomerDistrict { get; set; }
        public string? CustomerDetailedAddress { get; set; }
        public string? CustomerAddress { get; set; } // For backward compatibility
        public decimal AmountPaid { get; set; }
        public string? InvoiceNumber { get; set; } // Manual invoice number
        public string? OrderNumber { get; set; } // Manual order number
        public string? PolicyNumber { get; set; } // رقم البوليصة
        public int InvoiceStatus { get; set; } = 3; // حالة الفاتورة (افتراضي مدفوع)
        public int OrderOrigin { get; set; } = 7; // مصدر الطلب (افتراضي متجر فعلي)
        public string? PaymentMethod { get; set; } // طريقة الدفع
        public decimal ShippingCost { get; set; } = 0; // سعر الشحن
        public int? ShippingType { get; set; } // نوع الشحن (1=Bosta, 2=Cairo, 3=NoShipping)
        public int InvoiceType { get; set; } = 1; // نوع الفاتورة (1=بيع، 2=إرجاع)
        public string? OriginalInvoiceNumber { get; set; } // للربط مع الفاتورة الأصلية (اختياري)
        public string? Notes { get; set; } // ملاحظات عامة
        public string? CashierName { get; set; } // اسم الكاشير الذي قام بعمل الفاتورة
        public List<TransactionItem> Items { get; set; } = new();
    }

    public class TransactionItem
    {
        public int ProductId { get; set; }
        public string? ProductName { get; set; } // اسم المنتج (للعرض أو البحث)
        public string? Color { get; set; } // اللون المختار
        public string? Size { get; set; } // المقاس المختار
        public int Quantity { get; set; } // الكمية (موجبة للبيع، سالبة للإرجاع)
        public decimal Price { get; set; } // سعر الوحدة
        public decimal Discount { get; set; } // الخصم
        public string? Notes { get; set; } // ملاحظات خاصة بالمنتج
    }
}
