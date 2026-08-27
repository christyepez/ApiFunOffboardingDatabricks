using IdbInvest.Offboarding.Databricks.Facade.Application.Query;
using IdbInvest.Offboarding.Databricks.Facade.Application.Registry;
using IdbInvest.Offboarding.Databricks.Facade.Application.Services;
using IdbInvest.Offboarding.Databricks.Facade.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace IdbInvest.Offboarding.Databricks.Facade.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, string resourceDefinitionPath)
    {
        services.AddSingleton<IResourceRegistry>(_ => new JsonResourceRegistry(resourceDefinitionPath));
        services.AddSingleton<IQueryBuilder, QueryBuilder>();
        services.AddScoped<IQueryService, QueryService>();
        return services;
    }
}
