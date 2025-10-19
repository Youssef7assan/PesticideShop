using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PesticideShop.Areas.Identity.Pages.Account;

public class LogoutModel : PageModel
{
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly ILogger<LogoutModel> _logger;

    public LogoutModel(SignInManager<IdentityUser> signInManager, ILogger<LogoutModel> logger)
    {
        _signInManager = signInManager;
        _logger = logger;
    }

    public async Task<IActionResult> OnPost(string returnUrl = null)
    {
        // Clear Remember Me cookie when logging out
        Response.Cookies.Delete("RememberMe");
        _logger.LogInformation("Remember Me cookie cleared on logout.");
        
        await _signInManager.SignOutAsync();
        _logger.LogInformation("User logged out.");
        
        if (returnUrl != null)
        {
            return LocalRedirect(returnUrl);
        }
        else
        {
            return RedirectToPage();
        }
    }
} 