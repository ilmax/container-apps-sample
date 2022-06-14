using Microsoft.AspNetCore.Mvc;

namespace Sample.HealthProbesInvoker.Modules.HealthChecks
{
    public static class Endpoints
    {
        public static async Task<IResult> WarmupAppLatestRevisionAsync(string appName, [FromServices] EndpointHandler endpointHandler)
        {
            return await endpointHandler.HandleAsync(null, appName, null);
        }

        public static async Task<IResult> WarmupAppRevisionByNameAsync(string appName, string revisionName, [FromServices] EndpointHandler endpointHandler)
        {
            return await endpointHandler.HandleAsync(null, appName, revisionName);
        }

        public static async Task<IResult> WarmupAppRevisionAsync(string rgName, string appName, string revisionName, [FromServices] EndpointHandler endpointHandler)
        {
            return await endpointHandler.HandleAsync(rgName, appName, revisionName);
        }
    }
}
