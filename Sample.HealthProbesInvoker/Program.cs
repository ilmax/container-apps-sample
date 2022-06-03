using Azure.Identity;
using Azure.ResourceManager;
using Sample.HealthProbesInvoker;
using Sample.HealthProbesInvoker.Config;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddScoped<RevisionSelector>();
builder.Services.AddScoped<ProbeInvoker>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped(_ => new ArmClient(new DefaultAzureCredential(GetDefaultAzureCredentialOptions(builder.Environment))));
builder.Services.Configure<AzureConfig>(builder.Configuration.GetSection("Azure"));

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(opt =>
{
    opt.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
    opt.RoutePrefix = string.Empty;
});
app.UseRouting();

app.MapGet("warmup/{appName}", Handlers.WarmupByDefaultAsync);
app.MapGet("warmup/{appName}/revisions/{revisionName}", Handlers.WarmupByRevisionNameAsync);
app.MapGet("warmup/resourceGroups/{rgName}/apps/{appName}/revisions/{revisionName}", Handlers.WarmupAsync);

app.Run();

static DefaultAzureCredentialOptions GetDefaultAzureCredentialOptions(IHostEnvironment hostEnvironment)
{
    return new DefaultAzureCredentialOptions
    {
        ExcludeEnvironmentCredential = true,
        ExcludeInteractiveBrowserCredential = true,
        ExcludeAzurePowerShellCredential = true,
        ExcludeSharedTokenCacheCredential = true,
        ExcludeVisualStudioCodeCredential = true,
        ExcludeVisualStudioCredential = !hostEnvironment.IsDevelopment(),
        ExcludeAzureCliCredential = !hostEnvironment.IsDevelopment(),
        ExcludeManagedIdentityCredential = hostEnvironment.IsDevelopment(),
    };
}