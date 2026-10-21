namespace IdbInvest.Offboarding.Databricks.Facade.Infrastructure.DataMotion;

public interface IDataMotionAccessTokenProvider
{
    Task<string> GetTokenAsync(CancellationToken cancellationToken);
}
