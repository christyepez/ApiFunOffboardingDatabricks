using IdbInvest.Offboarding.Orchestrator.Application.Interfaces;
using IdbInvest.Offboarding.Orchestrator.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace IdbInvest.Offboarding.Orchestrator.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddOrchestratorApplication(this IServiceCollection services)
    {
        services.AddScoped<IOffboardingOrchestrator, OffboardingOrchestrator>();
        return services;
    }
}
