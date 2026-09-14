using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using IdbInvest.Offboarding.Databricks.Facade.Core.Exceptions;
using IdbInvest.Offboarding.Databricks.Facade.Core.Interfaces;
using IdbInvest.Offboarding.Databricks.Facade.Core.Models;
using IdbInvest.Offboarding.Databricks.Facade.Infrastructure.Databricks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IdbInvest.Offboarding.Databricks.Facade.Infrastructure.Repositories;

public sealed class DatabricksStatementRepository(
    HttpClient httpClient,
    IDatabricksTokenProvider tokenProvider,
    IOptions<DatabricksOptions> options,
    ILogger<DatabricksStatementRepository> logger) : IDatabricksRepository
{
    private readonly DatabricksOptions _options = options.Value;

    public async Task<DatabricksQueryResult> QueryAsync(QueryPlan plan, CancellationToken cancellationToken)
    {
        var response = await ExecuteAsync(plan.Sql, plan.Parameters, cancellationToken);
        if (response.Manifest?.Truncated == true)
            throw new DependencyUnavailableException("Databricks returned a truncated result set.");

        var columns = response.Manifest?.Schema?.Columns.Select(x => x.Name).ToArray() ?? [];
        var data = await ReadAllChunksAsync(response, cancellationToken);
        var rows = data.Select(row => (IReadOnlyDictionary<string, object?>)columns
            .Select((name, i) => new KeyValuePair<string, object?>(name, i < row.Count ? Normalize(row[i]) : null))
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase)).ToArray();
        return new DatabricksQueryResult(rows, response.StatementId);
    }

    public async Task<long> CountAsync(CountQueryPlan plan, CancellationToken cancellationToken)
    {
        var response = await ExecuteAsync(plan.Sql, plan.Parameters, cancellationToken);
        var first = response.Result?.DataArray.FirstOrDefault()?.FirstOrDefault();
        if (first is JsonElement je) first = je.ValueKind == JsonValueKind.String ? je.GetString() : je.ToString();
        return first is not null && long.TryParse(first.ToString(), out var total)
            ? total : throw new DependencyUnavailableException("Databricks count query returned an unexpected result.");
    }

    public async Task<bool> PingAsync(CancellationToken cancellationToken)
    {
        try
        {
            await ExecuteAsync("SELECT 1 AS ok", [], cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Databricks health check failed.");
            return false;
        }
    }

    private async Task<StatementResponse> ExecuteAsync(string sql, IReadOnlyList<SqlParameterValue> parameters, CancellationToken cancellationToken)
    {
        EnsureConfiguration();
        var payload = new StatementExecuteRequest(_options.WarehouseId, sql,
            parameters.Select(x => new StatementParameter(x.Name, x.Value, x.Type)).ToArray(),
            $"{Math.Clamp(_options.WaitTimeoutSeconds, 5, 50)}s");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/2.0/sql/statements") { Content = JsonContent.Create(payload) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokenProvider.GetTokenAsync(cancellationToken));
        using var httpResponse = await httpClient.SendAsync(request, cancellationToken);
        if (!httpResponse.IsSuccessStatusCode)
        {
            logger.LogError("Databricks statement request failed with HTTP {StatusCode}.", (int)httpResponse.StatusCode);
            throw new DependencyUnavailableException($"Databricks query failed with HTTP {(int)httpResponse.StatusCode}.");
        }

        var response = await httpResponse.Content.ReadFromJsonAsync<StatementResponse>(cancellationToken: cancellationToken)
                       ?? throw new DependencyUnavailableException("Databricks returned an empty response.");
        return await WaitForCompletionAsync(response, cancellationToken);
    }

    private async Task<StatementResponse> WaitForCompletionAsync(StatementResponse response, CancellationToken cancellationToken)
    {
        var state = response.Status?.State?.ToUpperInvariant();
        if (state == "SUCCEEDED") return response;
        if (state is "FAILED" or "CANCELED" or "CLOSED")
            throw CreateStatementFailure(response, state);
        if (string.IsNullOrWhiteSpace(response.StatementId))
            throw new DependencyUnavailableException("Databricks did not return a statement id.");

        var statementId = response.StatementId;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.MaxPollSeconds));

        try
        {
            while (true)
            {
                await Task.Delay(_options.PollIntervalMilliseconds, timeout.Token);
                using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/2.0/sql/statements/{Uri.EscapeDataString(statementId)}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokenProvider.GetTokenAsync(timeout.Token));
                using var httpResponse = await httpClient.SendAsync(request, timeout.Token);
                if (!httpResponse.IsSuccessStatusCode)
                    throw new DependencyUnavailableException("Databricks statement polling failed.");

                response = await httpResponse.Content.ReadFromJsonAsync<StatementResponse>(cancellationToken: timeout.Token)
                           ?? throw new DependencyUnavailableException("Databricks polling returned an empty response.");
                state = response.Status?.State?.ToUpperInvariant();
                if (state == "SUCCEEDED") return response;
                if (state is "FAILED" or "CANCELED" or "CLOSED")
                    throw CreateStatementFailure(response, state);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new DependencyUnavailableException("Databricks statement timed out.");
        }
    }

    private async Task<IReadOnlyList<List<object?>>> ReadAllChunksAsync(StatementResponse response, CancellationToken cancellationToken)
    {
        if (response.Result is null) return [];

        var rows = new List<List<object?>>(response.Result.DataArray);
        var nextChunkIndex = response.Result.NextChunkIndex;
        var fetchedChunks = 1;
        var maxChunks = Math.Clamp(_options.MaxResultChunks, 1, 10_000);

        if (nextChunkIndex.HasValue && string.IsNullOrWhiteSpace(response.StatementId))
            throw new DependencyUnavailableException("Databricks returned chunked data without a statement id.");

        while (nextChunkIndex.HasValue)
        {
            if (fetchedChunks >= maxChunks)
                throw new DependencyUnavailableException($"Databricks result exceeded the configured chunk limit ({maxChunks}).");

            var chunk = await GetChunkAsync(response.StatementId!, nextChunkIndex.Value, cancellationToken);
            rows.AddRange(chunk.DataArray);
            nextChunkIndex = chunk.NextChunkIndex;
            fetchedChunks++;
        }

        return rows;
    }

    private async Task<StatementResult> GetChunkAsync(string statementId, int chunkIndex, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/2.0/sql/statements/{Uri.EscapeDataString(statementId)}/result/chunks/{chunkIndex}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokenProvider.GetTokenAsync(cancellationToken));

        using var httpResponse = await httpClient.SendAsync(request, cancellationToken);
        if (!httpResponse.IsSuccessStatusCode)
        {
            logger.LogError("Databricks result chunk {ChunkIndex} failed with HTTP {StatusCode}.", chunkIndex, (int)httpResponse.StatusCode);
            throw new DependencyUnavailableException($"Databricks result chunk {chunkIndex} could not be retrieved.");
        }

        return await httpResponse.Content.ReadFromJsonAsync<StatementResult>(cancellationToken: cancellationToken)
               ?? throw new DependencyUnavailableException($"Databricks result chunk {chunkIndex} returned an empty response.");
    }

    private void EnsureConfiguration()
    {
        if (!Uri.TryCreate(_options.Host, UriKind.Absolute, out _))
            throw new DependencyUnavailableException("Databricks Host is not configured with a valid absolute URI.");
        if (string.IsNullOrWhiteSpace(_options.WarehouseId) || _options.WarehouseId.Contains('<', StringComparison.Ordinal))
            throw new DependencyUnavailableException("Databricks WarehouseId is not configured.");
        if (_options.WaitTimeoutSeconds is <= 0 or > 50)
            throw new DependencyUnavailableException("Databricks WaitTimeoutSeconds is outside the supported range.");
        if (_options.PollIntervalMilliseconds is < 250 or > 10000)
            throw new DependencyUnavailableException("Databricks PollIntervalMilliseconds is outside the supported range.");
        if (_options.MaxPollSeconds is <= 0 or > 300)
            throw new DependencyUnavailableException("Databricks MaxPollSeconds is outside the supported range.");
        if (_options.MaxResultChunks is <= 0 or > 10000)
            throw new DependencyUnavailableException("Databricks MaxResultChunks is outside the supported range.");
    }
    private static DependencyUnavailableException CreateStatementFailure(StatementResponse response, string? state)
    {
        var errorCode = response.Status?.Error?.ErrorCode;
        return new DependencyUnavailableException(string.IsNullOrWhiteSpace(errorCode)
            ? $"Databricks statement ended in state {state}."
            : $"Databricks statement ended in state {state} with error code {errorCode}.");
    }

    private static object? Normalize(object? value)
    {
        if (value is not JsonElement element) return value;
        return element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            JsonValueKind.Number when element.TryGetDecimal(out var d) => d,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => element.ToString()
        };
    }
}
