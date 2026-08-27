namespace IdbInvest.Offboarding.Databricks.Facade.Infrastructure.Databricks;

public sealed class DatabricksOptions
{
    public const string SectionName = "Databricks";
    public required string Host { get; init; }
    public required string WarehouseId { get; init; }
    public string AuthenticationMode { get; init; } = "DefaultAzureCredential";
    public string OAuthScope { get; init; } = "2ff814a6-3304-4ab8-85cb-cd0e6f879c1d/.default";
    public string? PersonalAccessToken { get; init; }
    public int WaitTimeoutSeconds { get; init; } = 30;
    public int PollIntervalMilliseconds { get; init; } = 750;
    public int MaxPollSeconds { get; init; } = 30;
}
