namespace Sample.HealthProbesInvoker.Modules.Deployment.Models;

public record DeploymentResult(string Message, string App, string? Revision, DateTimeOffset CompletedAt)
{
    public static DeploymentResult Succeeded(string app, string revision)
    {
        return new DeploymentResult(
            $"Revision {revision} is active and ready to serve traffic",
            app,
            revision,
            DateTimeOffset.UtcNow
        );
    }

    public static DeploymentResult PartiallySucceeded(string app, string revision)
    {
        return new DeploymentResult(
            $"Some health probes returned a failure for app `{app}`, revision `{revision}` has not been activated, please manually investigate the issue",
            app,
            revision,
            DateTimeOffset.UtcNow
        );
    }

    public static DeploymentResult Failed(string app, string revision)
    {
        return new DeploymentResult(
            $"Some health probes returned a failure for app `{app}`, revision `{revision}` has not been activated, please manually investigate the issue",
            app,
            revision,
            DateTimeOffset.UtcNow
        );
    }

    public static DeploymentResult Exception(string exception, string app)
    {
        return new DeploymentResult(
            $"Unhandled exception in while deploying app `{app}`, please manually investigate the issue `{exception}`",
            app,
            null,
            DateTimeOffset.UtcNow
        );
    }
}
