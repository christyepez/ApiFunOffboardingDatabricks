namespace IdbInvest.Offboarding.Orchestrator.Core.Exceptions;

public sealed class OffboardingCandidateNotFoundException(string employeeId)
    : Exception($"Offboarding candidate '{employeeId}' was not found.");

public sealed class InvalidOrchestrationRequestException(string message)
    : Exception(message);
