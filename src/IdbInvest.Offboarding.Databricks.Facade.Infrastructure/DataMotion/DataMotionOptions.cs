namespace IdbInvest.Offboarding.Databricks.Facade.Infrastructure.DataMotion;

public sealed class DataMotionOptions
{
    public const string SectionName = "DataMotion";

    public required string BaseUrl { get; init; }
    public required string Warehouse { get; init; }
    public required string SubscriptionKey { get; init; }

    public string AuthenticationMode { get; init; } = "DefaultAzureCredential";
    public string AccessTokenScope { get; init; } = "2ff814a6-3304-4ab8-85cb-cd0e6f879c1d/.default";
    public string? PersonalAccessToken { get; init; }

    public int PollIntervalMilliseconds { get; init; } = 750;
    public int MaxPollSeconds { get; init; } = 30;
    public int HealthTimeoutSeconds { get; init; } = 5;
    public int MaxResultChunks { get; init; } = 100;
}
