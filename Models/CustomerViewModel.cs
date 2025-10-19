using System.ComponentModel.DataAnnotations;

namespace PesticideShop.Models
{
    public class CustomerViewModel
    {
        [Required(ErrorMessage = "اسم العميل مطلوب")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        public string PhoneNumber { get; set; } = string.Empty;

        public string? AdditionalPhone { get; set; }

        [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صحيح")]
        public string? Email { get; set; }

        public string? Governorate { get; set; }

        public string? District { get; set; }

        public string? DetailedAddress { get; set; }
    }
}
