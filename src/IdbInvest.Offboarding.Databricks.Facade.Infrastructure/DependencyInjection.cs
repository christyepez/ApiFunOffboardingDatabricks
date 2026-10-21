using IdbInvest.Offboarding.Databricks.Facade.Core.Interfaces;
using IdbInvest.Offboarding.Databricks.Facade.Infrastructure.Auth;
using IdbInvest.Offboarding.Databricks.Facade.Infrastructure.DataMotion;
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
        var provider = configuration["DataAccess:Provider"] ?? "DataMotion";

        if (provider.Equals("DataMotion", StringComparison.OrdinalIgnoreCase))
            return AddDataMotion(services, configuration, environment);

        if (provider.Equals("DirectDatabricks", StringComparison.OrdinalIgnoreCase))
            return AddDirectDatabricks(services, configuration, environment);

        throw new InvalidOperationException(
            $"Unsupported data access provider '{provider}'. Supported values: DataMotion, DirectDatabricks.");
    }

    private static IServiceCollection AddDataMotion(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddOptions<DataMotionOptions>()
            .Bind(configuration.GetSection(DataMotionOptions.SectionName));

        var mode = configuration[$"{DataMotionOptions.SectionName}:AuthenticationMode"]
                   ?? "DefaultAzureCredential";

        if (mode.Equals("PAT", StringComparison.OrdinalIgnoreCase))
        {
            if (!environment.IsDevelopment())
                throw new InvalidOperationException(
                    "DataMotion PAT authentication is allowed only in the Development environment. " +
                    "Azure-hosted environments must use managed identity/DefaultAzureCredential.");

            services.AddSingleton<IDataMotionAccessTokenProvider, StaticDataMotionTokenProvider>();
        }
        else if (mode.Equals("DefaultAzureCredential", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IDataMotionAccessTokenProvider, DefaultAzureCredentialDataMotionTokenProvider>();
        }
        else
        {
            throw new InvalidOperationException(
                $"Unsupported DataMotion authentication mode '{mode}'. " +
                "Supported values: DefaultAzureCredential, PAT (Development only).");
        }

        services.AddHttpClient<IDatabricksRepository, DataMotionStatementRepository>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<DataMotionOptions>>().Value;
            if (Uri.TryCreate(NormalizeBaseUrl(options.BaseUrl), UriKind.Absolute, out var baseAddress))
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

    private static IServiceCollection AddDirectDatabricks(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddOptions<DatabricksOptions>()
            .Bind(configuration.GetSection(DatabricksOptions.SectionName));

        var mode = configuration[$"{DatabricksOptions.SectionName}:AuthenticationMode"]
                   ?? "DefaultAzureCredential";

        if (mode.Equals("PAT", StringComparison.OrdinalIgnoreCase))
        {
            if (!environment.IsDevelopment())
                throw new InvalidOperationException(
                    "Databricks PAT authentication is allowed only in the Development environment. " +
                    "Azure-hosted environments must use managed identity/DefaultAzureCredential.");

            services.AddSingleton<IDatabricksTokenProvider, StaticDatabricksTokenProvider>();
        }
        else if (mode.Equals("DefaultAzureCredential", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IDatabricksTokenProvider, DefaultAzureCredentialDatabricksTokenProvider>();
        }
        else
        {
            throw new InvalidOperationException(
                $"Unsupported Databricks authentication mode '{mode}'. " +
                "Supported values: DefaultAzureCredential, PAT (Development only).");
        }

        services.AddHttpClient<IDatabricksRepository, DatabricksStatementRepository>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<DatabricksOptions>>().Value;
            if (Uri.TryCreate(options.Host?.TrimEnd('/'), UriKind.Absolute, out var baseAddress))
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

    private static string NormalizeBaseUrl(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            return string.Empty;

        return baseUrl.EndsWith("/", StringComparison.Ordinal)
            ? baseUrl
            : baseUrl + "/";
    }
}
