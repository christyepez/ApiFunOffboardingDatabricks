using System.Net;
using System.Text;
using IdbInvest.Offboarding.Databricks.Facade.Core.Exceptions;
using IdbInvest.Offboarding.Databricks.Facade.Core.Interfaces;
using IdbInvest.Offboarding.Databricks.Facade.Core.Models;
using IdbInvest.Offboarding.Databricks.Facade.Infrastructure.Databricks;
using IdbInvest.Offboarding.Databricks.Facade.Infrastructure.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace IdbInvest.Offboarding.Databricks.Facade.Tests;

public sealed class DatabricksStatementRepositoryTests
{
    [Fact]
    public async Task QueryAsync_ReadsAllInlineChunks()
    {
        var handler = new SequenceHandler(
            Json(HttpStatusCode.OK, """
            {
              "statement_id":"stmt-1",
              "status":{"state":"SUCCEEDED"},
              "manifest":{"schema":{"columns":[{"name":"employeeId"},{"name":"status"}]},"total_chunk_count":2,"truncated":false},
              "result":{"chunk_index":0,"data_array":[["E1","OFFBOARDED"]],"next_chunk_index":1}
            }
            """),
            Json(HttpStatusCode.OK, """
            {"chunk_index":1,"data_array":[["E2","OFFBOARDED"]]}
            """));
        var sut = Create(handler, maxChunks: 10);

        var result = await sut.QueryAsync(new QueryPlan("SELECT 1", [], ["employeeId", "status"], 1, 100), CancellationToken.None);

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal("E1", result.Rows[0]["employeeId"]);
        Assert.Equal("E2", result.Rows[1]["employeeId"]);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("/result/chunks/1", handler.Requests[1].RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task QueryAsync_RejectsChunkCountBeyondConfiguredLimit()
    {
        var handler = new SequenceHandler(Json(HttpStatusCode.OK, """
        {
          "statement_id":"stmt-1",
          "status":{"state":"SUCCEEDED"},
          "manifest":{"schema":{"columns":[{"name":"employeeId"}]},"total_chunk_count":2,"truncated":false},
          "result":{"chunk_index":0,"data_array":[["E1"]],"next_chunk_index":1}
        }
        """));
        var sut = Create(handler, maxChunks: 1);

        var ex = await Assert.ThrowsAsync<DependencyUnavailableException>(() =>
            sut.QueryAsync(new QueryPlan("SELECT 1", [], ["employeeId"], 1, 100), CancellationToken.None));

        Assert.Contains("chunk limit", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task QueryAsync_RejectsTruncatedManifest()
    {
        var handler = new SequenceHandler(Json(HttpStatusCode.OK, """
        {
          "statement_id":"stmt-1",
          "status":{"state":"SUCCEEDED"},
          "manifest":{"schema":{"columns":[{"name":"employeeId"}]},"truncated":true},
          "result":{"data_array":[["E1"]]}
        }
        """));
        var sut = Create(handler, maxChunks: 10);

        await Assert.ThrowsAsync<DependencyUnavailableException>(() =>
            sut.QueryAsync(new QueryPlan("SELECT 1", [], ["employeeId"], 1, 100), CancellationToken.None));
    }


    [Fact]
    public async Task QueryAsync_PollsUntilSucceeded()
    {
        var handler = new SequenceHandler(
            Json(HttpStatusCode.OK, """
            {"statement_id":"stmt-poll","status":{"state":"PENDING"}}
            """),
            Json(HttpStatusCode.OK, """
            {
              "statement_id":"stmt-poll",
              "status":{"state":"SUCCEEDED"},
              "manifest":{"schema":{"columns":[{"name":"employeeId"}]},"truncated":false},
              "result":{"data_array":[["E1"]]}
            }
            """));
        var sut = Create(handler, maxChunks: 10, pollIntervalMilliseconds: 250);

        var result = await sut.QueryAsync(new QueryPlan("SELECT 1", [], ["employeeId"], 1, 10), CancellationToken.None);

        Assert.Single(result.Rows);
        Assert.Equal("E1", result.Rows[0]["employeeId"]);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(HttpMethod.Get, handler.Requests[1].Method);
    }

    [Fact]
    public async Task QueryAsync_ThrowsWhenInitialStatementFails()
    {
        var handler = new SequenceHandler(Json(HttpStatusCode.OK, """
        {"statement_id":"stmt-fail","status":{"state":"FAILED","error":{"error_code":"BAD_STATEMENT"}}}
        """));
        var sut = Create(handler, maxChunks: 10);

        var ex = await Assert.ThrowsAsync<DependencyUnavailableException>(() =>
            sut.QueryAsync(new QueryPlan("SELECT 1", [], ["employeeId"], 1, 10), CancellationToken.None));

        Assert.Contains("BAD_STATEMENT", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryAsync_ThrowsWhenStatementIdMissingDuringPolling()
    {
        var handler = new SequenceHandler(Json(HttpStatusCode.OK, """
        {"status":{"state":"PENDING"}}
        """));
        var sut = Create(handler, maxChunks: 10);

        await Assert.ThrowsAsync<DependencyUnavailableException>(() =>
            sut.QueryAsync(new QueryPlan("SELECT 1", [], ["employeeId"], 1, 10), CancellationToken.None));
    }

    [Fact]
    public async Task QueryAsync_ThrowsOnHttpFailure()
    {
        var handler = new SequenceHandler(Json(HttpStatusCode.BadGateway, "{}"));
        var sut = Create(handler, maxChunks: 10);

        var ex = await Assert.ThrowsAsync<DependencyUnavailableException>(() =>
            sut.QueryAsync(new QueryPlan("SELECT 1", [], ["employeeId"], 1, 10), CancellationToken.None));

        Assert.Contains("502", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CountAsync_ReturnsParsedTotal()
    {
        var handler = new SequenceHandler(Json(HttpStatusCode.OK, """
        {
          "statement_id":"stmt-count",
          "status":{"state":"SUCCEEDED"},
          "result":{"data_array":[["42"]]}
        }
        """));
        var sut = Create(handler, maxChunks: 10);

        var total = await sut.CountAsync(new CountQueryPlan("SELECT COUNT(1)", []), CancellationToken.None);

        Assert.Equal(42, total);
    }

    [Fact]
    public async Task CountAsync_RejectsUnexpectedResult()
    {
        var handler = new SequenceHandler(Json(HttpStatusCode.OK, """
        {"statement_id":"stmt-count","status":{"state":"SUCCEEDED"},"result":{"data_array":[]}}
        """));
        var sut = Create(handler, maxChunks: 10);

        await Assert.ThrowsAsync<DependencyUnavailableException>(() =>
            sut.CountAsync(new CountQueryPlan("SELECT COUNT(1)", []), CancellationToken.None));
    }

    [Fact]
    public async Task PingAsync_ReturnsTrueOnSuccess()
    {
        var handler = new SequenceHandler(Json(HttpStatusCode.OK, """
        {"statement_id":"stmt-ping","status":{"state":"SUCCEEDED"},"result":{"data_array":[[1]]}}
        """));
        var sut = Create(handler, maxChunks: 10);

        Assert.True(await sut.PingAsync(CancellationToken.None));
    }

    [Fact]
    public async Task PingAsync_ReturnsFalseOnFailure()
    {
        var handler = new SequenceHandler(Json(HttpStatusCode.ServiceUnavailable, "{}"));
        var sut = Create(handler, maxChunks: 10);

        Assert.False(await sut.PingAsync(CancellationToken.None));
    }

    [Fact]
    public async Task QueryAsync_RejectsChunkedDataWithoutStatementId()
    {
        var handler = new SequenceHandler(Json(HttpStatusCode.OK, """
        {
          "status":{"state":"SUCCEEDED"},
          "manifest":{"schema":{"columns":[{"name":"employeeId"}]},"truncated":false},
          "result":{"data_array":[["E1"]],"next_chunk_index":1}
        }
        """));
        var sut = Create(handler, maxChunks: 10);

        await Assert.ThrowsAsync<DependencyUnavailableException>(() =>
            sut.QueryAsync(new QueryPlan("SELECT 1", [], ["employeeId"], 1, 10), CancellationToken.None));
    }
    [Theory]
    [InlineData("FAILED")]
    [InlineData("CANCELED")]
    [InlineData("CLOSED")]
    public async Task QueryAsync_ThrowsWhenPollingEndsInTerminalFailureState(string state)
    {
        var handler = new SequenceHandler(
            Json(HttpStatusCode.OK, """
            {"statement_id":"stmt-terminal","status":{"state":"PENDING"}}
            """),
            Json(HttpStatusCode.OK, $"{{\"statement_id\":\"stmt-terminal\",\"status\":{{\"state\":\"{state}\"}}}}"));
        var sut = Create(handler, maxChunks: 10, pollIntervalMilliseconds: 250);

        var ex = await Assert.ThrowsAsync<DependencyUnavailableException>(() =>
            sut.QueryAsync(new QueryPlan("SELECT 1", [], ["employeeId"], 1, 10), CancellationToken.None));

        Assert.Contains(state, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("", "warehouse")]
    [InlineData("not-a-uri", "warehouse")]
    [InlineData("https://example.azuredatabricks.net", "")]
    [InlineData("https://example.azuredatabricks.net", "<warehouse-id>")]
    public async Task QueryAsync_RejectsInvalidHostOrWarehouse(string host, string warehouseId)
    {
        var sut = CreateWithOptions(new SequenceHandler(), host, warehouseId, 5, 250, 5, 10);

        await Assert.ThrowsAsync<DependencyUnavailableException>(() =>
            sut.QueryAsync(new QueryPlan("SELECT 1", [], ["employeeId"], 1, 10), CancellationToken.None));
    }

    [Theory]
    [InlineData(0, 250, 5, 10)]
    [InlineData(51, 250, 5, 10)]
    [InlineData(5, 249, 5, 10)]
    [InlineData(5, 10001, 5, 10)]
    [InlineData(5, 250, 0, 10)]
    [InlineData(5, 250, 301, 10)]
    [InlineData(5, 250, 5, 0)]
    [InlineData(5, 250, 5, 10001)]
    public async Task QueryAsync_RejectsOutOfRangeRuntimeOptions(int waitSeconds, int pollMs, int maxPollSeconds, int maxChunks)
    {
        var sut = CreateWithOptions(new SequenceHandler(), "https://example.azuredatabricks.net", "warehouse", waitSeconds, pollMs, maxPollSeconds, maxChunks);

        await Assert.ThrowsAsync<DependencyUnavailableException>(() =>
            sut.QueryAsync(new QueryPlan("SELECT 1", [], ["employeeId"], 1, 10), CancellationToken.None));
    }

    [Fact]
    public async Task QueryAsync_RejectsEmptyStatementResponse()
    {
        var handler = new SequenceHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("null", Encoding.UTF8, "application/json")
        });
        var sut = Create(handler, 10);

        await Assert.ThrowsAsync<DependencyUnavailableException>(() =>
            sut.QueryAsync(new QueryPlan("SELECT 1", [], ["employeeId"], 1, 10), CancellationToken.None));
    }

    [Fact]
    public async Task QueryAsync_RejectsPollingHttpFailure()
    {
        var handler = new SequenceHandler(
            Json(HttpStatusCode.OK, "{\"statement_id\":\"stmt-poll\",\"status\":{\"state\":\"PENDING\"}}"),
            Json(HttpStatusCode.BadGateway, "{}"));
        var sut = Create(handler, 10, 250);

        var ex = await Assert.ThrowsAsync<DependencyUnavailableException>(() =>
            sut.QueryAsync(new QueryPlan("SELECT 1", [], ["employeeId"], 1, 10), CancellationToken.None));

        Assert.Contains("polling failed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryAsync_RejectsEmptyPollingResponse()
    {
        var handler = new SequenceHandler(
            Json(HttpStatusCode.OK, "{\"statement_id\":\"stmt-poll\",\"status\":{\"state\":\"PENDING\"}}"),
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("null", Encoding.UTF8, "application/json") });
        var sut = Create(handler, 10, 250);

        var ex = await Assert.ThrowsAsync<DependencyUnavailableException>(() =>
            sut.QueryAsync(new QueryPlan("SELECT 1", [], ["employeeId"], 1, 10), CancellationToken.None));

        Assert.Contains("polling returned an empty response", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryAsync_RejectsChunkHttpFailure()
    {
        var handler = new SequenceHandler(
            Json(HttpStatusCode.OK, "{\"statement_id\":\"stmt-chunk\",\"status\":{\"state\":\"SUCCEEDED\"},\"manifest\":{\"schema\":{\"columns\":[{\"name\":\"employeeId\"}]}},\"result\":{\"data_array\":[[\"E1\"]],\"next_chunk_index\":1}}"),
            Json(HttpStatusCode.InternalServerError, "{}"));
        var sut = Create(handler, 10);

        var ex = await Assert.ThrowsAsync<DependencyUnavailableException>(() =>
            sut.QueryAsync(new QueryPlan("SELECT 1", [], ["employeeId"], 1, 10), CancellationToken.None));

        Assert.Contains("chunk 1", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryAsync_RejectsEmptyChunkResponse()
    {
        var handler = new SequenceHandler(
            Json(HttpStatusCode.OK, "{\"statement_id\":\"stmt-chunk\",\"status\":{\"state\":\"SUCCEEDED\"},\"manifest\":{\"schema\":{\"columns\":[{\"name\":\"employeeId\"}]}},\"result\":{\"data_array\":[[\"E1\"]],\"next_chunk_index\":1}}"),
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("null", Encoding.UTF8, "application/json") });
        var sut = Create(handler, 10);

        var ex = await Assert.ThrowsAsync<DependencyUnavailableException>(() =>
            sut.QueryAsync(new QueryPlan("SELECT 1", [], ["employeeId"], 1, 10), CancellationToken.None));

        Assert.Contains("empty response", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryAsync_NormalizesJsonPrimitiveTypesAndMissingCells()
    {
        var handler = new SequenceHandler(Json(HttpStatusCode.OK, """
        {
          "statement_id":"stmt-types",
          "status":{"state":"SUCCEEDED"},
          "manifest":{"schema":{"columns":[{"name":"s"},{"name":"i"},{"name":"d"},{"name":"t"},{"name":"f"},{"name":"n"},{"name":"missing"}]}},
          "result":{"data_array":[["text",42,12.5,true,false,null]]}
        }
        """));
        var sut = Create(handler, 10);

        var result = await sut.QueryAsync(new QueryPlan("SELECT 1", [], ["s"], 1, 10), CancellationToken.None);

        var row = Assert.Single(result.Rows);
        Assert.Equal("text", row["s"]);
        Assert.Equal(42L, row["i"]);
        Assert.Equal(12.5m, row["d"]);
        Assert.Equal(true, row["t"]);
        Assert.Equal(false, row["f"]);
        Assert.Null(row["n"]);
        Assert.Null(row["missing"]);
    }

    [Fact]
    public async Task CountAsync_ParsesNumericJsonElement()
    {
        var handler = new SequenceHandler(Json(HttpStatusCode.OK, """
        {
          "statement_id":"stmt-count-number",
          "status":{"state":"SUCCEEDED"},
          "result":{"data_array":[[42]]}
        }
        """));
        var sut = Create(handler, 10);

        var total = await sut.CountAsync(new CountQueryPlan("SELECT COUNT(1)", []), CancellationToken.None);

        Assert.Equal(42L, total);
    }
    private static DatabricksStatementRepository Create(HttpMessageHandler handler, int maxChunks, int pollIntervalMilliseconds = 250)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.azuredatabricks.net") };
        var options = Options.Create(new DatabricksOptions
        {
            Host = "https://example.azuredatabricks.net",
            WarehouseId = "warehouse",
            WaitTimeoutSeconds = 5,
            PollIntervalMilliseconds = pollIntervalMilliseconds,
            MaxPollSeconds = 5,
            MaxResultChunks = maxChunks
        });
        return new DatabricksStatementRepository(client, new FakeTokenProvider(), options, NullLogger<DatabricksStatementRepository>.Instance);
    }


    private static DatabricksStatementRepository CreateWithOptions(
        HttpMessageHandler handler,
        string host,
        string warehouseId,
        int waitTimeoutSeconds,
        int pollIntervalMilliseconds,
        int maxPollSeconds,
        int maxResultChunks)
    {
        var options = Options.Create(new DatabricksOptions
        {
            Host = host,
            WarehouseId = warehouseId,
            WaitTimeoutSeconds = waitTimeoutSeconds,
            PollIntervalMilliseconds = pollIntervalMilliseconds,
            MaxPollSeconds = maxPollSeconds,
            MaxResultChunks = maxResultChunks
        });
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.azuredatabricks.net") };
        return new DatabricksStatementRepository(client, new FakeTokenProvider(), options, NullLogger<DatabricksStatementRepository>.Instance);
    }
    private static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class FakeTokenProvider : IDatabricksTokenProvider
    {
        public Task<string> GetTokenAsync(CancellationToken cancellationToken) => Task.FromResult("token");
    }

    private sealed class SequenceHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(CloneRequest(request));
            if (_responses.Count == 0) throw new InvalidOperationException("Unexpected HTTP request.");
            return Task.FromResult(_responses.Dequeue());
        }

        private static HttpRequestMessage CloneRequest(HttpRequestMessage request) => new(request.Method, request.RequestUri);
    }
}
