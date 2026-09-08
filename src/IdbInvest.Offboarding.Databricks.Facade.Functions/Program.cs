using IdbInvest.Offboarding.Databricks.Facade.Application;
using IdbInvest.Offboarding.Databricks.Facade.Functions.Middleware;
using IdbInvest.Offboarding.Databricks.Facade.Infrastructure;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables();

builder.Services.AddApplication(Path.Combine(AppContext.BaseDirectory, "resource-definitions.json"));
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddApplicationInsightsTelemetryWorkerService();
builder.Services.ConfigureFunctionsApplicationInsights();

builder.UseMiddleware<ExceptionHandlingMiddleware>();

builder.Build().Run();
