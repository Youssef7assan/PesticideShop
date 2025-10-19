using System.ComponentModel.DataAnnotations;

namespace PesticideShop.Models
{
    public class ActivityLog
    {
        public int Id { get; set; }

        [Required]
        public string Action { get; set; } = string.Empty;

        [Required]
        public string EntityType { get; set; } = string.Empty;

        public string? EntityName { get; set; }

        public string? Details { get; set; }

        [Required]
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public string? UserId { get; set; }
    }
} 