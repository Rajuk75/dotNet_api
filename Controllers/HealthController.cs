using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dotNetCrud.Controllers
{
    [ApiController]
    [Route("api")]
    [AllowAnonymous]
    public class HealthController : ControllerBase
    {
        [HttpGet("health")]
        public IActionResult Get()
        {
            return Ok(new { message = "Server started", status = "healthy", timestamp = DateTime.UtcNow });
        }
    }
}

