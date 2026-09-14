using IdbInvest.Offboarding.Databricks.Facade.Core.Interfaces;
using IdbInvest.Offboarding.Databricks.Facade.Infrastructure;
using IdbInvest.Offboarding.Databricks.Facade.Infrastructure.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace IdbInvest.Offboarding.Databricks.Facade.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddInfrastructure_RegistersManagedIdentityDependencies()
    {
        var services = new ServiceCollection();
        var config = BuildConfig("DefaultAzureCredential");

        services.AddInfrastructure(config, new TestHostEnvironment("Production"));
        using var provider = services.BuildServiceProvider();

        Assert.IsType<DefaultAzureCredentialDatabricksTokenProvider>(provider.GetRequiredService<IDatabricksTokenProvider>());
        Assert.NotNull(provider.GetRequiredService<IDatabricksRepository>());
    }

    [Fact]
    public void AddInfrastructure_RegistersPatProvider_InDevelopment()
    {
        var services = new ServiceCollection();
        var config = BuildConfig("PAT");

        services.AddInfrastructure(config, new TestHostEnvironment(Environments.Development));
        using var provider = services.BuildServiceProvider();

        Assert.IsType<StaticDatabricksTokenProvider>(provider.GetRequiredService<IDatabricksTokenProvider>());
    }

    [Fact]
    public void AddInfrastructure_RejectsPatOutsideDevelopment()
    {
        var services = new ServiceCollection();
        var config = BuildConfig("PAT");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(config, new TestHostEnvironment(Environments.Production)));

        Assert.Contains("only in the Development environment", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddInfrastructure_RejectsUnsupportedAuthenticationMode()
    {
        var services = new ServiceCollection();
        var config = BuildConfig("UnknownMode");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(config, new TestHostEnvironment(Environments.Development)));

        Assert.Contains("Unsupported Databricks authentication mode", ex.Message, StringComparison.Ordinal);
    }

    private static IConfiguration BuildConfig(string mode) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Databricks:Host"] = "https://example.azuredatabricks.net",
            ["Databricks:WarehouseId"] = "warehouse",
            ["Databricks:AuthenticationMode"] = mode,
            ["Databricks:PersonalAccessToken"] = "local-token",
            ["Databricks:WaitTimeoutSeconds"] = "30",
            ["Databricks:PollIntervalMilliseconds"] = "750",
            ["Databricks:MaxPollSeconds"] = "30",
            ["Databricks:MaxResultChunks"] = "100"
        }).Build();

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
