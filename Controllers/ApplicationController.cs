using Microsoft.AspNetCore.Mvc;

namespace CenzasBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ApplicationController : ControllerBase
    {
        [HttpPost("apply")]
        public IActionResult Apply() => Ok(new { success = true });
    }
}