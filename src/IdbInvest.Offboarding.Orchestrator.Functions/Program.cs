using IdbInvest.Offboarding.Orchestrator.Application;
using IdbInvest.Offboarding.Orchestrator.Functions.Middleware;
using IdbInvest.Offboarding.Orchestrator.Infrastructure;
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

builder.Services.AddOrchestratorApplication();
builder.Services.AddOrchestratorInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddApplicationInsightsTelemetryWorkerService();
builder.Services.ConfigureFunctionsApplicationInsights();

builder.UseMiddleware<ExceptionHandlingMiddleware>();

await builder.Build().RunAsync();
