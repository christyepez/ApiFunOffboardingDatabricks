using IdbInvest.Offboarding.Databricks.Facade.Infrastructure.Auth;
using IdbInvest.Offboarding.Databricks.Facade.Infrastructure.Databricks;
using Microsoft.Extensions.Options;

namespace IdbInvest.Offboarding.Databricks.Facade.Tests;

public sealed class DatabricksTokenProviderTests
{
    [Fact]
    public async Task StaticProvider_ReturnsConfiguredToken()
    {
        var sut = new StaticDatabricksTokenProvider(Options.Create(new DatabricksOptions
        {
            Host = "https://example.azuredatabricks.net",
            WarehouseId = "warehouse",
            PersonalAccessToken = "local-token"
        }));

        var token = await sut.GetTokenAsync(CancellationToken.None);

        Assert.Equal("local-token", token);
    }

    [Fact]
    public async Task StaticProvider_RejectsMissingToken()
    {
        var sut = new StaticDatabricksTokenProvider(Options.Create(new DatabricksOptions
        {
            Host = "https://example.azuredatabricks.net",
            WarehouseId = "warehouse"
        }));

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetTokenAsync(CancellationToken.None));
    }
}
