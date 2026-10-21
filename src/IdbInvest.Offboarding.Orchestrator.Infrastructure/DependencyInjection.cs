using Azure.Data.Tables;
using Azure.Identity;
using IdbInvest.Offboarding.Orchestrator.Core.Interfaces;
using IdbInvest.Offboarding.Orchestrator.Core.Models;
using IdbInvest.Offboarding.Orchestrator.Infrastructure.DataExposure;
using IdbInvest.Offboarding.Orchestrator.Infrastructure.State;
using IdbInvest.Offboarding.Orchestrator.Infrastructure.Targets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace IdbInvest.Offboarding.Orchestrator.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddOrchestratorInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        AddDataExposure(services, configuration, environment);
        AddStateStore(services, configuration, environment);
        AddTargetAdapters(services, configuration);

        return services;
    }

    private static void AddDataExposure(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddOptions<DataExposureOptions>()
            .Bind(configuration.GetSection(DataExposureOptions.SectionName));

        var authMode = configuration[$"{DataExposureOptions.SectionName}:AuthenticationMode"]
                       ?? "DefaultAzureCredential";

        if (authMode.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            if (!environment.IsDevelopment())
                throw new InvalidOperationException(
                    "DataExposure authentication mode 'None' is allowed only in Development.");

            services.AddSingleton<IDataExposureAccessTokenProvider, NoAuthDataExposureTokenProvider>();
        }
        else if (authMode.Equals("DefaultAzureCredential", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IDataExposureAccessTokenProvider, DefaultAzureCredentialDataExposureTokenProvider>();
        }
        else
        {
            throw new InvalidOperationException(
                $"Unsupported DataExposure authentication mode '{authMode}'. " +
                "Supported values: None (Development only), DefaultAzureCredential.");
        }

        services.AddHttpClient<IOffboardingCandidateProvider, DataExposureCandidateProvider>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<DataExposureOptions>>().Value;
            if (!Uri.TryCreate(NormalizeBaseUrl(options.BaseUrl), UriKind.Absolute, out var baseAddress))
                throw new InvalidOperationException("DataExposure:BaseUrl must be a valid absolute URL.");

            client.BaseAddress = baseAddress;
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        }).AddStandardResilienceHandler(options =>
        {
            options.Retry.MaxRetryAttempts = 2;
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
        });
    }

    private static void AddStateStore(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddOptions<OrchestrationStateOptions>()
            .Bind(configuration.GetSection(OrchestrationStateOptions.SectionName));

        var provider = configuration[$"{OrchestrationStateOptions.SectionName}:Provider"]
                       ?? (environment.IsDevelopment() ? "InMemory" : "AzureTable");

        if (provider.Equals("InMemory", StringComparison.OrdinalIgnoreCase))
        {
            if (!environment.IsDevelopment())
                throw new InvalidOperationException(
                    "OrchestrationState provider 'InMemory' is allowed only in Development.");

            services.AddSingleton<IOrchestrationStateStore, InMemoryOrchestrationStateStore>();
            return;
        }

        if (!provider.Equals("AzureTable", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Unsupported OrchestrationState provider '{provider}'. " +
                "Supported values: InMemory (Development only), AzureTable.");

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<OrchestrationStateOptions>>().Value;

            if (!string.IsNullOrWhiteSpace(options.ConnectionString))
                return new TableClient(options.ConnectionString, options.TableName);

            if (!Uri.TryCreate(options.ServiceUri, UriKind.Absolute, out var serviceUri))
                throw new InvalidOperationException(
                    "OrchestrationState:ServiceUri must be configured when ConnectionString is not provided.");

            return new TableClient(
                serviceUri,
                options.TableName,
                new DefaultAzureCredential());
        });

        services.AddSingleton<IOrchestrationStateStore, AzureTableOrchestrationStateStore>();
    }

    private static void AddTargetAdapters(
        IServiceCollection services,
        IConfiguration configuration)
    {
        var targetMode = configuration["TargetAdapters:Mode"] ?? "Mock";
        if (!targetMode.Equals("Mock", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Unsupported TargetAdapters:Mode '{targetMode}'. Current supported value: Mock.");

        services.AddSingleton<ITargetOffboardingAdapter>(
            new MockTargetOffboardingAdapter(TargetSystem.PeopleSoft));
        services.AddSingleton<ITargetOffboardingAdapter>(
            new MockTargetOffboardingAdapter(TargetSystem.Acbs));
        services.AddSingleton<ITargetOffboardingAdapter>(
            new MockTargetOffboardingAdapter(TargetSystem.Workiva));
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
