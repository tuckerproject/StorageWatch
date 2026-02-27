using Microsoft.AspNetCore.Mvc;

namespace StorageWatchServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ServerController : ControllerBase
{
    [HttpGet("ping")]
    public IActionResult Ping()
    {
        return Ok("pong");
    }
}
