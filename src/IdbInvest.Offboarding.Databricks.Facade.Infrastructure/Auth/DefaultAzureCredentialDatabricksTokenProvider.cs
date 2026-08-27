using Azure.Core;
using Azure.Identity;
using IdbInvest.Offboarding.Databricks.Facade.Core.Interfaces;
using IdbInvest.Offboarding.Databricks.Facade.Infrastructure.Databricks;
using Microsoft.Extensions.Options;

namespace IdbInvest.Offboarding.Databricks.Facade.Infrastructure.Auth;

public sealed class DefaultAzureCredentialDatabricksTokenProvider : IDatabricksTokenProvider
{
    private readonly TokenCredential _credential = new DefaultAzureCredential();
    private readonly DatabricksOptions _options;
    public DefaultAzureCredentialDatabricksTokenProvider(IOptions<DatabricksOptions> options) => _options = options.Value;
    public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        var token = await _credential.GetTokenAsync(new TokenRequestContext([_options.OAuthScope]), cancellationToken);
        return token.Token;
    }
}
