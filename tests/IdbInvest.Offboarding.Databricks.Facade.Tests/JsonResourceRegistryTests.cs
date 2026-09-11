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


    [Fact]
    public void MissingFileIsRejected()
    {
        Assert.Throws<FileNotFoundException>(() => new JsonResourceRegistry(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json")));
    }

    [Fact]
    public void InvalidDefaultFieldIsRejected()
    {
        var path = WriteRawConfig("""
        {"resources":[{"name":"employees","model":"employee-offboarding-candidate","contractVersion":"1.0","source":"gold.offboarding.vw_employees","defaultFields":["missing"],"fields":{"employeeId":{"column":"employee_id","selectable":true,"filterable":true,"sortable":true}}}]}
        """);
        try { Assert.Throws<InvalidOperationException>(() => new JsonResourceRegistry(path)); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void InvalidDefaultSortIsRejected()
    {
        var path = WriteRawConfig("""
        {"resources":[{"name":"employees","model":"employee-offboarding-candidate","contractVersion":"1.0","source":"gold.offboarding.vw_employees","defaultFields":["employeeId"],"defaultSort":"email","fields":{"employeeId":{"column":"employee_id","selectable":true,"filterable":true,"sortable":true},"email":{"column":"email","selectable":true,"filterable":true,"sortable":false}}}]}
        """);
        try { Assert.Throws<InvalidOperationException>(() => new JsonResourceRegistry(path)); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void InvalidRequiredFilterFieldIsRejected()
    {
        var path = WriteRawConfig("""
        {"resources":[{"name":"employees","model":"employee-offboarding-candidate","contractVersion":"1.0","source":"gold.offboarding.vw_employees","defaultFields":["employeeId"],"requiredFilters":[{"field":"email","operator":"eq","value":"x"}],"fields":{"employeeId":{"column":"employee_id","selectable":true,"filterable":true,"sortable":true},"email":{"column":"email","selectable":true,"filterable":false,"sortable":false}}}]}
        """);
        try { Assert.Throws<InvalidOperationException>(() => new JsonResourceRegistry(path)); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void InvalidRequiredFilterOperatorIsRejected()
    {
        var path = WriteRawConfig("""
        {"resources":[{"name":"employees","model":"employee-offboarding-candidate","contractVersion":"1.0","source":"gold.offboarding.vw_employees","defaultFields":["employeeId"],"requiredFilters":[{"field":"employeeId","operator":"regex","value":"x"}],"fields":{"employeeId":{"column":"employee_id","selectable":true,"filterable":true,"sortable":true}}}]}
        """);
        try { Assert.Throws<InvalidOperationException>(() => new JsonResourceRegistry(path)); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void InvalidLookbackFieldIsRejected()
    {
        var path = WriteRawConfig("""
        {"resources":[{"name":"employees","model":"employee-offboarding-candidate","contractVersion":"1.0","source":"gold.offboarding.vw_employees","defaultFields":["employeeId"],"lookback":{"field":"terminationDate","days":30},"fields":{"employeeId":{"column":"employee_id","selectable":true,"filterable":true,"sortable":true},"terminationDate":{"column":"termination_date","selectable":true,"filterable":false,"sortable":true}}}]}
        """);
        try { Assert.Throws<InvalidOperationException>(() => new JsonResourceRegistry(path)); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void InvalidLookbackDaysIsRejected()
    {
        var path = WriteRawConfig("""
        {"resources":[{"name":"employees","model":"employee-offboarding-candidate","contractVersion":"1.0","source":"gold.offboarding.vw_employees","defaultFields":["employeeId"],"lookback":{"field":"terminationDate","days":0},"fields":{"employeeId":{"column":"employee_id","selectable":true,"filterable":true,"sortable":true},"terminationDate":{"column":"termination_date","selectable":true,"filterable":true,"sortable":true}}}]}
        """);
        try { Assert.Throws<InvalidOperationException>(() => new JsonResourceRegistry(path)); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void InvalidMaxPageSizeIsRejected()
    {
        var path = WriteRawConfig("""
        {"resources":[{"name":"employees","model":"employee-offboarding-candidate","contractVersion":"1.0","source":"gold.offboarding.vw_employees","maxPageSize":6000,"defaultFields":["employeeId"],"fields":{"employeeId":{"column":"employee_id","selectable":true,"filterable":true,"sortable":true}}}]}
        """);
        try { Assert.Throws<InvalidOperationException>(() => new JsonResourceRegistry(path)); }
        finally { File.Delete(path); }
    }

    private static string WriteRawConfig(string json)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, json);
        return path;
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
