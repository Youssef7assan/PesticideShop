using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PesticideShop.Models
{
    public class DailyInventory
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "تاريخ الجرد")]
        public DateTime InventoryDate { get; set; }

        [Display(Name = "تاريخ البداية")]
        public DateTime StartTime { get; set; } // 12:00 AM

        [Display(Name = "تاريخ النهاية")]
        public DateTime EndTime { get; set; } // 11:59 PM

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "إجمالي المبيعات")]
        public decimal TotalSales { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "إجمالي التكلفة")]
        public decimal TotalCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "صافي الربح")]
        public decimal NetProfit { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "إجمالي الخصومات")]
        public decimal TotalDiscounts { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "إجمالي المدفوعات")]
        public decimal TotalPayments { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "إجمالي المديونيات")]
        public decimal TotalDebts { get; set; }

        [Display(Name = "عدد المعاملات")]
        public int TransactionsCount { get; set; }

        [Display(Name = "عدد العملاء")]
        public int CustomersCount { get; set; }

        [Display(Name = "عدد المنتجات المباعة")]
        public int ProductsSoldCount { get; set; }

        [Display(Name = "إجمالي الكمية المباعة")]
        public int TotalQuantitySold { get; set; }

        [Display(Name = "حالة الجرد")]
        public InventoryStatus Status { get; set; }

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        [Display(Name = "تاريخ الإنشاء")]
        public DateTime CreatedAt { get; set; }

        [Display(Name = "تاريخ التحديث")]
        public DateTime? UpdatedAt { get; set; }

        [Display(Name = "المستخدم المسؤول")]
        public string? ResponsibleUser { get; set; }

        // Navigation properties
        public virtual ICollection<DailySaleTransaction> SaleTransactions { get; set; } = new List<DailySaleTransaction>();
        public virtual ICollection<DailyProductSummary> ProductSummaries { get; set; } = new List<DailyProductSummary>();
        public virtual ICollection<DailyCustomerSummary> CustomerSummaries { get; set; } = new List<DailyCustomerSummary>();

        // Calculated properties
        [NotMapped]
        public decimal ProfitMargin => TotalSales > 0 ? (NetProfit / TotalSales) * 100 : 0;

        [NotMapped]
        public decimal PaymentPercentage => TotalSales > 0 ? (TotalPayments / TotalSales) * 100 : 0;

        [NotMapped]
        public decimal AverageTransactionValue => TransactionsCount > 0 ? TotalSales / TransactionsCount : 0;
    }

    public class DailySaleTransaction
    {
        public int Id { get; set; }

        [Required]
        public int DailyInventoryId { get; set; }
        public DailyInventory DailyInventory { get; set; }

        [Required]
        public int CustomerId { get; set; }
        public Customer Customer { get; set; }

        [Required]
        public int ProductId { get; set; }
        public Product Product { get; set; }

        [Display(Name = "الكمية")]
        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "سعر الوحدة")]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "سعر التكلفة")]
        public decimal CostPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "إجمالي السعر")]
        public decimal TotalPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "الخصم")]
        public decimal Discount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "المبلغ المدفوع")]
        public decimal AmountPaid { get; set; }

        [Display(Name = "وقت المعاملة")]
        public DateTime TransactionTime { get; set; }

        [Display(Name = "رقم الفاتورة")]
        public string? InvoiceNumber { get; set; }

        [Display(Name = "رقم الطلب")]
        public string? OrderNumber { get; set; }

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        // Reference to original transaction
        [Display(Name = "رقم المعاملة الأصلية")]
        public int OriginalTransactionId { get; set; }
    }

    public class DailyProductSummary
    {
        public int Id { get; set; }

        [Required]
        public int DailyInventoryId { get; set; }
        public DailyInventory DailyInventory { get; set; }

        [Required]
        public int ProductId { get; set; }
        public Product Product { get; set; }

        [Display(Name = "إجمالي الكمية المباعة")]
        public int TotalQuantitySold { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "إجمالي قيمة المبيعات")]
        public decimal TotalSalesValue { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "إجمالي التكلفة")]
        public decimal TotalCostValue { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "إجمالي الخصومات")]
        public decimal TotalDiscounts { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "صافي المبيعات")]
        public decimal NetSalesValue { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "صافي الربح")]
        public decimal NetProfit { get; set; }

        [Display(Name = "عدد المعاملات")]
        public int TransactionsCount { get; set; }

        [Display(Name = "الكمية المتبقية بداية اليوم")]
        public int StartingQuantity { get; set; }

        [Display(Name = "الكمية المتبقية نهاية اليوم")]
        public int EndingQuantity { get; set; }
    }

    public class DailyCustomerSummary
    {
        public int Id { get; set; }

        [Required]
        public int DailyInventoryId { get; set; }
        public DailyInventory DailyInventory { get; set; }

        [Required]
        public int CustomerId { get; set; }
        public Customer Customer { get; set; }

        [Display(Name = "عدد المعاملات")]
        public int TransactionsCount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "إجمالي المشتريات")]
        public decimal TotalPurchases { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "إجمالي المدفوعات")]
        public decimal TotalPayments { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "المديونية")]
        public decimal DebtAmount { get; set; }

        [Display(Name = "آخر وقت معاملة")]
        public DateTime LastTransactionTime { get; set; }
    }

    public enum InventoryStatus
    {
        [Display(Name = "نشط")]
        Active = 1,

        [Display(Name = "مكتمل")]
        Completed = 2,

        [Display(Name = "مغلق")]
        Closed = 3,

        [Display(Name = "ملغي")]
        Cancelled = 4
    }
}
