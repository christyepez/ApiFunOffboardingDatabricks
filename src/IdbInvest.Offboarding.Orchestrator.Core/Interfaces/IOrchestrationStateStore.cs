using IdbInvest.Offboarding.Orchestrator.Core.Models;

namespace IdbInvest.Offboarding.Orchestrator.Core.Interfaces;

public interface IOrchestrationStateStore
{
    Task<OffboardingOrchestration?> GetByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<OffboardingOrchestration?> GetByIdAsync(
        string orchestrationId,
        CancellationToken cancellationToken);

    Task SaveAsync(
        OffboardingOrchestration orchestration,
        CancellationToken cancellationToken);
}
