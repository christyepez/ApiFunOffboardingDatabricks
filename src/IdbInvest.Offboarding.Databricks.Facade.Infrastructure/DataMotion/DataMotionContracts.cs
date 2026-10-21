using System.Text.Json.Serialization;

namespace IdbInvest.Offboarding.Databricks.Facade.Infrastructure.DataMotion;

internal sealed record DataMotionQueryRequest(
    [property: JsonPropertyName("warehouse")] string Warehouse,
    [property: JsonPropertyName("query")] string Query);
