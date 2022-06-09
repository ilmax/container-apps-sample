using Sample.HealthProbesInvoker.Modules;

var builder = WebApplication.CreateBuilder(args);
builder.RegisterModules();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(opt =>
{
    opt.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
    opt.RoutePrefix = string.Empty;
});
app.UseRouting();
app.MapEndpoints();
app.Run();