using IdbInvest.Offboarding.Databricks.Facade.Core.Interfaces;
using IdbInvest.Offboarding.Databricks.Facade.Infrastructure.Auth;
using IdbInvest.Offboarding.Databricks.Facade.Infrastructure.Databricks;
using IdbInvest.Offboarding.Databricks.Facade.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IdbInvest.Offboarding.Databricks.Facade.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabricksOptions>().Bind(configuration.GetSection(DatabricksOptions.SectionName)).Validate(o =>
            Uri.TryCreate(o.Host, UriKind.Absolute, out _) && !string.IsNullOrWhiteSpace(o.WarehouseId), "Databricks Host and WarehouseId are required.").ValidateOnStart();

        var mode = configuration[$"{DatabricksOptions.SectionName}:AuthenticationMode"] ?? "DefaultAzureCredential";
        if (mode.Equals("PAT", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IDatabricksTokenProvider, StaticDatabricksTokenProvider>();
        else
            services.AddSingleton<IDatabricksTokenProvider, DefaultAzureCredentialDatabricksTokenProvider>();

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
