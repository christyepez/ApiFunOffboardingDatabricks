using System.Net;
using System.Text;
using System.Text.Json;
using IdbInvest.Offboarding.Databricks.Facade.Core.Exceptions;
using IdbInvest.Offboarding.Databricks.Facade.Core.Models;
using IdbInvest.Offboarding.Databricks.Facade.Infrastructure.DataMotion;
using IdbInvest.Offboarding.Databricks.Facade.Infrastructure.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace IdbInvest.Offboarding.Databricks.Facade.Tests;

public sealed class DataMotionStatementRepositoryTests
{
    [Fact]
    public async Task QueryAsync_SendsWarehouseQueryAndRequiredHeaders()
    {
        var handler = new CaptureHandler(Json(HttpStatusCode.OK, """
        {
          "statement_id":"stmt-1",
          "status":{"state":"SUCCEEDED"},
          "manifest":{"schema":{"columns":[{"name":"email"}]},"truncated":false},
          "result":{"data_array":[["john.o'connor@iadb.org"]]}
        }
        """));

        var sut = Create(handler);

        var result = await sut.QueryAsync(
            new QueryPlan(
                "SELECT email FROM trusted_integration_idbi_offboarding.offboarded_employees WHERE email = :p0",
                [new SqlParameterValue("p0", "john.o'connor@iadb.org", "STRING")],
                ["email"],
                1,
                10),
            CancellationToken.None);

        Assert.Single(result.Rows);
        Assert.Single(handler.Captured);
        var request = handler.Captured[0];

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.EndsWith("/2.0/sql/statements", request.Uri.AbsoluteUri, StringComparison.Ordinal);
        Assert.Equal("Bearer token", request.Authorization);
        Assert.Equal("subscription-key", request.SubscriptionKey);

        using var document = JsonDocument.Parse(request.Body);
        Assert.Equal("warehouse_idbi_offboarding", document.RootElement.GetProperty("warehouse").GetString());

        var query = document.RootElement.GetProperty("query").GetString();
        Assert.NotNull(query);
        Assert.Contains("john.o''connor@iadb.org", query, StringComparison.Ordinal);
        Assert.DoesNotContain(":p0", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryAsync_RendersDateParameterUsingTypedLiteral()
    {
        var handler = new CaptureHandler(Json(HttpStatusCode.OK, """
        {
          "statement_id":"stmt-date",
          "status":{"state":"SUCCEEDED"},
          "manifest":{"schema":{"columns":[{"name":"terminationDate"}]},"truncated":false},
          "result":{"data_array":[["2026-10-01"]]}
        }
        """));

        var sut = Create(handler);

        await sut.QueryAsync(
            new QueryPlan(
                "SELECT termination_date AS terminationDate FROM trusted_integration_idbi_offboarding.offboarded_employees WHERE termination_date >= :p0",
                [new SqlParameterValue("p0", "2026-10-01", "DATE")],
                ["terminationDate"],
                1,
                10),
            CancellationToken.None);

        using var document = JsonDocument.Parse(handler.Captured[0].Body);
        var query = document.RootElement.GetProperty("query").GetString();

        Assert.Contains("DATE '2026-10-01'", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryAsync_ReadsAdditionalChunks()
    {
        var handler = new CaptureHandler(
            Json(HttpStatusCode.OK, """
            {
              "statement_id":"stmt-chunk",
              "status":{"state":"SUCCEEDED"},
              "manifest":{"schema":{"columns":[{"name":"email"}]},"truncated":false},
              "result":{"data_array":[["a@iadb.org"]],"next_chunk_index":1}
            }
            """),
            Json(HttpStatusCode.OK, """
            {"chunk_index":1,"data_array":[["b@iadb.org"]]}
            """));

        var sut = Create(handler);

        var result = await sut.QueryAsync(
            new QueryPlan(
                "SELECT email FROM trusted_integration_idbi_offboarding.offboarded_employees",
                [],
                ["email"],
                1,
                10),
            CancellationToken.None);

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(2, handler.Captured.Count);
        Assert.Contains("/result/chunks/1", handler.Captured[1].Uri.AbsolutePath, StringComparison.Ordinal);
        Assert.Equal("subscription-key", handler.Captured[1].SubscriptionKey);
    }

    [Fact]
    public async Task PingAsync_ReturnsFalseWhenDataMotionRejectsRequest()
    {
        var handler = new CaptureHandler(Json(HttpStatusCode.Forbidden, "{}"));
        var sut = Create(handler);

        var healthy = await sut.PingAsync(CancellationToken.None);

        Assert.False(healthy);
    }

    [Fact]
    public async Task QueryAsync_RejectsMissingSubscriptionKeyBeforeHttpCall()
    {
        var handler = new CaptureHandler(Json(HttpStatusCode.OK, "{}"));
        var sut = Create(handler, subscriptionKey: "");

        var ex = await Assert.ThrowsAsync<DependencyUnavailableException>(() =>
            sut.QueryAsync(
                new QueryPlan("SELECT 1", [], ["ok"], 1, 10),
                CancellationToken.None));

        Assert.Contains("SubscriptionKey", ex.Message, StringComparison.Ordinal);
        Assert.Empty(handler.Captured);
    }

    [Fact]
    public async Task QueryAsync_RejectsInvalidDateBeforeHttpCall()
    {
        var handler = new CaptureHandler(Json(HttpStatusCode.OK, "{}"));
        var sut = Create(handler);

        var ex = await Assert.ThrowsAsync<InvalidQueryException>(() =>
            sut.QueryAsync(
                new QueryPlan(
                    "SELECT 1 WHERE DATE '2026-01-01' <= :p0",
                    [new SqlParameterValue("p0", "not-a-date", "DATE")],
                    ["ok"],
                    1,
                    10),
                CancellationToken.None));

        Assert.Contains("yyyy-MM-dd", ex.Message, StringComparison.Ordinal);
        Assert.Empty(handler.Captured);
    }

    private static DataMotionStatementRepository Create(
        HttpMessageHandler handler,
        string subscriptionKey = "subscription-key")
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://idbinvestapi-d.iadb.org/datamotion-analytical-query-data-2/")
        };

        var options = Options.Create(new DataMotionOptions
        {
            BaseUrl = "https://idbinvestapi-d.iadb.org/datamotion-analytical-query-data-2/",
            Warehouse = "warehouse_idbi_offboarding",
            SubscriptionKey = subscriptionKey,
            PollIntervalMilliseconds = 250,
            MaxPollSeconds = 5,
            HealthTimeoutSeconds = 2,
            MaxResultChunks = 10
        });

        return new DataMotionStatementRepository(
            client,
            new FakeTokenProvider(),
            options,
            NullLogger<DataMotionStatementRepository>.Instance);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class FakeTokenProvider : IDataMotionAccessTokenProvider
    {
        public Task<string> GetTokenAsync(CancellationToken cancellationToken)
            => Task.FromResult("token");
    }

    private sealed class CaptureHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public List<CapturedRequest> Captured { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            Captured.Add(new CapturedRequest(
                request.Method,
                request.RequestUri!,
                request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues("Ocp-Apim-Subscription-Key", out var values)
                    ? values.Single()
                    : null,
                body));

            if (_responses.Count == 0)
                throw new InvalidOperationException("Unexpected HTTP request.");

            return _responses.Dequeue();
        }
    }

    private sealed record CapturedRequest(
        HttpMethod Method,
        Uri Uri,
        string? Authorization,
        string? SubscriptionKey,
        string Body);
}
