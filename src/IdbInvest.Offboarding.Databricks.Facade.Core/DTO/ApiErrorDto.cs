namespace IdbInvest.Offboarding.Databricks.Facade.Core.DTO;

public sealed record ApiErrorDto(
    string Code,
    string Message,
    string CorrelationId,
    IReadOnlyDictionary<string, object?>? Details = null);
