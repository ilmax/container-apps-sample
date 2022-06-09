using Microsoft.AspNetCore.Mvc;
using Sample.HealthProbesInvoker.Modules.HealthChecks.Services;

namespace Sample.HealthProbesInvoker.Modules.HealthChecks
{
    public static class Handlers
    {
        public static async Task<IResult> WarmupByDefaultAsync(string appName,
            [FromServices] RevisionSelector revisionSelector, [FromServices] ProbeInvoker prbInvoker)
        {
            return await ExecuteAsync(null, appName, null, revisionSelector, prbInvoker);
        }

        public static async Task<IResult> WarmupByRevisionNameAsync(string appName, string revisionName,
            [FromServices] RevisionSelector revisionSelector, [FromServices] ProbeInvoker prbInvoker)
        {
            return await ExecuteAsync(null, appName, revisionName, revisionSelector, prbInvoker);
        }

        public static async Task<IResult> WarmupAsync(string rgName, string appName, string revisionName,
            [FromServices] RevisionSelector revisionSelector, [FromServices] ProbeInvoker prbInvoker)
        {
            return await ExecuteAsync(rgName, appName, revisionName, revisionSelector, prbInvoker);
        }

        private static async Task<IResult> ExecuteAsync(string? rgName, string appName, string? revName, RevisionSelector revisionSelector, ProbeInvoker prbInvoker)
        {
            try
            {
                var revision = await revisionSelector.SelectRevisionAsync(null, appName, revName);

                var result = await prbInvoker.InvokeRevisionProbesAsync(revision);

                if (result.All(p => p.IsSuccessful))
                {
                    return Results.Ok(new
                    {
                        status = $"revision {revision.Name} is active and ready to serve traffic",
                        revision = revision.Name
                    });
                }

                return Results.BadRequest(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(ex.Message);
            }
        }
    }
}
