using Microsoft.AspNetCore.Mvc;

namespace Sample.HealthProbesInvoker.Modules.HealthChecks
{
    public static class Endpoints
    {
        public static async Task<IResult> WarmupByDefaultAsync(string appName, [FromServices] Handler handler)
        {
            return await handler.ExecuteAsync(null, appName, null);
        }

        public static async Task<IResult> WarmupByRevisionNameAsync(string appName, string revisionName, [FromServices] Handler handler)
        {
            return await handler.ExecuteAsync(null, appName, revisionName);
        }

        public static async Task<IResult> WarmupAsync(string rgName, string appName, string revisionName, [FromServices] Handler handler)
        {
            return await handler.ExecuteAsync(rgName, appName, revisionName);
        }
    }
}
