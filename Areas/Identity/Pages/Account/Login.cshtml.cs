using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;

namespace PesticideShop.Areas.Identity.Pages.Account;

public class LoginModel : PageModel
{
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(SignInManager<IdentityUser> signInManager, UserManager<IdentityUser> userManager, ILogger<LoginModel> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; }

    public string ReturnUrl { get; set; }

    [TempData]
    public string ErrorMessage { get; set; }

    public class InputModel
    {
        [Required]
        [Display(Name = "اسم المستخدم أو البريد الإلكتروني")]
        public string UserNameOrEmail { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "كلمة المرور")]
        public string Password { get; set; }

        [Display(Name = "تذكرني؟")]
        public bool RememberMe { get; set; }
    }

    public async Task OnGetAsync(string returnUrl = null)
    {
        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            ModelState.AddModelError(string.Empty, ErrorMessage);
        }

        returnUrl ??= Url.Content("~/");

        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        ReturnUrl = returnUrl;
    }

    public async Task<IActionResult> OnPostAsync(string returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");

        if (ModelState.IsValid)
        {
            // ابحث عن المستخدم بالبريد أو اسم المستخدم
            IdentityUser user = await _userManager.FindByNameAsync(Input.UserNameOrEmail);
            if (user == null)
            {
                user = await _userManager.FindByEmailAsync(Input.UserNameOrEmail);
            }

            if (user != null)
            {
                var result = await _signInManager.PasswordSignInAsync(user.UserName, Input.Password, Input.RememberMe, lockoutOnFailure: false);
                if (result.Succeeded)
                {
                    _logger.LogInformation($"User {user.UserName} logged in with RememberMe: {Input.RememberMe}");
                    
                    // Set additional cookie for "Remember Me" functionality
                    if (Input.RememberMe)
                    {
                        var rememberMeCookie = new CookieOptions
                        {
                            Expires = DateTimeOffset.UtcNow.AddDays(30),
                            HttpOnly = true,
                            Secure = Request.IsHttps,
                            SameSite = SameSiteMode.Lax
                        };
                        Response.Cookies.Append("RememberMe", user.Id, rememberMeCookie);
                        _logger.LogInformation($"Remember Me cookie set for user {user.UserName}");
                    }
                    
                    return LocalRedirect(returnUrl);
                }
                if (result.RequiresTwoFactor)
                {
                    return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
                }
                if (result.IsLockedOut)
                {
                    _logger.LogWarning("User account locked out.");
                    return RedirectToPage("./Lockout");
                }
            }
            ModelState.AddModelError(string.Empty, "محاولة تسجيل دخول غير صحيحة. تأكد من اسم المستخدم أو البريد الإلكتروني وكلمة المرور.");
            return Page();
        }

        return Page();
    }
} 