using System.Text.Json;
using IdbInvest.Offboarding.Databricks.Facade.Core.Exceptions;
using IdbInvest.Offboarding.Databricks.Facade.Core.Interfaces;
using IdbInvest.Offboarding.Databricks.Facade.Core.Models;

namespace IdbInvest.Offboarding.Databricks.Facade.Application.Registry;

public sealed class JsonResourceRegistry : IResourceRegistry
{
    private static readonly HashSet<string> AllowedOperators = new(StringComparer.OrdinalIgnoreCase)
    { "eq", "ne", "gt", "gte", "lt", "lte", "contains" };

    private readonly IReadOnlyDictionary<string, ResourceDefinition> _resources;

    public JsonResourceRegistry(string filePath)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException("Resource definition file was not found.", filePath);
        var json = File.ReadAllText(filePath);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var config = JsonSerializer.Deserialize<ResourceConfiguration>(json, options)
                     ?? throw new InvalidOperationException("Resource definition file is empty or invalid.");
        _resources = config.Resources.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        ValidateResources();
    }

    public ResourceDefinition GetRequired(string resource) =>
        _resources.TryGetValue(resource, out var definition) ? definition : throw new ResourceNotFoundException(resource);

    public IReadOnlyCollection<ResourceDefinition> GetAll() => _resources.Values.ToArray();

    private void ValidateResources()
    {
        foreach (var resource in _resources.Values) ValidateResource(resource);
    }

    private static void ValidateResource(ResourceDefinition resource)
    {
        ValidateRequiredProperties(resource);
        ValidateDefaultFields(resource);
        ValidateDefaultSort(resource);
        ValidateRequiredFilters(resource);
        ValidateLookback(resource);
    }

    private static void ValidateRequiredProperties(ResourceDefinition resource)
    {
        if (string.IsNullOrWhiteSpace(resource.Name) || string.IsNullOrWhiteSpace(resource.Source) || resource.Fields.Count == 0)
            throw new InvalidOperationException("Resource definitions require name, source, and at least one field.");
        if (string.IsNullOrWhiteSpace(resource.Model) || string.IsNullOrWhiteSpace(resource.ContractVersion))
            throw new InvalidOperationException($"Resource '{resource.Name}' requires model and contractVersion.");
        if (resource.MaxPageSize is < 1 or > 5000)
            throw new InvalidOperationException($"Resource '{resource.Name}' maxPageSize must be between 1 and 5000.");
    }

    private static void ValidateDefaultFields(ResourceDefinition resource)
    {
        foreach (var field in resource.DefaultFields)
        {
            if (!resource.Fields.TryGetValue(field, out var definition) || !definition.Selectable)
                throw new InvalidOperationException($"Default field '{field}' for '{resource.Name}' is invalid.");
        }
    }

    private static void ValidateDefaultSort(ResourceDefinition resource)
    {
        if (string.IsNullOrWhiteSpace(resource.DefaultSort)) return;

        foreach (var token in resource.DefaultSort.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var fieldName = token.StartsWith('-') ? token[1..] : token;
            if (!resource.Fields.TryGetValue(fieldName, out var field) || !field.Sortable)
                throw new InvalidOperationException($"Default sort field '{fieldName}' for '{resource.Name}' is invalid.");
        }
    }

    private static void ValidateRequiredFilters(ResourceDefinition resource)
    {
        foreach (var required in resource.RequiredFilters)
        {
            ValidateFilterableField(resource, required.Field, "Required filter");
            if (!AllowedOperators.Contains(required.Operator))
                throw new InvalidOperationException($"Required filter operator '{required.Operator}' for '{resource.Name}' is invalid.");
        }
    }

    private static void ValidateLookback(ResourceDefinition resource)
    {
        if (resource.Lookback is null) return;

        ValidateFilterableField(resource, resource.Lookback.Field, "Lookback");
        if (resource.Lookback.Days is < 1 or > 3650)
            throw new InvalidOperationException($"Lookback days for '{resource.Name}' must be between 1 and 3650.");
    }

    private static void ValidateFilterableField(ResourceDefinition resource, string fieldName, string usage)
    {
        if (!resource.Fields.TryGetValue(fieldName, out var field) || !field.Filterable)
            throw new InvalidOperationException($"{usage} field '{fieldName}' for '{resource.Name}' is invalid.");
    }

    private sealed class ResourceConfiguration
    {
        public List<ResourceDefinition> Resources { get; init; } = [];
    }
}
