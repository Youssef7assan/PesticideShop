using Microsoft.AspNetCore.Mvc;
using PesticideShop.Models;

namespace PesticideShop.Controllers
{
    public class ErrorController : Controller
    {
        [Route("Error/{statusCode}")]
        public IActionResult HttpStatusCodeHandler(int statusCode)
        {
            var errorViewModel = new ErrorViewModel
            {
                StatusCode = statusCode,
                Message = GetErrorMessage(statusCode)
            };

            return View("Error", errorViewModel);
        }

        [Route("Error")]
        public IActionResult Error()
        {
            var errorViewModel = new ErrorViewModel
            {
                StatusCode = 500,
                Message = "حدث خطأ غير متوقع في النظام"
            };

            return View("Error", errorViewModel);
        }

        private string GetErrorMessage(int statusCode)
        {
            return statusCode switch
            {
                404 => "الصفحة المطلوبة غير موجودة",
                403 => "غير مصرح لك بالوصول إلى هذه الصفحة",
                401 => "يجب تسجيل الدخول للوصول إلى هذه الصفحة",
                500 => "حدث خطأ في الخادم",
                _ => "حدث خطأ غير متوقع"
            };
        }
    }
} 