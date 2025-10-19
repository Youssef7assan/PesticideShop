using System.ComponentModel.DataAnnotations;

namespace PesticideShop.Models
{
    public class ReturnTracking
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string OriginalInvoiceNumber { get; set; } = "";
        
        [Required]
        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;
        
        [Required]
        public int ReturnedQuantity { get; set; }
        
        [Required]
        public string ReturnInvoiceNumber { get; set; } = "";
        
        [Required]
        public DateTime ReturnDate { get; set; } = DateTime.Now;
        
        public string? ReturnReason { get; set; }
        
        public string? Notes { get; set; }
        
        [Required]
        public string CreatedBy { get; set; } = "";
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
