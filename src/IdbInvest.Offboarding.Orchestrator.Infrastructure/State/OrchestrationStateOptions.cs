namespace IdbInvest.Offboarding.Orchestrator.Infrastructure.State;

public sealed class OrchestrationStateOptions
{
    public const string SectionName = "OrchestrationState";

    public string Provider { get; init; } = "InMemory";
    public string TableName { get; init; } = "OffboardingOrchestrations";
    public string? ConnectionString { get; init; }
    public string? ServiceUri { get; init; }
}
