using IdbInvest.Offboarding.Orchestrator.Core.Models;

namespace IdbInvest.Offboarding.Orchestrator.Core.Interfaces;

public interface IOffboardingCandidateProvider
{
    Task<OffboardingCandidate?> GetByEmployeeIdAsync(
        string employeeId,
        CancellationToken cancellationToken);

    Task<bool> PingAsync(CancellationToken cancellationToken);
}
