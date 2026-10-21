namespace IdbInvest.Offboarding.Orchestrator.Infrastructure.DataExposure;

public interface IDataExposureAccessTokenProvider
{
    Task<string?> GetTokenAsync(CancellationToken cancellationToken);
}
