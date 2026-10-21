using IdbInvest.Offboarding.Orchestrator.Core.Interfaces;
using IdbInvest.Offboarding.Orchestrator.Core.Models;

namespace IdbInvest.Offboarding.Orchestrator.Infrastructure.Targets;

public sealed class MockTargetOffboardingAdapter(TargetSystem target) : ITargetOffboardingAdapter
{
    public TargetSystem Target { get; } = target;

    public Task<TargetExecutionResult> ExecuteAsync(
        OffboardingCandidate candidate,
        string orchestrationId,
        CancellationToken cancellationToken)
        => Task.FromResult(new TargetExecutionResult(
            Target,
            true,
            "MockCompleted",
            "Development mock adapter. No target-system change was executed."));
}
