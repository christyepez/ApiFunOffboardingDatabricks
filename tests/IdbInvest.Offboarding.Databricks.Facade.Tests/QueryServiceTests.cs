using IdbInvest.Offboarding.Databricks.Facade.Application.Services;
using IdbInvest.Offboarding.Databricks.Facade.Core.DTO;
using IdbInvest.Offboarding.Databricks.Facade.Core.Interfaces;
using IdbInvest.Offboarding.Databricks.Facade.Core.Models;

namespace IdbInvest.Offboarding.Databricks.Facade.Tests;

public sealed class QueryServiceTests
{
    [Fact]
    public async Task QueryAsync_ReturnsOnlyRequestedPageSize_AndSetsHasMore()
    {
        var definition = CreateDefinition();
        var registry = new FakeRegistry(definition);
        var builder = new FakeBuilder();
        var repository = new FakeRepository(
            rows:
            [
                Row("1"),
                Row("2"),
                Row("3")
            ],
            total: 3);
        var sut = new QueryService(registry, builder, repository);
        var request = new QueryRequestDto(null, [], null, 1, 2, false);

        var result = await sut.QueryAsync("employees", request, "corr-123", CancellationToken.None);

        Assert.Equal(2, result.Data.Count);
        Assert.True(result.Meta.HasMore);
        Assert.Equal(2, result.Meta.Returned);
        Assert.Equal("corr-123", result.Meta.CorrelationId);
        Assert.Null(result.Meta.Total);
        Assert.Equal(1, repository.QueryCalls);
        Assert.Equal(0, repository.CountCalls);
    }

    [Fact]
    public async Task QueryAsync_RequestsCount_WhenIncludeTotalIsTrue()
    {
        var definition = CreateDefinition();
        var repository = new FakeRepository([Row("1")], total: 42);
        var sut = new QueryService(new FakeRegistry(definition), new FakeBuilder(), repository);
        var request = new QueryRequestDto(null, [], null, 1, 100, true);

        var result = await sut.QueryAsync("employees", request, "corr-total", CancellationToken.None);

        Assert.Equal(42, result.Meta.Total);
        Assert.False(result.Meta.HasMore);
        Assert.Equal(1, repository.QueryCalls);
        Assert.Equal(1, repository.CountCalls);
    }

    [Fact]
    public void GetMetadata_MapsResourceDefinitionWithoutPhysicalSource()
    {
        var definition = CreateDefinition();
        var sut = new QueryService(new FakeRegistry(definition), new FakeBuilder(), new FakeRepository([], 0));

        var metadata = sut.GetMetadata("employees");

        Assert.Equal("employees", metadata.Resource);
        Assert.Equal(1000, metadata.MaxPageSize);
        Assert.Contains(metadata.Fields, x => x.Name == "employeeId" && x.Filterable && x.Sortable && x.Selectable);
    }

    private static ResourceDefinition CreateDefinition() => new()
    {
        Name = "employees",
        Source = "gold.offboarding.vw_employees",
        MaxPageSize = 1000,
        DefaultFields = ["employeeId"],
        Fields = new Dictionary<string, FieldDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["employeeId"] = new() { Column = "employee_id", Type = "STRING", Selectable = true, Filterable = true, Sortable = true }
        }
    };

    private static IReadOnlyDictionary<string, object?> Row(string id) =>
        new Dictionary<string, object?> { ["employeeId"] = id };

    private sealed class FakeRegistry(ResourceDefinition definition) : IResourceRegistry
    {
        public ResourceDefinition GetRequired(string resource) => definition;
        public IReadOnlyCollection<ResourceDefinition> GetAll() => [definition];
    }

    private sealed class FakeBuilder : IQueryBuilder
    {
        public QueryPlan Build(ResourceDefinition definition, QueryRequestDto request) =>
            new("SELECT 1", [], definition.DefaultFields, request.Page, request.PageSize);

        public CountQueryPlan BuildCount(ResourceDefinition definition, QueryRequestDto request) =>
            new("SELECT COUNT(1)", []);
    }

    private sealed class FakeRepository(IReadOnlyList<IReadOnlyDictionary<string, object?>> rows, long total) : IDatabricksRepository
    {
        public int QueryCalls { get; private set; }
        public int CountCalls { get; private set; }

        public Task<DatabricksQueryResult> QueryAsync(QueryPlan plan, CancellationToken cancellationToken)
        {
            QueryCalls++;
            return Task.FromResult(new DatabricksQueryResult(rows, "statement-test"));
        }

        public Task<long> CountAsync(CountQueryPlan plan, CancellationToken cancellationToken)
        {
            CountCalls++;
            return Task.FromResult(total);
        }

        public Task<bool> PingAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    }
}
