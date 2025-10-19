using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PesticideShop.Data;
using PesticideShop.Models;
using System.ComponentModel.DataAnnotations;

namespace PesticideShop.Controllers
{
    [Authorize]
    public class AdminController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly ApplicationDbContext _context;

        public AdminController(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
        }



        // GET: Admin/Index
        public IActionResult Index()
        {
            return View();
        }

        // GET: Admin/Register
        public IActionResult Register()
        {
            // Only allow if user is already logged in as admin
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Account", new { area = "Identity" });
            }

            return View();
        }



        // POST: Admin/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(AdminRegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new IdentityUser { UserName = model.Username, Email = model.Email };
                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    TempData["SuccessMessage"] = "تم إنشاء حساب المستخدم بنجاح!";
                    return RedirectToAction(nameof(Users));
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return View(model);
        }







        // GET: Admin/Users
        public async Task<IActionResult> Users()
        {
            var users = await _userManager.Users.ToListAsync();
            var userViewModels = users.Select(u => new UserViewModel
            {
                Id = u.Id,
                Username = u.UserName,
                Email = u.Email
            }).ToList();

            return View(userViewModels);
        }

        // GET: Admin/AddUser
        public IActionResult AddUser()
        {
            return View();
        }

        // POST: Admin/AddUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddUser(AdminRegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new IdentityUser { UserName = model.Username, Email = model.Email };
                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    TempData["SuccessMessage"] = "تم إنشاء حساب المستخدم بنجاح!";
                    return RedirectToAction(nameof(Users));
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return View(model);
        }

        // POST: Admin/DeleteUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                var result = await _userManager.DeleteAsync(user);
                if (result.Succeeded)
                {
                    TempData["SuccessMessage"] = "User deleted successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to delete user.";
                }
            }

            return RedirectToAction(nameof(Users));
        }

        // GET: Admin/ResetStatistics
        [AllowAnonymous]
        public async Task<IActionResult> ResetStatistics()
        {
            return View();
        }

        // POST: Admin/ResetStatistics
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> ResetStatisticsConfirmed()
        {
            try
            {
                // Clear all customer transactions
                var customerTransactions = await _context.CustomerTransactions.ToListAsync();
                _context.CustomerTransactions.RemoveRange(customerTransactions);

                

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Statistics have been reset successfully! All transactions have been cleared.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error resetting statistics: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }
    }
} 