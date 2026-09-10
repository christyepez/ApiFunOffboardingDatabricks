using System.Net;
using System.Text.Json;
using IdbInvest.Offboarding.Databricks.Facade.Core.DTO;
using IdbInvest.Offboarding.Databricks.Facade.Core.Interfaces;
using IdbInvest.Offboarding.Databricks.Facade.Core.Models;
using IdbInvest.Offboarding.Databricks.Facade.Functions.Controllers;
using Microsoft.Extensions.Logging.Abstractions;

namespace IdbInvest.Offboarding.Databricks.Facade.Tests;

public sealed class FunctionControllerTests
{
    [Fact]
    public async Task CorporateHealth_ReturnsHealthy_WhenDatabricksResponds()
    {
        var request = Request("https://localhost/api/health", "corr-health");
        var sut = new HealthController(new FakeDatabricksRepository(true));

        var response = (TestHttpResponseData)await sut.CorporateHealthAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Healthy", response.ReadBody(), StringComparison.Ordinal);
        Assert.Contains("corr-health", response.ReadBody(), StringComparison.Ordinal);
        Assert.Equal("no-store", response.Headers.GetValues("Cache-Control").Single());
    }

    [Fact]
    public async Task CorporateHealth_ReturnsUnhealthy_WhenDatabricksFails()
    {
        var request = Request("https://localhost/api/health");
        var sut = new HealthController(new FakeDatabricksRepository(false));

        var response = (TestHttpResponseData)await sut.CorporateHealthAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("Unhealthy", response.ReadBody(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Liveness_DoesNotCallDatabricks_AndReturnsOk()
    {
        var repository = new FakeDatabricksRepository(false);
        var request = Request("https://localhost/api/offboarding/v1/health/live", "live-1");
        var sut = new HealthController(repository);

        var response = (TestHttpResponseData)await sut.LiveAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, repository.PingCalls);
        Assert.Contains("\"status\":\"ok\"", response.ReadBody(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("ready")]
    [InlineData("healthcheck")]
    [InlineData("health")]
    public async Task DependencyHealthAliases_Return503_WhenDatabricksUnavailable(string endpoint)
    {
        var request = Request($"https://localhost/api/offboarding/v1/{endpoint}");
        var sut = new HealthController(new FakeDatabricksRepository(false));

        var response = endpoint switch
        {
            "ready" => await sut.ReadyAsync(request, CancellationToken.None),
            "healthcheck" => await sut.HealthCheckAsync(request, CancellationToken.None),
            _ => await sut.GetAsync(request, CancellationToken.None)
        };

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("unavailable", ((TestHttpResponseData)response).ReadBody(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Metadata_ReturnsLogicalContract_AndCorrelationHeader()
    {
        var service = new FakeQueryService();
        var request = Request("https://localhost/api/offboarding/v1/resources/employees/metadata", "meta-1");
        var sut = new MetadataController(service);

        var response = (TestHttpResponseData)await sut.GetAsync(request, "employees", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("meta-1", response.Headers.GetValues("x-correlation-id").Single());
        var body = response.ReadBody();
        Assert.Contains("employee-offboarding-candidate", body, StringComparison.Ordinal);
        Assert.Equal("employees", service.LastMetadataResource);
    }

    [Fact]
    public async Task DynamicQuery_MapsQueryString_AndReturnsServiceResult()
    {
        var service = new FakeQueryService();
        var request = Request("https://localhost/api/offboarding/v1/employees?fields=employeeId,status&filter=country:eq:US&sort=-updatedAt&page=2&pageSize=25&includeTotal=true", "query-1");
        var sut = new DynamicQueryController(service, NullLogger<DynamicQueryController>.Instance);

        var response = (TestHttpResponseData)await sut.GetAsync(request, "employees", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("query-1", response.Headers.GetValues("x-correlation-id").Single());
        Assert.Equal("no-store", response.Headers.GetValues("Cache-Control").Single());
        Assert.Equal("employees", service.LastResource);
        Assert.NotNull(service.LastRequest);
        Assert.Equal(2, service.LastRequest!.Page);
        Assert.Equal(25, service.LastRequest.PageSize);
        Assert.True(service.LastRequest.IncludeTotal);
        Assert.Equal("employeeId,status", service.LastRequest.Fields);
        Assert.Contains("country:eq:US", service.LastRequest.Filters);
        Assert.Contains("employee-offboarding-candidate", response.ReadBody(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DynamicQuery_UsesDefaults_WhenOptionalQueryParametersAreMissing()
    {
        var service = new FakeQueryService();
        var request = Request("https://localhost/api/offboarding/v1/employees");
        var sut = new DynamicQueryController(service, NullLogger<DynamicQueryController>.Instance);

        var response = (TestHttpResponseData)await sut.GetAsync(request, "employees", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(service.LastRequest);
        Assert.Equal(1, service.LastRequest!.Page);
        Assert.Equal(100, service.LastRequest.PageSize);
        Assert.False(service.LastRequest.IncludeTotal);
    }

    [Fact]
    public async Task DynamicQuery_RejectsNonIntegerPage()
    {
        var service = new FakeQueryService();
        var request = Request("https://localhost/api/offboarding/v1/employees?page=abc");
        var sut = new DynamicQueryController(service, NullLogger<DynamicQueryController>.Instance);

        var ex = await Assert.ThrowsAsync<IdbInvest.Offboarding.Databricks.Facade.Core.Exceptions.InvalidQueryException>(() =>
            sut.GetAsync(request, "employees", CancellationToken.None));

        Assert.Contains("page must be an integer", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DynamicQuery_RejectsNonBooleanIncludeTotal()
    {
        var service = new FakeQueryService();
        var request = Request("https://localhost/api/offboarding/v1/employees?includeTotal=maybe");
        var sut = new DynamicQueryController(service, NullLogger<DynamicQueryController>.Instance);

        var ex = await Assert.ThrowsAsync<IdbInvest.Offboarding.Databricks.Facade.Core.Exceptions.InvalidQueryException>(() =>
            sut.GetAsync(request, "employees", CancellationToken.None));

        Assert.Contains("includeTotal must be true or false", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CorporateHealth_ReplacesInvalidCorrelationId()
    {
        var request = Request("https://localhost/api/health", "invalid correlation id with spaces");
        var sut = new HealthController(new FakeDatabricksRepository(true));

        var response = (TestHttpResponseData)await sut.CorporateHealthAsync(request, CancellationToken.None);

        var correlation = response.Headers.GetValues("x-correlation-id").Single();
        Assert.NotEqual("invalid correlation id with spaces", correlation);
        Assert.True(Guid.TryParse(correlation, out _));
    }
    private static TestHttpRequestData Request(string url, string? correlationId = null)
    {
        var request = new TestHttpRequestData(url);
        if (correlationId is not null) request.Headers.Add("x-correlation-id", correlationId);
        return request;
    }

    private sealed class FakeDatabricksRepository(bool pingResult) : IDatabricksRepository
    {
        public int PingCalls { get; private set; }
        public Task<DatabricksQueryResult> QueryAsync(QueryPlan plan, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<long> CountAsync(CountQueryPlan plan, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> PingAsync(CancellationToken cancellationToken)
        {
            PingCalls++;
            return Task.FromResult(pingResult);
        }
    }

    private sealed class FakeQueryService : IQueryService
    {
        public string? LastResource { get; private set; }
        public QueryRequestDto? LastRequest { get; private set; }
        public string? LastMetadataResource { get; private set; }

        public Task<QueryResponseDto> QueryAsync(string resource, QueryRequestDto request, string correlationId, CancellationToken cancellationToken)
        {
            LastResource = resource;
            LastRequest = request;
            return Task.FromResult(new QueryResponseDto(
                [new Dictionary<string, object?> { ["employeeId"] = "E1", ["status"] = "OFFBOARDED" }],
                new QueryMetadataDto(resource, "employee-offboarding-candidate", "1.0", request.Page, request.PageSize, 1, 1, false, correlationId)));
        }

        public ResourceMetadataDto GetMetadata(string resource)
        {
            LastMetadataResource = resource;
            return new ResourceMetadataDto(resource, "employee-offboarding-candidate", "1.0",
                [new ResourceFieldDto("employeeId", "STRING", true, true, true)], 1000);
        }
    }
}
