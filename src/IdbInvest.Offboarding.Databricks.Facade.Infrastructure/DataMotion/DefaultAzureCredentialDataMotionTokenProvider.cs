using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Options;

namespace IdbInvest.Offboarding.Databricks.Facade.Infrastructure.DataMotion;

public sealed class DefaultAzureCredentialDataMotionTokenProvider : IDataMotionAccessTokenProvider
{
    private readonly TokenCredential _credential = new DefaultAzureCredential();
    private readonly DataMotionOptions _options;

    public DefaultAzureCredentialDataMotionTokenProvider(IOptions<DataMotionOptions> options)
        => _options = options.Value;

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        var token = await _credential.GetTokenAsync(
            new TokenRequestContext([_options.AccessTokenScope]),
            cancellationToken);

        return token.Token;
    }
}
