using IdbInvest.Offboarding.Orchestrator.Core.Models;

namespace IdbInvest.Offboarding.Orchestrator.Application.Interfaces;

public interface IOffboardingOrchestrator
{
    Task<OrchestrationExecutionResult> ExecuteAsync(
        StartOrchestrationCommand command,
        CancellationToken cancellationToken);

    Task<OffboardingOrchestration?> GetAsync(
        string orchestrationId,
        CancellationToken cancellationToken);

    Task<OrchestratorReadiness> GetReadinessAsync(
        CancellationToken cancellationToken);
}
