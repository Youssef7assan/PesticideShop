using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PesticideShop.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ApiController : ControllerBase
    {
        [HttpPost("heartbeat")]
        [Authorize]
        public IActionResult Heartbeat()
        {
            // This endpoint keeps the session alive
            // It doesn't need to do anything special, just return success
            return Ok(new { 
                success = true, 
                message = "Session kept alive",
                timestamp = DateTime.Now
            });
        }

    }
}