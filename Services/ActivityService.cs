using PesticideShop.Data;
using PesticideShop.Models;
using Microsoft.EntityFrameworkCore;

namespace PesticideShop.Services
{
    public interface IActivityService
    {
        Task LogActivityAsync(string action, string entityType, string? entityName = null, string? details = null, string? userId = null);
        Task<List<ActivityLog>> GetRecentActivitiesAsync(int count = 10);
    }

    public class ActivityService : IActivityService
    {
        private readonly ApplicationDbContext _context;

        public ActivityService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task LogActivityAsync(string action, string entityType, string? entityName = null, string? details = null, string? userId = null)
        {
            var activity = new ActivityLog
            {
                Action = action,
                EntityType = entityType,
                EntityName = entityName,
                Details = details,
                UserId = userId,
                Timestamp = DateTime.Now
            };

            _context.ActivityLogs.Add(activity);
            await _context.SaveChangesAsync();
        }

        public async Task<List<ActivityLog>> GetRecentActivitiesAsync(int count = 10)
        {
            return await _context.ActivityLogs
                .OrderByDescending(a => a.Timestamp)
                .Take(count)
                .ToListAsync();
        }
    }
} 