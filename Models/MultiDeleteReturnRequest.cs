using System.ComponentModel.DataAnnotations;

namespace PesticideShop.Models
{
    public class MultiDeleteReturnRequest
    {
        [Required(ErrorMessage = "يجب اختيار مرتجع واحد على الأقل للحذف")]
        [MinLength(1, ErrorMessage = "يجب اختيار مرتجع واحد على الأقل للحذف")]
        public List<int> ReturnIds { get; set; } = new List<int>();

        public string? Notes { get; set; }
    }

    public class ReturnDeleteItem
    {
        public int ReturnId { get; set; }
        public string ReturnInvoiceNumber { get; set; } = "";
        public string OriginalInvoiceNumber { get; set; } = "";
        public int ProductId { get; set; }
        public string ProductName { get; set; } = "";
        public int ReturnedQuantity { get; set; }
        public string? ReturnReason { get; set; }
        public DateTime ReturnDate { get; set; }
        public string CreatedBy { get; set; } = "";
        public bool IsSelected { get; set; }
    }
}

