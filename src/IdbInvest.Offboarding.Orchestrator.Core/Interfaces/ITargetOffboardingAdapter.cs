using IdbInvest.Offboarding.Orchestrator.Core.Models;

namespace IdbInvest.Offboarding.Orchestrator.Core.Interfaces;

public interface ITargetOffboardingAdapter
{
    TargetSystem Target { get; }

    Task<TargetExecutionResult> ExecuteAsync(
        OffboardingCandidate candidate,
        string orchestrationId,
        CancellationToken cancellationToken);
}
