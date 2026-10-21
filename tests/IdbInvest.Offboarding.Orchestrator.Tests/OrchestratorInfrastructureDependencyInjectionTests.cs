using Azure.Data.Tables;
using IdbInvest.Offboarding.Orchestrator.Core.Interfaces;
using IdbInvest.Offboarding.Orchestrator.Infrastructure;
using IdbInvest.Offboarding.Orchestrator.Infrastructure.State;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace IdbInvest.Offboarding.Orchestrator.Tests;

public sealed class OrchestratorInfrastructureDependencyInjectionTests
{
    [Fact]
    public void AddOrchestratorInfrastructure_UsesInMemoryStateStoreInDevelopment()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["DataExposure:AuthenticationMode"] = "None",
            ["DataExposure:BaseUrl"] = "http://localhost:7071/api/",
            ["OrchestrationState:Provider"] = "InMemory",
            ["TargetAdapters:Mode"] = "Mock"
        });

        services.AddOrchestratorInfrastructure(
            configuration,
            new TestHostEnvironment(Environments.Development));

        using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IOrchestrationStateStore>();

        Assert.IsType<InMemoryOrchestrationStateStore>(store);
    }

    [Fact]
    public void AddOrchestratorInfrastructure_RejectsInMemoryOutsideDevelopment()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["DataExposure:AuthenticationMode"] = "DefaultAzureCredential",
            ["DataExposure:BaseUrl"] = "https://example.invalid/api/",
            ["OrchestrationState:Provider"] = "InMemory",
            ["TargetAdapters:Mode"] = "Mock"
        });

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddOrchestratorInfrastructure(
                configuration,
                new TestHostEnvironment(Environments.Production)));

        Assert.Contains("InMemory", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddOrchestratorInfrastructure_RegistersAzureTableStateStore()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["DataExposure:AuthenticationMode"] = "DefaultAzureCredential",
            ["DataExposure:BaseUrl"] = "https://example.invalid/api/",
            ["OrchestrationState:Provider"] = "AzureTable",
            ["OrchestrationState:TableName"] = "OffboardingOrchestrations",
            ["OrchestrationState:ServiceUri"] = "https://storage.example.invalid/",
            ["TargetAdapters:Mode"] = "Mock"
        });

        services.AddOrchestratorInfrastructure(
            configuration,
            new TestHostEnvironment(Environments.Production));

        using var provider = services.BuildServiceProvider();
        var tableClient = provider.GetRequiredService<TableClient>();
        var store = provider.GetRequiredService<IOrchestrationStateStore>();

        Assert.Equal("OffboardingOrchestrations", tableClient.Name);
        Assert.IsType<AzureTableOrchestrationStateStore>(store);
    }

    [Fact]
    public void AddOrchestratorInfrastructure_RejectsUnsupportedStateProvider()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["DataExposure:AuthenticationMode"] = "None",
            ["DataExposure:BaseUrl"] = "http://localhost:7071/api/",
            ["OrchestrationState:Provider"] = "SqlServer",
            ["TargetAdapters:Mode"] = "Mock"
        });

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddOrchestratorInfrastructure(
                configuration,
                new TestHostEnvironment(Environments.Development)));

        Assert.Contains("SqlServer", ex.Message, StringComparison.Ordinal);
    }

    private static IConfiguration BuildConfiguration(
        IDictionary<string, string?> values)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }
}
