using AlfaHackaton.Controllers.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace UserService.Controllers;

public class PingController : BaseController
{
    [HttpGet("ping")]
    public async Task<IActionResult> GetPing()
    {
        return Ok("pong");
    }
}