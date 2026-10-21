namespace IdbInvest.Offboarding.Orchestrator.Infrastructure.DataExposure;

public sealed class DataExposureOptions
{
    public const string SectionName = "DataExposure";

    public string BaseUrl { get; init; } = "http://localhost:7071/api/";
    public string AuthenticationMode { get; init; } = "None";
    public string ResourceScope { get; init; } = string.Empty;
    public string SubscriptionKey { get; init; } = string.Empty;
}
