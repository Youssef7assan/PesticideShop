using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PesticideShop.Models
{
    public class InvoiceItem
    {
        public int Id { get; set; }

        [Required]
        public int InvoiceId { get; set; }
        public Invoice? Invoice { get; set; }

        [Required]
        public int ProductId { get; set; }
        public Product? Product { get; set; }

        [Required]
        [Display(Name = "الكمية")]
        public int Quantity { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "سعر الوحدة")]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "الخصم")]
        public decimal Discount { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "السعر الإجمالي")]
        public decimal TotalPrice { get; set; }

        [Display(Name = "اللون")]
        public string? Color { get; set; }

        [Display(Name = "المقاس")]
        public string? Size { get; set; }

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        [Display(Name = "تاريخ الإنشاء")]
        public DateTime CreatedAt { get; set; }

        // Calculated properties
        [NotMapped]
        public decimal NetPrice => UnitPrice - Discount;

        [NotMapped]
        public decimal ItemTotal => NetPrice * Quantity;
    }
}
