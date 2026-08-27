using IdbInvest.Offboarding.Databricks.Facade.Core.Models;

namespace IdbInvest.Offboarding.Databricks.Facade.Core.Interfaces;

public interface IDatabricksRepository
{
    Task<DatabricksQueryResult> QueryAsync(QueryPlan plan, CancellationToken cancellationToken);
    Task<long> CountAsync(CountQueryPlan plan, CancellationToken cancellationToken);
    Task<bool> PingAsync(CancellationToken cancellationToken);
}
