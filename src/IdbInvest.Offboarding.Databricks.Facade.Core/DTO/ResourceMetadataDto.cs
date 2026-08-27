namespace IdbInvest.Offboarding.Databricks.Facade.Core.DTO;

public sealed record ResourceMetadataDto(
    string Resource,
    IReadOnlyList<ResourceFieldDto> Fields,
    int MaxPageSize);

public sealed record ResourceFieldDto(
    string Name,
    string Type,
    bool Filterable,
    bool Sortable,
    bool Selectable);
