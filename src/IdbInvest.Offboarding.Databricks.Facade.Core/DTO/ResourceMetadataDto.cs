namespace IdbInvest.Offboarding.Databricks.Facade.Core.DTO;

public sealed record ResourceMetadataDto(
    string Resource,
    string Model,
    string ContractVersion,
    IReadOnlyList<ResourceFieldDto> Fields,
    int MaxPageSize);

public sealed record ResourceFieldDto(
    string Name,
    string Type,
    bool Filterable,
    bool Sortable,
    bool Selectable);
