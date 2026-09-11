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
        // Bind configuration without ValidateOnStart so a downstream Databricks
        // configuration problem does not crash the Functions worker before discovery.
        services.AddOptions<DatabricksOptions>()
            .Bind(configuration.GetSection(DatabricksOptions.SectionName));

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
            if (Uri.TryCreate(options.Host?.TrimEnd('/') , UriKind.Absolute, out var baseAddress))
                client.BaseAddress = baseAddress;
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
