using System.Reflection;
using IdbInvest.Offboarding.Databricks.Facade.Functions.Controllers;
using Microsoft.Azure.Functions.Worker;

namespace IdbInvest.Offboarding.Databricks.Facade.Tests;

public sealed class HealthEndpointContractTests
{
    [Fact]
    public void Corporate_health_endpoint_is_exposed_as_api_health()
    {
        var method = typeof(HealthController).GetMethod("CorporateHealthAsync", BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(method);

        var function = method!.GetCustomAttribute<FunctionAttribute>();
        Assert.NotNull(function);
        Assert.Equal("Health", function!.Name);

        var parameter = method.GetParameters()[0];
        var trigger = parameter.GetCustomAttributes(inherit: false)
            .FirstOrDefault(x => x.GetType().Name == "HttpTriggerAttribute");
        Assert.NotNull(trigger);

        var route = trigger!.GetType().GetProperty("Route")?.GetValue(trigger) as string;
        Assert.Equal("health", route);
    }

    [Fact]
    public void Healthcheck_endpoint_is_exposed_with_expected_route()
    {
        var method = typeof(HealthController).GetMethod("HealthCheckAsync", BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(method);

        var function = method!.GetCustomAttribute<FunctionAttribute>();
        Assert.NotNull(function);
        Assert.Equal("OffboardingHealthCheck", function!.Name);

        var parameter = method.GetParameters()[0];
        var trigger = parameter.GetCustomAttributes(inherit: false)
            .FirstOrDefault(x => x.GetType().Name == "HttpTriggerAttribute");
        Assert.NotNull(trigger);

        var route = trigger!.GetType().GetProperty("Route")?.GetValue(trigger) as string;
        Assert.Equal("offboarding/v1/healthcheck", route);
    }
}
