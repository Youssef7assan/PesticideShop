using System.ComponentModel.DataAnnotations;

namespace PesticideShop.Models
{
    public class Category
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم التصنيف مطلوب")]
        [StringLength(100, ErrorMessage = "اسم التصنيف يجب أن يكون أقل من 100 حرف")]
        [Display(Name = "اسم التصنيف")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "الوصف يجب أن يكون أقل من 500 حرف")]
        [Display(Name = "الوصف")]
        public string? Description { get; set; }

        [Display(Name = "تاريخ الإنشاء")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;

        // Navigation property
        public ICollection<Product>? Products { get; set; }

        [Display(Name = "عدد المنتجات")]
        public int ProductsCount => Products?.Count ?? 0;
    }
}
