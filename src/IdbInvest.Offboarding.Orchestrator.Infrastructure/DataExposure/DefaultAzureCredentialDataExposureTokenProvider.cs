using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Options;

namespace IdbInvest.Offboarding.Orchestrator.Infrastructure.DataExposure;

public sealed class DefaultAzureCredentialDataExposureTokenProvider(
    IOptions<DataExposureOptions> options) : IDataExposureAccessTokenProvider
{
    private readonly TokenCredential _credential = new DefaultAzureCredential();
    private readonly DataExposureOptions _options = options.Value;

    public async Task<string?> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ResourceScope))
            throw new InvalidOperationException("DataExposure:ResourceScope is required.");

        var token = await _credential.GetTokenAsync(
            new TokenRequestContext([_options.ResourceScope]),
            cancellationToken);

        return token.Token;
    }
}
