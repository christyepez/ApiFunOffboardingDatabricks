namespace IdbInvest.Offboarding.Orchestrator.Infrastructure.DataExposure;

public sealed class NoAuthDataExposureTokenProvider : IDataExposureAccessTokenProvider
{
    public Task<string?> GetTokenAsync(CancellationToken cancellationToken)
        => Task.FromResult<string?>(null);
}
