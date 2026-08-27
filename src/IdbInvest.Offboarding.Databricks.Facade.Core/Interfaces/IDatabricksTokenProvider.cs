namespace IdbInvest.Offboarding.Databricks.Facade.Core.Interfaces;

public interface IDatabricksTokenProvider
{
    Task<string> GetTokenAsync(CancellationToken cancellationToken);
}
