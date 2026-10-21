using IdbInvest.Offboarding.Databricks.Facade.Application.Query;
using IdbInvest.Offboarding.Databricks.Facade.Core.DTO;
using IdbInvest.Offboarding.Databricks.Facade.Core.Models;

namespace IdbInvest.Offboarding.Databricks.Facade.Tests;

public sealed class ExposureGuardrailTests
{
    private static readonly ResourceDefinition Definition = new()
    {
        Name = "employees",
        Model = "employee-offboarding-candidate",
        ContractVersion = "1.0",
        Source = "gold.offboarding.vw_employees",
        MaxPageSize = 1000,
        DefaultFields = ["employeeId", "status", "terminationDate"],
        DefaultSort = "-terminationDate,employeeId",
        RequiredFilters = [new RequiredFilterDefinition { Field = "status", Operator = "eq", Value = "OFFBOARDED" }],
        Lookback = new LookbackDefinition { Field = "terminationDate", Days = 30 },
        Fields = new Dictionary<string, FieldDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["employeeId"] = new() { Column = "employee_id", Type = "STRING", Selectable = true, Filterable = true, Sortable = true },
            ["status"] = new() { Column = "status", Type = "STRING", Selectable = true, Filterable = true, Sortable = true },
            ["terminationDate"] = new() { Column = "termination_date", Type = "DATE", Selectable = true, Filterable = true, Sortable = true }
        }
    };

    [Fact]
    public void Build_AlwaysAppliesOffboardingEligibilityAndLookback()
    {
        var plan = new QueryBuilder().Build(Definition, new QueryRequestDto(null, [], null, 1, 100, false));

        Assert.Contains("`status` = :p0", plan.Sql);
        Assert.Contains("`termination_date` >= :p1", plan.Sql);
        Assert.DoesNotContain("OFFBOARDED", plan.Sql);
        Assert.Equal("OFFBOARDED", plan.Parameters[0].Value);
        Assert.Equal(DateTime.UtcNow.Date.AddDays(-30).ToString("yyyy-MM-dd"), plan.Parameters[1].Value);
    }

    [Fact]
    public void Build_ConsumerFilterCannotOverrideServerEligibility()
    {
        var plan = new QueryBuilder().Build(
            Definition,
            new QueryRequestDto(null, ["status:eq:ACTIVE"], null, 1, 100, false));

        Assert.Contains("`status` = :p0", plan.Sql);
        Assert.Contains("`status` = :p2", plan.Sql);
        Assert.Equal("OFFBOARDED", plan.Parameters[0].Value);
        Assert.Equal("ACTIVE", plan.Parameters[2].Value);
        Assert.DoesNotContain("ACTIVE", plan.Sql);
    }

    [Fact]
    public void Count_UsesSameServerEligibilityGuardrails()
    {
        var plan = new QueryBuilder().BuildCount(Definition, new QueryRequestDto(null, [], null, 1, 100, true));

        Assert.Contains("`status` = :p0", plan.Sql);
        Assert.Contains("`termination_date` >= :p1", plan.Sql);
        Assert.Equal(2, plan.Parameters.Count);
    }
}

