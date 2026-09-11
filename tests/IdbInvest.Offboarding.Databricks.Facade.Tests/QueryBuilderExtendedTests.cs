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
    [InlineData("status:eq:OFFBOARDED", "`status` = :p0")]
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

        var plan = sut.Build(Employees, new QueryRequestDto(null, ["status:eq:OFFBOARDED;fullName:contains:John"], null, 1, 100, false));

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

        var plan = sut.BuildCount(Employees, new QueryRequestDto("employeeId", ["status:eq:OFFBOARDED"], "-updatedAt", 3, 25, true));

        Assert.Contains("COUNT(1)", plan.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("`status` = :p0", plan.Sql);
        Assert.DoesNotContain("LIMIT", plan.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OFFSET", plan.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Single(plan.Parameters);
    }
    [Fact]
    public void Build_RejectsEmptyExplicitFieldList()
    {
        var sut = new QueryBuilder();

        Assert.Throws<InvalidQueryException>(() =>
            sut.Build(Employees, new QueryRequestDto(",,", [], null, 1, 100, false)));
    }

    [Fact]
    public void Build_RejectsSelectingNonSelectableField()
    {
        var sut = new QueryBuilder();

        Assert.Throws<InvalidQueryException>(() =>
            sut.Build(Employees, new QueryRequestDto("privateNote", [], null, 1, 100, false)));
    }

    [Theory]
    [InlineData("updatedAt", "`updated_at` ASC")]
    [InlineData("-updatedAt", "`updated_at` DESC")]
    public void Build_MapsSortDirection(string sort, string expected)
    {
        var sut = new QueryBuilder();

        var plan = sut.Build(Employees, new QueryRequestDto(null, [], sort, 1, 100, false));

        Assert.Contains(expected, plan.Sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_RejectsUnsafeConfiguredSourceWithSinglePart()
    {
        var sut = new QueryBuilder();
        var definition = Definition("unsafe");

        Assert.Throws<InvalidOperationException>(() =>
            sut.Build(definition, new QueryRequestDto(null, [], null, 1, 100, false)));
    }

    [Fact]
    public void Build_RejectsUnsafeConfiguredSourceIdentifier()
    {
        var sut = new QueryBuilder();
        var definition = Definition("gold.offboarding.bad-name");

        Assert.Throws<InvalidOperationException>(() =>
            sut.Build(definition, new QueryRequestDto(null, [], null, 1, 100, false)));
    }

    [Fact]
    public void Build_AppliesDefaultSort_WhenCallerDoesNotProvideSort()
    {
        var sut = new QueryBuilder();
        var definition = Definition(Employees.Source, defaultSort: "-updatedAt");

        var plan = sut.Build(definition, new QueryRequestDto(null, [], null, 1, 100, false));

        Assert.Contains("ORDER BY `updated_at` DESC", plan.Sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_DoesNotDuplicateDefaultSort_WhenCallerAlreadySortsSameField()
    {
        var sut = new QueryBuilder();
        var definition = Definition(Employees.Source, defaultSort: "-updatedAt");

        var plan = sut.Build(definition, new QueryRequestDto(null, [], "updatedAt", 1, 100, false));

        Assert.Contains("ORDER BY `updated_at` ASC", plan.Sql, StringComparison.Ordinal);
        Assert.Equal(1, plan.Sql.Split("updated_at", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void Build_AppliesRequiredAndLookbackFilters()
    {
        var sut = new QueryBuilder();
        var definition = Definition(
            Employees.Source,
            requiredFilters: [new RequiredFilterDefinition { Field = "status", Value = "OFFBOARDED" }],
            lookback: new LookbackDefinition { Field = "updatedAt", Days = 30 });

        var plan = sut.Build(definition, new QueryRequestDto(null, [], null, 1, 100, false));

        Assert.Contains("`status` = :p0", plan.Sql, StringComparison.Ordinal);
        Assert.Contains("`updated_at` >= :p1", plan.Sql, StringComparison.Ordinal);
        Assert.Equal(2, plan.Parameters.Count);
    }
    private static ResourceDefinition Definition(
        string source,
        string? defaultSort = null,
        IReadOnlyList<RequiredFilterDefinition>? requiredFilters = null,
        LookbackDefinition? lookback = null) => new()
    {
        Name = Employees.Name,
        Source = source,
        MaxPageSize = Employees.MaxPageSize,
        DefaultFields = Employees.DefaultFields,
        DefaultSort = defaultSort,
        RequiredFilters = requiredFilters ?? [],
        Lookback = lookback,
        Fields = Employees.Fields
    };
}

