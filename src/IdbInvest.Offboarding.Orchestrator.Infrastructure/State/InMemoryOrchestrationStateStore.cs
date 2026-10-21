using System.Collections.Concurrent;
using IdbInvest.Offboarding.Orchestrator.Core.Interfaces;
using IdbInvest.Offboarding.Orchestrator.Core.Models;

namespace IdbInvest.Offboarding.Orchestrator.Infrastructure.State;

public sealed class InMemoryOrchestrationStateStore : IOrchestrationStateStore
{
    private readonly ConcurrentDictionary<string, OffboardingOrchestration> _byId =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, string> _idByIdempotencyKey =
        new(StringComparer.OrdinalIgnoreCase);

    public Task<OffboardingOrchestration?> GetByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (_idByIdempotencyKey.TryGetValue(idempotencyKey, out var id) &&
            _byId.TryGetValue(id, out var item))
            return Task.FromResult<OffboardingOrchestration?>(item);

        return Task.FromResult<OffboardingOrchestration?>(null);
    }

    public Task<OffboardingOrchestration?> GetByIdAsync(
        string orchestrationId,
        CancellationToken cancellationToken)
    {
        _byId.TryGetValue(orchestrationId, out var item);
        return Task.FromResult<OffboardingOrchestration?>(item);
    }

    public Task SaveAsync(
        OffboardingOrchestration orchestration,
        CancellationToken cancellationToken)
    {
        _byId[orchestration.OrchestrationId] = orchestration;
        _idByIdempotencyKey[orchestration.IdempotencyKey] = orchestration.OrchestrationId;
        return Task.CompletedTask;
    }

    public Task<bool> PingAsync(CancellationToken cancellationToken)
        => Task.FromResult(true);
}
