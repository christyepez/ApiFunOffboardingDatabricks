using IdbInvest.Offboarding.Databricks.Facade.Application.Registry;
using IdbInvest.Offboarding.Databricks.Facade.Core.Exceptions;

namespace IdbInvest.Offboarding.Databricks.Facade.Tests;

public sealed class JsonResourceRegistryTests
{
    [Fact]
    public void LoadsLogicalContractMetadataAndEmailField()
    {
        var path = WriteConfig();
        try
        {
            var registry = new JsonResourceRegistry(path);
            var resource = registry.GetRequired("employees");

            Assert.Equal("employee-offboarding-candidate", resource.Model);
            Assert.Equal("1.0", resource.ContractVersion);
            Assert.Contains("email", resource.DefaultFields);
            Assert.True(resource.Fields["email"].Selectable);
            Assert.True(resource.Fields["email"].Filterable);
            Assert.False(resource.Fields["email"].Sortable);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void UnknownResourceIsRejected()
    {
        var path = WriteConfig();
        try
        {
            var registry = new JsonResourceRegistry(path);
            Assert.Throws<ResourceNotFoundException>(() => registry.GetRequired("unknown"));
        }
        finally { File.Delete(path); }
    }

    private static string WriteConfig()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, """
        {
          "resources": [{
            "name": "employees",
            "model": "employee-offboarding-candidate",
            "contractVersion": "1.0",
            "source": "gold.offboarding.vw_employees",
            "defaultFields": ["employeeId", "email"],
            "fields": {
              "employeeId": { "column": "employee_id", "selectable": true, "filterable": true, "sortable": true },
              "email": { "column": "email", "selectable": true, "filterable": true, "sortable": false }
            }
          }]
        }
        """);
        return path;
    }
}
