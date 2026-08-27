using IdbInvest.Offboarding.Databricks.Facade.Application.Query;
using IdbInvest.Offboarding.Databricks.Facade.Core.DTO;
using IdbInvest.Offboarding.Databricks.Facade.Core.Exceptions;
using IdbInvest.Offboarding.Databricks.Facade.Core.Models;

namespace IdbInvest.Offboarding.Databricks.Facade.Tests;

public sealed class QueryBuilderExtendedTests
{
    private static readonly ResourceDefinition Employees = new()
    {
        Name = "employees",
        Source = "gold.offboarding.vw_employees",
        MaxPageSize = 500,
        DefaultFields = ["employeeId", "fullName", "status"],
        Fields = new Dictionary<string, FieldDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["employeeId"] = new() { Column = "employee_id", Type = "STRING", Selectable = true, Filterable = true, Sortable = true },
            ["fullName"] = new() { Column = "full_name", Type = "STRING", Selectable = true, Filterable = true, Sortable = true },
            ["status"] = new() { Column = "status", Type = "STRING", Selectable = true, Filterable = true, Sortable = true },
            ["updatedAt"] = new() { Column = "updated_at", Type = "TIMESTAMP", Selectable = true, Filterable = true, Sortable = true },
            ["privateNote"] = new() { Column = "private_note", Type = "STRING", Selectable = false, Filterable = false, Sortable = false }
        }
    };

    [Fact]
    public void Build_UsesDefaultFields_WhenFieldsAreNotProvided()
    {
        var sut = new QueryBuilder();

        var plan = sut.Build(Employees, new QueryRequestDto(null, [], null, 1, 100, false));

        Assert.Equal(["employeeId", "fullName", "status"], plan.PublicFields);
        Assert.Contains("`employee_id` AS `employeeId`", plan.Sql);
        Assert.Contains("`full_name` AS `fullName`", plan.Sql);
        Assert.Contains("`status` AS `status`", plan.Sql);
    }

    [Theory]
    [InlineData("status:eq:ACTIVE", "`status` = :p0")]
    [InlineData("status:ne:INACTIVE", "`status` <> :p0")]
    [InlineData("updatedAt:gt:2026-01-01", "`updated_at` > :p0")]
    [InlineData("updatedAt:gte:2026-01-01", "`updated_at` >= :p0")]
    [InlineData("updatedAt:lt:2026-12-31", "`updated_at` < :p0")]
    [InlineData("updatedAt:lte:2026-12-31", "`updated_at` <= :p0")]
    public void Build_MapsSupportedOperators_ToParameterizedSql(string filter, string expectedSql)
    {
        var sut = new QueryBuilder();

        var plan = sut.Build(Employees, new QueryRequestDto(null, [filter], null, 1, 100, false));

        Assert.Contains(expectedSql, plan.Sql);
        Assert.Single(plan.Parameters);
        Assert.DoesNotContain(plan.Parameters[0].Value, plan.Sql);
    }

    [Fact]
    public void Build_ContainsOperator_UsesLikeWithoutEmbeddingCallerValue()
    {
        var sut = new QueryBuilder();

        var plan = sut.Build(Employees, new QueryRequestDto(null, ["fullName:contains:Smith"], null, 1, 100, false));

        Assert.Contains("LIKE :p0", plan.Sql);
        Assert.Single(plan.Parameters);
        Assert.DoesNotContain("Smith", plan.Sql);
    }

    [Fact]
    public void Build_ParsesMultipleFilters_FromSemicolonSeparatedInput()
    {
        var sut = new QueryBuilder();

        var plan = sut.Build(Employees, new QueryRequestDto(null, ["status:eq:ACTIVE;fullName:contains:John"], null, 1, 100, false));

        Assert.Equal(2, plan.Parameters.Count);
        Assert.Contains("`status` = :p0", plan.Sql);
        Assert.Contains("`full_name` LIKE :p1", plan.Sql);
    }

    [Fact]
    public void Build_RejectsFilteringOnNonFilterableField()
    {
        var sut = new QueryBuilder();

        Assert.Throws<InvalidQueryException>(() =>
            sut.Build(Employees, new QueryRequestDto(null, ["privateNote:eq:x"], null, 1, 100, false)));
    }

    [Fact]
    public void Build_RejectsSortingOnUnknownField()
    {
        var sut = new QueryBuilder();

        Assert.Throws<InvalidQueryException>(() =>
            sut.Build(Employees, new QueryRequestDto(null, [], "password", 1, 100, false)));
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(-1, 100)]
    [InlineData(1, 0)]
    [InlineData(1, -10)]
    [InlineData(1, 501)]
    public void Build_RejectsInvalidPagination(int page, int pageSize)
    {
        var sut = new QueryBuilder();

        Assert.Throws<InvalidQueryException>(() =>
            sut.Build(Employees, new QueryRequestDto(null, [], null, page, pageSize, false)));
    }

    [Fact]
    public void BuildCount_UsesSameFilters_AndOmitsPaginationAndProjection()
    {
        var sut = new QueryBuilder();

        var plan = sut.BuildCount(Employees, new QueryRequestDto("employeeId", ["status:eq:ACTIVE"], "-updatedAt", 3, 25, true));

        Assert.Contains("COUNT(1)", plan.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("`status` = :p0", plan.Sql);
        Assert.DoesNotContain("LIMIT", plan.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OFFSET", plan.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Single(plan.Parameters);
    }
}
