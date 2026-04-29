using CenzasBackend.Models;
using CenzasBackend.Services;
using Microsoft.AspNetCore.Mvc;

namespace CenzasBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ContactController : ControllerBase
    {
        private readonly IEmailService _emailService;
        private readonly ILogger<ContactController> _logger;

        public ContactController(IEmailService emailService, ILogger<ContactController> logger)
        {
            _emailService = emailService;
            _logger = logger;
        }

        [HttpPost("submit")]
        public async Task<IActionResult> Submit([FromBody] LoanApplication application)
        {
            if (application == null)
            {
                return BadRequest(new { success = false, message = "Paraiškos duomenys tušti." });
            }

            try
            {
                _logger.LogInformation("Receiving new loan application for: {Name}", application.Name);
                
                await _emailService.SendApplicationEmailAsync(application);
                
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing loan application for: {Name}", application.Name);
                return StatusCode(500, new { success = false, message = "Įvyko serverio klaida siunčiant paraišką." });
            }
        }
    }
}
