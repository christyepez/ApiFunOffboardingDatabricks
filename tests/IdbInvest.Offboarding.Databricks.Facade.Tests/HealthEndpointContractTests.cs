using System.Reflection;
using IdbInvest.Offboarding.Databricks.Facade.Functions.Controllers;
using Microsoft.Azure.Functions.Worker;

namespace IdbInvest.Offboarding.Databricks.Facade.Tests;

public sealed class HealthEndpointContractTests
{
    [Theory]
    [InlineData("CorporateHealthAsync", "Health", "health")]
    [InlineData("DependencyHealthAsync", "OffboardingHealth", "offboarding/v1/health")]
    public void Health_endpoints_are_exposed_with_expected_routes(string methodName, string functionName, string expectedRoute)
    {
        var method = typeof(HealthController).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(method);

        var function = method!.GetCustomAttribute<FunctionAttribute>();
        Assert.NotNull(function);
        Assert.Equal(functionName, function!.Name);

        var parameter = method.GetParameters()[0];
        var trigger = parameter.GetCustomAttributes(inherit: false)
            .FirstOrDefault(x => x.GetType().Name == "HttpTriggerAttribute");
        Assert.NotNull(trigger);

        var route = trigger!.GetType().GetProperty("Route")?.GetValue(trigger) as string;
        Assert.Equal(expectedRoute, route);
    }
}
