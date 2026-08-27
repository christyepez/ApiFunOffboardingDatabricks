using IdbInvest.Offboarding.Databricks.Facade.Application.Query;
using IdbInvest.Offboarding.Databricks.Facade.Core.DTO;
using IdbInvest.Offboarding.Databricks.Facade.Core.Exceptions;
using IdbInvest.Offboarding.Databricks.Facade.Core.Models;

namespace IdbInvest.Offboarding.Databricks.Facade.Tests;

public sealed class QueryBuilderTests
{
    private static readonly ResourceDefinition Employees = new()
    {
        Name = "employees",
        Source = "gold.offboarding.vw_employees",
        MaxPageSize = 1000,
        DefaultFields = ["employeeId", "status"],
        Fields = new Dictionary<string, FieldDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["employeeId"] = new() { Column = "employee_id", Type = "STRING", Selectable = true, Filterable = true, Sortable = true },
            ["status"] = new() { Column = "status", Type = "STRING", Selectable = true, Filterable = true, Sortable = true },
            ["secret"] = new() { Column = "secret_value", Type = "STRING", Selectable = false, Filterable = false, Sortable = false }
        }
    };

    [Fact]
    public void Build_UsesWhitelistAndParameters()
    {
        var sut = new QueryBuilder();
        var plan = sut.Build(Employees, new QueryRequestDto("employeeId,status", ["status:eq:ACTIVE"], "-employeeId", 1, 100, false));
        Assert.Contains("`status` = :p0", plan.Sql);
        Assert.DoesNotContain("ACTIVE", plan.Sql);
        Assert.Single(plan.Parameters);
        Assert.Equal("ACTIVE", plan.Parameters[0].Value);
    }

    [Fact]
    public void Build_RejectsUnknownField()
    {
        var sut = new QueryBuilder();
        Assert.Throws<InvalidQueryException>(() => sut.Build(Employees, new QueryRequestDto("employeeId,password", [], null, 1, 100, false)));
    }

    [Fact]
    public void Build_RejectsPageSizeAboveResourceLimit()
    {
        var sut = new QueryBuilder();
        Assert.Throws<InvalidQueryException>(() => sut.Build(Employees, new QueryRequestDto(null, [], null, 1, 1001, false)));
    }
}
