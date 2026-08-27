using System.Text.Json;
using IdbInvest.Offboarding.Databricks.Facade.Core.Exceptions;
using IdbInvest.Offboarding.Databricks.Facade.Core.Interfaces;
using IdbInvest.Offboarding.Databricks.Facade.Core.Models;

namespace IdbInvest.Offboarding.Databricks.Facade.Application.Registry;

public sealed class JsonResourceRegistry : IResourceRegistry
{
    private readonly IReadOnlyDictionary<string, ResourceDefinition> _resources;

    public JsonResourceRegistry(string filePath)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException("Resource definition file was not found.", filePath);
        var json = File.ReadAllText(filePath);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var config = JsonSerializer.Deserialize<ResourceConfiguration>(json, options)
                     ?? throw new InvalidOperationException("Resource definition file is empty or invalid.");
        _resources = config.Resources.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        Validate();
    }

    public ResourceDefinition GetRequired(string resource) =>
        _resources.TryGetValue(resource, out var definition) ? definition : throw new ResourceNotFoundException(resource);

    public IReadOnlyCollection<ResourceDefinition> GetAll() => _resources.Values.ToArray();

    private void Validate()
    {
        foreach (var resource in _resources.Values)
        {
            if (string.IsNullOrWhiteSpace(resource.Source) || resource.Fields.Count == 0)
                throw new InvalidOperationException($"Resource '{resource.Name}' has an invalid configuration.");
            foreach (var field in resource.DefaultFields)
                if (!resource.Fields.TryGetValue(field, out var def) || !def.Selectable)
                    throw new InvalidOperationException($"Default field '{field}' for '{resource.Name}' is invalid.");
        }
    }

    private sealed class ResourceConfiguration
    {
        public List<ResourceDefinition> Resources { get; init; } = [];
    }
}
