using Sample.HealthProbesInvoker.Modules.HealthChecks.Services;

namespace Sample.HealthProbesInvoker.Modules.HealthChecks;

public class Handler
{
    private readonly RevisionSelector _revisionSelector;
    private readonly ProbeInvoker _prbInvoker;
    private readonly ILogger<Handler> _logger;

    public Handler(RevisionSelector revisionSelector, ProbeInvoker prbInvoker, ILogger<Handler> logger)
    {
        _revisionSelector = revisionSelector;
        _prbInvoker = prbInvoker;
        _logger = logger;
    }

    public async Task<IResult> ExecuteAsync(string? rgName, string appName, string? revName)
    {
        try
        {
            var revision = await _revisionSelector.SelectRevisionAsync(rgName, appName, revName);

            var result = await _prbInvoker.InvokeRevisionProbesAsync(revision);

            if (result.All(p => p.IsSuccessful))
            {
                return Results.Ok(new
                {
                    status = $"revision {revision.Name} is active and ready to serve traffic",
                    revision = revision.Name
                });
            }

            _logger.LogWarning("Not all result are successful, returning a failure for {app}", appName);
            return Results.BadRequest(result);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to warmup application {app}", appName);
            return Results.BadRequest(ex.Message);
        }
    }
}