namespace IdbInvest.Offboarding.Databricks.Facade.Core.DTO;

public sealed record QueryResponseDto(
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Data,
    QueryMetadataDto Meta);

public sealed record QueryMetadataDto(
    string Resource,
    int Page,
    int PageSize,
    int Returned,
    long? Total,
    bool HasMore,
    string CorrelationId);
