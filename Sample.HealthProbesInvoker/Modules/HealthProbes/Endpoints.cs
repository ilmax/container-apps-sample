using Microsoft.AspNetCore.Mvc;

namespace Sample.HealthProbesInvoker.Modules.HealthProbes
{
    public static class Endpoints
    {
        public static async Task<IResult> WarmupAppLatestRevisionAsync(string appName, [FromServices] HealthProbeEndpointHandler healthProbeEndpointHandler)
        {
            return await healthProbeEndpointHandler.HandleAsync(null, appName, null);
        }

        public static async Task<IResult> WarmupAppRevisionByNameAsync(string appName, string revisionName, [FromServices] HealthProbeEndpointHandler healthProbeEndpointHandler)
        {
            return await healthProbeEndpointHandler.HandleAsync(null, appName, revisionName);
        }

        public static async Task<IResult> WarmupAppRevisionAsync(string rgName, string appName, string revisionName, [FromServices] HealthProbeEndpointHandler healthProbeEndpointHandler)
        {
            return await healthProbeEndpointHandler.HandleAsync(rgName, appName, revisionName);
        }
    }
}
