using Microsoft.AspNetCore.Mvc;

namespace Sample.HealthProbesInvoker.Modules.HealthChecks
{
    public static class Endpoints
    {
        public static async Task<IResult> WarmupByDefaultAsync(string appName, [FromServices] EndpointHandler endpointHandler)
        {
            return await endpointHandler.HandleAsync(null, appName, null);
        }

        public static async Task<IResult> WarmupByRevisionNameAsync(string appName, string revisionName, [FromServices] EndpointHandler endpointHandler)
        {
            return await endpointHandler.HandleAsync(null, appName, revisionName);
        }

        public static async Task<IResult> WarmupAsync(string rgName, string appName, string revisionName, [FromServices] EndpointHandler endpointHandler)
        {
            return await endpointHandler.HandleAsync(rgName, appName, revisionName);
        }
    }
}
