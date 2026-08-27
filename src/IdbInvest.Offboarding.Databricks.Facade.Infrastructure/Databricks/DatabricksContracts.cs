using System.Text.Json.Serialization;

namespace IdbInvest.Offboarding.Databricks.Facade.Infrastructure.Databricks;

internal sealed record StatementExecuteRequest(
    [property: JsonPropertyName("warehouse_id")] string WarehouseId,
    [property: JsonPropertyName("statement")] string Statement,
    [property: JsonPropertyName("parameters")] IReadOnlyList<StatementParameter> Parameters,
    [property: JsonPropertyName("wait_timeout")] string WaitTimeout,
    [property: JsonPropertyName("disposition")] string Disposition = "INLINE",
    [property: JsonPropertyName("format")] string Format = "JSON_ARRAY");

internal sealed record StatementParameter(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("type")] string Type);

internal sealed class StatementResponse
{
    [JsonPropertyName("statement_id")] public string? StatementId { get; init; }
    [JsonPropertyName("status")] public StatementStatus? Status { get; init; }
    [JsonPropertyName("manifest")] public StatementManifest? Manifest { get; init; }
    [JsonPropertyName("result")] public StatementResult? Result { get; init; }
}
internal sealed class StatementStatus
{
    [JsonPropertyName("state")] public string? State { get; init; }
    [JsonPropertyName("error")] public StatementError? Error { get; init; }
}
internal sealed class StatementError
{
    [JsonPropertyName("error_code")] public string? ErrorCode { get; init; }
    [JsonPropertyName("message")] public string? Message { get; init; }
}
internal sealed class StatementManifest
{
    [JsonPropertyName("schema")] public StatementSchema? Schema { get; init; }
}
internal sealed class StatementSchema
{
    [JsonPropertyName("columns")] public List<StatementColumn> Columns { get; init; } = [];
}
internal sealed class StatementColumn
{
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("type_name")] public string? TypeName { get; init; }
}
internal sealed class StatementResult
{
    [JsonPropertyName("data_array")] public List<List<object?>> DataArray { get; init; } = [];
}
