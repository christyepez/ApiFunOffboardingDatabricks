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

        services.AddSingleton<IOrchestrationStateStore, InMemoryOrchestrationStateStore>();

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
