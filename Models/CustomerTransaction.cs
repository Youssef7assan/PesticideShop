using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PesticideShop.Models
{
    public class CustomerTransaction
    {
        public int Id { get; set; }

        [Required]
        public int CustomerId { get; set; }
        public Customer? Customer { get; set; }

        [Required]
        public int ProductId { get; set; }
        public Product? Product { get; set; }

        [Required]
        public int Quantity { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "الخصم")]
        public decimal Discount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "سعر الشحن")]
        public decimal ShippingCost { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal AmountPaid { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [Display(Name = "اللون")]
        public string? Color { get; set; }

        [Display(Name = "المقاس")]
        public string? Size { get; set; }

        [Display(Name = "نوع الشحن")]
        public ShippingType? ShippingType { get; set; }

        public string? Notes { get; set; }
    }
} 