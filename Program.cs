using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PesticideShop.Data;
using PesticideShop.Services;
using OfficeOpenXml;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using System.IO;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;

// EPPlus 8+ license is set in the controller

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
	options.UseSqlServer(connectionString));

// Add Activity Service
builder.Services.AddScoped<IActivityService, ActivityService>();

// Add Daily Inventory Service
builder.Services.AddScoped<IDailyInventoryService, DailyInventoryService>();

// Add Cashier Service
builder.Services.AddScoped<ICashierService, CashierService>();

// Add Financial Service
builder.Services.AddScoped<FinancialService>();

// Add WhatsApp Service
builder.Services.AddScoped<IWhatsAppService, WhatsAppService>();

// Add POS Service



builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
	options.SignIn.RequireConfirmedAccount = false;
	options.Password.RequireDigit = true;
	options.Password.RequireLowercase = true;
	options.Password.RequireNonAlphanumeric = true;
	options.Password.RequireUppercase = true;
	options.Password.RequiredLength = 8;
	options.Password.RequiredUniqueChars = 1;

	// Lockout settings
	options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
	options.Lockout.MaxFailedAccessAttempts = 5;
	options.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>();

// Reduce forced re-validation to avoid unexpected sign-outs on inactivity
builder.Services.Configure<SecurityStampValidatorOptions>(o =>
{
    o.ValidationInterval = TimeSpan.FromDays(30);
});

// Configure Session
builder.Services.AddSession(options =>
{
	options.IdleTimeout = TimeSpan.FromDays(30); // 30 days
	options.Cookie.Name = "PesticideShop.Session";
    options.Cookie.HttpOnly = true;
	options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// Configure Authentication Cookie
builder.Services.ConfigureApplicationCookie(options =>
{
	options.ExpireTimeSpan = TimeSpan.FromDays(30); // 30 days
	options.SlidingExpiration = true; // Extend expiration on activity
	options.Cookie.Name = "PesticideShop.Auth";
    options.Cookie.HttpOnly = true;
	options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
	options.LoginPath = "/Identity/Account/Login";
	options.LogoutPath = "/Identity/Account/Logout";
	options.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

// Configure Anti-Forgery cookie to be small and stable
builder.Services.AddAntiforgery(options =>
{
	options.Cookie.Name = "PesticideShop.AntiForgery";
	options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// Persist DataProtection keys to survive app restarts (global hosting / load balancers)
var keysPath = Path.Combine(builder.Environment.ContentRootPath, "DataProtectionKeys");
Directory.CreateDirectory(keysPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
    .SetApplicationName("PesticideShop");
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.UseMigrationsEndPoint();
}
//else
//{
//	app.UseExceptionHandler("/Error");
//	app.UseStatusCodePagesWithReExecute("/Error/{0}");
//	// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
//	app.UseHsts();
//}

// Respect proxy headers on global hosting (e.g., reverse proxy / load balancer)
var fwdOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
fwdOptions.KnownNetworks.Clear();
fwdOptions.KnownProxies.Clear();
app.UseForwardedHeaders(fwdOptions);

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Add Session middleware
app.UseSession();

// Cookie bloat guard middleware - prevents HTTP 400 due to oversized Cookie header
app.Use(async (context, next) =>
{
	var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
	var cookieHeader = context.Request.Headers.Cookie.ToString();
	// If Cookie header is too large, clear known cookies and reload once
	if (!string.IsNullOrEmpty(cookieHeader) && cookieHeader.Length > 6000)
	{
		try
		{
			var cookiesToClear = new[]
			{
				"PesticideShop.Session",
				"PesticideShop.Auth",
				".AspNetCore.Identity.Application",
				".AspNetCore.Antiforgery",
				"PesticideShop.AntiForgery"
			};
			foreach (var name in cookiesToClear)
			{
				if (context.Request.Cookies.ContainsKey(name))
				{
					context.Response.Cookies.Append(name, string.Empty, new CookieOptions
					{
						Expires = DateTimeOffset.UtcNow.AddDays(-1),
						HttpOnly = true,
						Secure = context.Request.IsHttps,
						SameSite = SameSiteMode.Lax,
						Path = "/"
					});
				}
			}
			logger.LogWarning("Cookie header too large ({Length} bytes). Cleared known cookies and reloading path {Path}", cookieHeader.Length, context.Request.Path);
			context.Response.Redirect(context.Request.Path + context.Request.QueryString);
			return;
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Error while clearing bloated cookies");
		}
	}

	await next();
});

// Auto-recover on 400/401/403 by clearing auth/session cookies once and retrying
app.UseStatusCodePages(async statusContext =>
{
    var http = statusContext.HttpContext;
    var code = http.Response.StatusCode;
    if ((code == 400 || code == 401 || code == 403) && !http.Request.Query.ContainsKey("recovered"))
    {
        var logger = http.RequestServices.GetRequiredService<ILogger<Program>>();
        try
        {
            var cookiesToClear = new[]
            {
                "PesticideShop.Session",
                "PesticideShop.Auth",
                ".AspNetCore.Identity.Application",
                ".AspNetCore.Antiforgery",
                "PesticideShop.AntiForgery"
            };
            foreach (var name in cookiesToClear)
            {
                if (http.Request.Cookies.ContainsKey(name))
                {
                    http.Response.Cookies.Append(name, string.Empty, new CookieOptions
                    {
                        Expires = DateTimeOffset.UtcNow.AddDays(-1),
                        HttpOnly = true,
                        Secure = http.Request.IsHttps,
                        SameSite = SameSiteMode.Lax,
                        Path = "/"
                    });
                }
            }

            var separator = http.Request.QueryString.HasValue ? "&" : "?";
            var redirectUrl = http.Request.Path + http.Request.QueryString + separator + "recovered=1";
            logger.LogWarning("Auto-recovering from status {Code} on {Path}: cleared cookies and redirecting", code, http.Request.Path);
            http.Response.Redirect(redirectUrl);
        }
        catch (Exception ex)
        {
            var logger2 = http.RequestServices.GetRequiredService<ILogger<Program>>();
            logger2.LogError(ex, "Error in status code auto-recovery middleware");
        }
    }
});

// Add Remember Me middleware
app.Use(async (context, next) =>
{
    // Check if user is not authenticated but has Remember Me cookie
    if (!context.User.Identity.IsAuthenticated)
    {
        var rememberMeCookie = context.Request.Cookies["RememberMe"];
        if (!string.IsNullOrEmpty(rememberMeCookie))
        {
            // Log that Remember Me cookie was found
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogInformation($"Remember Me cookie found for user ID: {rememberMeCookie}");
        }
    }
    
    await next();
});

// Security headers
app.Use(async (context, next) =>
{
	context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
	context.Response.Headers.Add("X-Frame-Options", "DENY");
	context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
	context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
	await next();
});


//Custom error handling middleware
//app.Use(async (context, next) =>
//{
//    try
//    {
//        await next();
//    }
//    catch (Exception ex)
//    {
//        context.Response.StatusCode = 500;
//context.Response.Redirect("/Error");
//return;
//    }
//});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
	name: "default",
	pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.MapControllerRoute(
	name: "areas",
	pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.Run();
