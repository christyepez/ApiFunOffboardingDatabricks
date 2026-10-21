using IdbInvest.Offboarding.Databricks.Facade.Core.Exceptions;
using Microsoft.Extensions.Options;

namespace IdbInvest.Offboarding.Databricks.Facade.Infrastructure.DataMotion;

public sealed class StaticDataMotionTokenProvider : IDataMotionAccessTokenProvider
{
    private readonly DataMotionOptions _options;

    public StaticDataMotionTokenProvider(IOptions<DataMotionOptions> options)
        => _options = options.Value;

    public Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(_options.PersonalAccessToken))
            throw new DependencyUnavailableException("DataMotion PersonalAccessToken is not configured.");

        return Task.FromResult(_options.PersonalAccessToken);
    }
}
