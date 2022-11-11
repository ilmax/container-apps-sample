using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sample.Producer.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class EchoController : ControllerBase
{
    public static string Version = typeof(EchoController).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

    [HttpGet("ping")]
    public ActionResult Ping() => Ok($"pong from {Version}");
}