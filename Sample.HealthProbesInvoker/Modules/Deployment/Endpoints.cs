using System.Net;
using Microsoft.AspNetCore.Mvc;

namespace Sample.HealthProbesInvoker.Modules.Deployment;

public static class Endpoints
{
    public static async Task<IResult> DeployNewImageCurrentRgAsync(string appName, [FromQuery]string imageName, [FromServices] DeploymentEndpointHandler endpointHandler)
    {
        return await endpointHandler.DeployNewImageAsync(null, appName, WebUtility.UrlDecode(imageName));
    }

    public static async Task<IResult> DeployNewImageAsync(string rgName, string appName, [FromQuery]string imageName, [FromServices] DeploymentEndpointHandler endpointHandler)
    {
        return await endpointHandler.DeployNewImageAsync(rgName, appName, WebUtility.UrlDecode(imageName));
    }
}