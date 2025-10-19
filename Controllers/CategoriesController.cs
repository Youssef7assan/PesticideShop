using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PesticideShop.Data;
using PesticideShop.Models;
using PesticideShop.Services;

namespace PesticideShop.Controllers
{
    public class CategoriesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IActivityService _activityService;

        public CategoriesController(ApplicationDbContext context, IActivityService activityService)
        {
            _context = context;
            _activityService = activityService;
        }

        // GET: Categories
        public async Task<IActionResult> Index()
        {
            var categories = await _context.Categories
                .Include(c => c.Products)
                .OrderBy(c => c.Name)
                .ToListAsync();
            return View(categories);
        }

        // GET: Categories/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var category = await _context.Categories
                .Include(c => c.Products)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (category == null)
            {
                return NotFound();
            }

            return View(category);
        }

        // GET: Categories/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Categories/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Description,IsActive")] Category category)
        {
            if (ModelState.IsValid)
            {
                category.CreatedAt = DateTime.Now;
                _context.Add(category);
                await _context.SaveChangesAsync();

                await _activityService.LogActivityAsync(
                    User.Identity?.Name ?? "System",
                    "إنشاء تصنيف جديد",
                    $"تم إنشاء التصنيف: {category.Name}",
                    "Categories"
                );

                TempData["Success"] = $"تم إنشاء التصنيف '{category.Name}' بنجاح";
                return RedirectToAction(nameof(Index));
            }
            return View(category);
        }

        // GET: Categories/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var category = await _context.Categories.FindAsync(id);
            if (category == null)
            {
                return NotFound();
            }
            return View(category);
        }

        // POST: Categories/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Description,IsActive,CreatedAt")] Category category)
        {
            if (id != category.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingCategory = await _context.Categories.FindAsync(id);
                    if (existingCategory == null)
                    {
                        return NotFound();
                    }

                    var oldName = existingCategory.Name;
                    existingCategory.Name = category.Name;
                    existingCategory.Description = category.Description;
                    existingCategory.IsActive = category.IsActive;

                    _context.Update(existingCategory);
                    await _context.SaveChangesAsync();

                    await _activityService.LogActivityAsync(
                        User.Identity?.Name ?? "System",
                        "تعديل التصنيف",
                        $"تم تعديل التصنيف من '{oldName}' إلى '{category.Name}'",
                        "Categories"
                    );

                    TempData["Success"] = $"تم تحديث التصنيف '{category.Name}' بنجاح";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CategoryExists(category.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            return View(category);
        }

        // GET: Categories/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var category = await _context.Categories
                .Include(c => c.Products)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (category == null)
            {
                return NotFound();
            }

            return View(category);
        }

        // POST: Categories/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var category = await _context.Categories
                .Include(c => c.Products)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
            {
                return NotFound();
            }

            if (category.Products != null && category.Products.Any())
            {
                TempData["Error"] = $"لا يمكن حذف التصنيف '{category.Name}' لأنه يحتوي على {category.Products.Count} منتج. يرجى نقل المنتجات إلى تصنيف آخر أولاً.";
                return RedirectToAction(nameof(Index));
            }

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();

            await _activityService.LogActivityAsync(
                User.Identity?.Name ?? "System",
                "حذف التصنيف",
                $"تم حذف التصنيف: {category.Name}",
                "Categories"
            );

            TempData["Success"] = $"تم حذف التصنيف '{category.Name}' بنجاح";
            return RedirectToAction(nameof(Index));
        }

        private bool CategoryExists(int id)
        {
            return _context.Categories.Any(e => e.Id == id);
        }

        // API endpoint for getting categories (used in product forms)
        [HttpGet]
        public async Task<IActionResult> GetCategories()
        {
            var categories = await _context.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .Select(c => new { c.Id, c.Name })
                .ToListAsync();
            return Json(categories);
        }
    }
}
