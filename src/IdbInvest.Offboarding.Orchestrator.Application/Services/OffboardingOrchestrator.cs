using IdbInvest.Offboarding.Orchestrator.Application.Interfaces;
using IdbInvest.Offboarding.Orchestrator.Core.Exceptions;
using IdbInvest.Offboarding.Orchestrator.Core.Interfaces;
using IdbInvest.Offboarding.Orchestrator.Core.Models;

namespace IdbInvest.Offboarding.Orchestrator.Application.Services;

public sealed class OffboardingOrchestrator(
    IOffboardingCandidateProvider candidateProvider,
    IEnumerable<ITargetOffboardingAdapter> adapters,
    IOrchestrationStateStore stateStore) : IOffboardingOrchestrator
{
    private readonly IReadOnlyDictionary<TargetSystem, ITargetOffboardingAdapter> _adapters =
        adapters.ToDictionary(x => x.Target);

    public async Task<OrchestrationExecutionResult> ExecuteAsync(
        StartOrchestrationCommand command,
        CancellationToken cancellationToken)
    {
        Validate(command);

        var targets = command.Targets.Distinct().Order().ToArray();
        var idempotencyKey = string.IsNullOrWhiteSpace(command.IdempotencyKey)
            ? BuildIdempotencyKey(command.EmployeeId, targets)
            : command.IdempotencyKey.Trim();

        var existing = await stateStore.GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
        if (existing is not null)
            return new OrchestrationExecutionResult(existing, true);

        var candidate = await candidateProvider.GetByEmployeeIdAsync(
            command.EmployeeId.Trim(),
            cancellationToken)
            ?? throw new OffboardingCandidateNotFoundException(command.EmployeeId);

        foreach (var target in targets)
        {
            if (!_adapters.ContainsKey(target))
                throw new InvalidOrchestrationRequestException(
                    $"No offboarding adapter is registered for target '{target}'.");
        }

        var orchestrationId = Guid.NewGuid().ToString("D");
        var startedAt = DateTimeOffset.UtcNow;
        var inProgress = new OffboardingOrchestration(
            orchestrationId,
            candidate.EmployeeId,
            idempotencyKey,
            startedAt,
            null,
            OrchestrationStatus.InProgress,
            []);

        await stateStore.SaveAsync(inProgress, cancellationToken);

        var results = new List<TargetExecutionResult>(targets.Length);
        foreach (var target in targets)
        {
            results.Add(await _adapters[target].ExecuteAsync(
                candidate,
                orchestrationId,
                cancellationToken));
        }

        var status = ResolveStatus(results);
        var completed = inProgress with
        {
            CompletedAtUtc = DateTimeOffset.UtcNow,
            Status = status,
            Targets = results
        };

        await stateStore.SaveAsync(completed, cancellationToken);
        return new OrchestrationExecutionResult(completed, false);
    }

    public Task<OffboardingOrchestration?> GetAsync(
        string orchestrationId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(orchestrationId))
            throw new InvalidOrchestrationRequestException("orchestrationId is required.");

        return stateStore.GetByIdAsync(orchestrationId.Trim(), cancellationToken);
    }

    public async Task<OrchestratorReadiness> GetReadinessAsync(
        CancellationToken cancellationToken)
    {
        var dataExposureAvailable = await IsAvailableAsync(
            candidateProvider.PingAsync,
            cancellationToken);
        var stateStoreAvailable = await IsAvailableAsync(
            stateStore.PingAsync,
            cancellationToken);

        return new OrchestratorReadiness(
            dataExposureAvailable && stateStoreAvailable,
            dataExposureAvailable,
            stateStoreAvailable);
    }

    private static async Task<bool> IsAvailableAsync(
        Func<CancellationToken, Task<bool>> probe,
        CancellationToken cancellationToken)
    {
        try
        {
            return await probe(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    private static void Validate(StartOrchestrationCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.EmployeeId))
            throw new InvalidOrchestrationRequestException("employeeId is required.");

        if (command.Targets is null || command.Targets.Count == 0)
            throw new InvalidOrchestrationRequestException("At least one target system is required.");
    }

    private static string BuildIdempotencyKey(
        string employeeId,
        IEnumerable<TargetSystem> targets)
        => $"offboarding:{employeeId.Trim().ToUpperInvariant()}:{string.Join(',', targets)}";

    private static OrchestrationStatus ResolveStatus(IReadOnlyCollection<TargetExecutionResult> results)
    {
        var successCount = results.Count(x => x.Success);
        return successCount switch
        {
            0 => OrchestrationStatus.Failed,
            var count when count == results.Count => OrchestrationStatus.Completed,
            _ => OrchestrationStatus.PartiallyCompleted
        };
    }
}
