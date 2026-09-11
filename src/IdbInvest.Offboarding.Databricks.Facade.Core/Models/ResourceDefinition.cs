namespace IdbInvest.Offboarding.Databricks.Facade.Core.Models;

public sealed class ResourceDefinition
{
    public required string Name { get; init; }
    public required string Source { get; init; }
    public string Model { get; init; } = "generic-resource";
    public string ContractVersion { get; init; } = "1.0";
    public required IReadOnlyDictionary<string, FieldDefinition> Fields { get; init; }
    public required IReadOnlyList<string> DefaultFields { get; init; }
    public string? DefaultSort { get; init; }
    public IReadOnlyList<RequiredFilterDefinition> RequiredFilters { get; init; } = [];
    public LookbackDefinition? Lookback { get; init; }
    public int MaxPageSize { get; init; } = 1000;
}

public sealed class FieldDefinition
{
    public required string Column { get; init; }
    public string Type { get; init; } = "STRING";
    public bool Selectable { get; init; } = true;
    public bool Filterable { get; init; }
    public bool Sortable { get; init; }
}

public sealed class RequiredFilterDefinition
{
    public required string Field { get; init; }
    public string Operator { get; init; } = "eq";
    public required string Value { get; init; }
}

public sealed class LookbackDefinition
{
    public required string Field { get; init; }
    public int Days { get; init; } = 30;
}
