using Microsoft.AspNetCore.Mvc;

namespace CenzasBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ListingsController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetListings() => Ok(new List<string>());
    }
}
