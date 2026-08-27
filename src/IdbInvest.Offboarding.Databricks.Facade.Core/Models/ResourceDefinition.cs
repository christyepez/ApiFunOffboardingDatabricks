namespace IdbInvest.Offboarding.Databricks.Facade.Core.Models;

public sealed class ResourceDefinition
{
    public required string Name { get; init; }
    public required string Source { get; init; }
    public required IReadOnlyDictionary<string, FieldDefinition> Fields { get; init; }
    public required IReadOnlyList<string> DefaultFields { get; init; }
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
