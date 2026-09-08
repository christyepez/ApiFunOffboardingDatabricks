using IdbInvest.Offboarding.Databricks.Facade.Core.Interfaces;
using IdbInvest.Offboarding.Databricks.Facade.Infrastructure.Auth;
using IdbInvest.Offboarding.Databricks.Facade.Infrastructure.Databricks;
using IdbInvest.Offboarding.Databricks.Facade.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace IdbInvest.Offboarding.Databricks.Facade.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddOptions<DatabricksOptions>()
            .Bind(configuration.GetSection(DatabricksOptions.SectionName))
            .Validate(o => Uri.TryCreate(o.Host, UriKind.Absolute, out _), "Databricks Host must be an absolute URI.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.WarehouseId), "Databricks WarehouseId is required.")
            .Validate(o => o.WaitTimeoutSeconds is > 0 and <= 50, "Databricks WaitTimeoutSeconds must be between 1 and 50 seconds.")
            .Validate(o => o.PollIntervalMilliseconds is >= 250 and <= 10000, "Databricks PollIntervalMilliseconds must be between 250 and 10000 milliseconds.")
            .Validate(o => o.MaxPollSeconds is > 0 and <= 300, "Databricks MaxPollSeconds must be between 1 and 300 seconds.")
            .ValidateOnStart();

        var mode = configuration[$"{DatabricksOptions.SectionName}:AuthenticationMode"] ?? "DefaultAzureCredential";

        if (mode.Equals("PAT", StringComparison.OrdinalIgnoreCase))
        {
            if (!environment.IsDevelopment())
                throw new InvalidOperationException("Databricks PAT authentication is allowed only in the Development environment. Azure-hosted environments must use managed identity/DefaultAzureCredential.");

            services.AddSingleton<IDatabricksTokenProvider, StaticDatabricksTokenProvider>();
        }
        else if (mode.Equals("DefaultAzureCredential", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IDatabricksTokenProvider, DefaultAzureCredentialDatabricksTokenProvider>();
        }
        else
        {
            throw new InvalidOperationException($"Unsupported Databricks authentication mode '{mode}'. Supported values: DefaultAzureCredential, PAT (Development only).");
        }

        services.AddHttpClient<IDatabricksRepository, DatabricksStatementRepository>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<DatabricksOptions>>().Value;
            client.BaseAddress = new Uri(options.Host.TrimEnd('/'));
            client.Timeout = TimeSpan.FromSeconds(Math.Max(60, options.MaxPollSeconds + 10));
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        }).AddStandardResilienceHandler(options =>
        {
            options.Retry.MaxRetryAttempts = 2;
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(60);
        });

        return services;
    }
}
