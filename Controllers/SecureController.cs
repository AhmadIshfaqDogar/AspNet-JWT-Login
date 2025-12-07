using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace JwtAuthDemo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SecureController : ControllerBase
    {
        // This endpoint requires a valid Access Token
            [Authorize(Roles = "SuperAdmin")]
            [HttpPost("create-admin")]
            public IActionResult CreateAdmin()
            {
                return Ok("Only SuperAdmin can create admins");
            }
    }
}
