using Microsoft.AspNetCore.Mvc;

namespace Sample.Producer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EchoController : ControllerBase
{
    [HttpGet("ping")]
    public ActionResult Ping() => Ok("pong");
}