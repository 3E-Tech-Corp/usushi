using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ProjectTemplate.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet("/health")]
    public IActionResult Health() => Ok(new { status = "healthy", timestamp = DateTime.UtcNow });

    /// <summary>Debug: show current user's JWT claims</summary>
    [HttpGet("/debug/claims")]
    [Authorize]
    public IActionResult Claims()
    {
        var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();
        var isAdmin = User.IsInRole("Admin");
        return Ok(new { isAdmin, claims });
    }
}
