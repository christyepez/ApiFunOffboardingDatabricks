using IdbInvest.Offboarding.Databricks.Facade.Core.DTO;
using IdbInvest.Offboarding.Databricks.Facade.Core.Models;

namespace IdbInvest.Offboarding.Databricks.Facade.Core.Interfaces;

public interface IQueryBuilder
{
    QueryPlan Build(ResourceDefinition definition, QueryRequestDto request);
    CountQueryPlan BuildCount(ResourceDefinition definition, QueryRequestDto request);
}
