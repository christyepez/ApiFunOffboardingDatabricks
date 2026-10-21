namespace IdbInvest.Offboarding.Orchestrator.Core.Models;

public enum TargetSystem
{
    PeopleSoft,
    Acbs,
    Workiva
}

public enum OrchestrationStatus
{
    InProgress,
    Completed,
    PartiallyCompleted,
    Failed
}

public sealed record OffboardingCandidate(
    string EmployeeId,
    string Email,
    DateOnly TerminationDate);

public sealed record StartOrchestrationCommand(
    string EmployeeId,
    IReadOnlyCollection<TargetSystem> Targets,
    string? IdempotencyKey = null);

public sealed record TargetExecutionResult(
    TargetSystem Target,
    bool Success,
    string Status,
    string? Message = null);

public sealed record OffboardingOrchestration(
    string OrchestrationId,
    string EmployeeId,
    string IdempotencyKey,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    OrchestrationStatus Status,
    IReadOnlyList<TargetExecutionResult> Targets);

public sealed record OrchestrationExecutionResult(
    OffboardingOrchestration Orchestration,
    bool IsDuplicate);
