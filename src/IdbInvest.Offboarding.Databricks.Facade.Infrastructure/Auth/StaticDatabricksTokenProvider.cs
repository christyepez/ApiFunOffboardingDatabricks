using IdbInvest.Offboarding.Databricks.Facade.Core.Interfaces;
using IdbInvest.Offboarding.Databricks.Facade.Infrastructure.Databricks;
using Microsoft.Extensions.Options;

namespace IdbInvest.Offboarding.Databricks.Facade.Infrastructure.Auth;

public sealed class StaticDatabricksTokenProvider(IOptions<DatabricksOptions> options) : IDatabricksTokenProvider
{
    private readonly DatabricksOptions _options = options.Value;
    public Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.PersonalAccessToken))
            throw new InvalidOperationException("Databricks PersonalAccessToken is missing. PAT mode is intended only for local development.");
        return Task.FromResult(_options.PersonalAccessToken);
    }
}
