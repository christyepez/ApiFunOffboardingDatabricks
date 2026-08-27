using IdbInvest.Offboarding.Databricks.Facade.Core.Models;

namespace IdbInvest.Offboarding.Databricks.Facade.Core.Interfaces;

public interface IResourceRegistry
{
    ResourceDefinition GetRequired(string resource);
    IReadOnlyCollection<ResourceDefinition> GetAll();
}
