using IdbInvest.Offboarding.Databricks.Facade.Core.Interfaces;
using IdbInvest.Offboarding.Databricks.Facade.Infrastructure;
using IdbInvest.Offboarding.Databricks.Facade.Infrastructure.DataMotion;
using IdbInvest.Offboarding.Databricks.Facade.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace IdbInvest.Offboarding.Databricks.Facade.Tests;

public sealed class DataMotionDependencyInjectionTests
{
    [Fact]
    public void AddInfrastructure_RegistersDataMotionAsDefaultProvider()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfig("DefaultAzureCredential");

        services.AddInfrastructure(configuration, new TestHostEnvironment(Environments.Production));
        using var provider = services.BuildServiceProvider();

        Assert.IsType<DefaultAzureCredentialDataMotionTokenProvider>(
            provider.GetRequiredService<IDataMotionAccessTokenProvider>());

        Assert.IsType<DataMotionStatementRepository>(
            provider.GetRequiredService<IDatabricksRepository>());
    }

    [Fact]
    public void AddInfrastructure_RegistersStaticTokenProviderInDevelopment()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfig("PAT");

        services.AddInfrastructure(configuration, new TestHostEnvironment(Environments.Development));
        using var provider = services.BuildServiceProvider();

        Assert.IsType<StaticDataMotionTokenProvider>(
            provider.GetRequiredService<IDataMotionAccessTokenProvider>());
    }

    [Fact]
    public void AddInfrastructure_RejectsPatOutsideDevelopment()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfig("PAT");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(configuration, new TestHostEnvironment(Environments.Production)));

        Assert.Contains("DataMotion PAT authentication", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddInfrastructure_RejectsUnsupportedProvider()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataAccess:Provider"] = "Unknown"
            })
            .Build();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(configuration, new TestHostEnvironment(Environments.Development)));

        Assert.Contains("Unsupported data access provider", ex.Message, StringComparison.Ordinal);
    }

    private static IConfiguration BuildConfig(string mode) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DataMotion:BaseUrl"] = "https://idbinvestapi-d.iadb.org/datamotion-analytical-query-data-2/",
            ["DataMotion:Warehouse"] = "warehouse_idbi_offboarding",
            ["DataMotion:SubscriptionKey"] = "subscription-key",
            ["DataMotion:AuthenticationMode"] = mode,
            ["DataMotion:PersonalAccessToken"] = "local-token",
            ["DataMotion:PollIntervalMilliseconds"] = "750",
            ["DataMotion:MaxPollSeconds"] = "30",
            ["DataMotion:HealthTimeoutSeconds"] = "5",
            ["DataMotion:MaxResultChunks"] = "100"
        })
        .Build();

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
