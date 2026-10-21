using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using IdbInvest.Offboarding.Databricks.Facade.Core.Exceptions;
using IdbInvest.Offboarding.Databricks.Facade.Core.Interfaces;
using IdbInvest.Offboarding.Databricks.Facade.Core.Models;
using IdbInvest.Offboarding.Databricks.Facade.Infrastructure.DataMotion;
using IdbInvest.Offboarding.Databricks.Facade.Infrastructure.Databricks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IdbInvest.Offboarding.Databricks.Facade.Infrastructure.Repositories;

public sealed class DataMotionStatementRepository(
    HttpClient httpClient,
    IDataMotionAccessTokenProvider tokenProvider,
    IOptions<DataMotionOptions> options,
    ILogger<DataMotionStatementRepository> logger) : IDatabricksRepository
{
    private const string SubscriptionHeader = "Ocp-Apim-Subscription-Key";
    private readonly DataMotionOptions _options = options.Value;

    public async Task<DatabricksQueryResult> QueryAsync(QueryPlan plan, CancellationToken cancellationToken)
    {
        var query = SqlParameterRenderer.Render(plan.Sql, plan.Parameters);
        var response = await ExecuteAsync(query, cancellationToken);

        if (response.Manifest?.Truncated == true)
            throw new DependencyUnavailableException("DataMotion returned a truncated result set.");

        var columns = response.Manifest?.Schema?.Columns.Select(x => x.Name).ToArray() ?? [];
        var data = await ReadAllChunksAsync(response, cancellationToken);

        var rows = data.Select(row => (IReadOnlyDictionary<string, object?>)columns
            .Select((name, index) => new KeyValuePair<string, object?>(
                name,
                index < row.Count ? Normalize(row[index]) : null))
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        return new DatabricksQueryResult(rows, response.StatementId);
    }

    public async Task<long> CountAsync(CountQueryPlan plan, CancellationToken cancellationToken)
    {
        var query = SqlParameterRenderer.Render(plan.Sql, plan.Parameters);
        var response = await ExecuteAsync(query, cancellationToken);
        var first = response.Result?.DataArray.FirstOrDefault()?.FirstOrDefault();

        if (first is JsonElement element)
            first = element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString();

        return first is not null && long.TryParse(first.ToString(), out var total)
            ? total
            : throw new DependencyUnavailableException("DataMotion count query returned an unexpected result.");
    }

    public async Task<bool> PingAsync(CancellationToken cancellationToken)
    {
        using var healthTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var timeoutSeconds = Math.Clamp(_options.HealthTimeoutSeconds, 2, 15);
        healthTimeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            await ExecuteAsync("SELECT 1 AS ok", healthTimeout.Token);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            logger.LogWarning(
                ex,
                "DataMotion health check timed out after {TimeoutSeconds} seconds.",
                timeoutSeconds);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "DataMotion health check failed.");
            return false;
        }
    }

    private async Task<StatementResponse> ExecuteAsync(string query, CancellationToken cancellationToken)
    {
        EnsureConfiguration();

        var payload = new DataMotionQueryRequest(_options.Warehouse, query);
        using var request = new HttpRequestMessage(HttpMethod.Post, "2.0/sql/statements")
        {
            Content = JsonContent.Create(payload)
        };

        await ApplyHeadersAsync(request, cancellationToken);

        using var httpResponse = await httpClient.SendAsync(request, cancellationToken);
        if (!httpResponse.IsSuccessStatusCode)
        {
            logger.LogError(
                "DataMotion statement request failed with HTTP {StatusCode}.",
                (int)httpResponse.StatusCode);

            throw new DependencyUnavailableException(
                $"DataMotion query failed with HTTP {(int)httpResponse.StatusCode}.");
        }

        var response = await httpResponse.Content.ReadFromJsonAsync<StatementResponse>(
                           cancellationToken: cancellationToken)
                       ?? throw new DependencyUnavailableException("DataMotion returned an empty response.");

        return await WaitForCompletionAsync(response, cancellationToken);
    }

    private async Task<StatementResponse> WaitForCompletionAsync(
        StatementResponse response,
        CancellationToken cancellationToken)
    {
        var state = response.Status?.State?.ToUpperInvariant();

        if (state == "SUCCEEDED")
            return response;

        if (state is "FAILED" or "CANCELED" or "CLOSED")
            throw CreateStatementFailure(response, state);

        if (string.IsNullOrWhiteSpace(response.StatementId))
            throw new DependencyUnavailableException("DataMotion did not return a statement id.");

        var statementId = response.StatementId;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.MaxPollSeconds));

        try
        {
            while (true)
            {
                await Task.Delay(_options.PollIntervalMilliseconds, timeout.Token);

                using var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    $"2.0/sql/statements/{Uri.EscapeDataString(statementId)}");

                await ApplyHeadersAsync(request, timeout.Token);

                using var httpResponse = await httpClient.SendAsync(request, timeout.Token);
                if (!httpResponse.IsSuccessStatusCode)
                    throw new DependencyUnavailableException("DataMotion statement polling failed.");

                response = await httpResponse.Content.ReadFromJsonAsync<StatementResponse>(
                               cancellationToken: timeout.Token)
                           ?? throw new DependencyUnavailableException(
                               "DataMotion polling returned an empty response.");

                state = response.Status?.State?.ToUpperInvariant();

                if (state == "SUCCEEDED")
                    return response;

                if (state is "FAILED" or "CANCELED" or "CLOSED")
                    throw CreateStatementFailure(response, state);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new DependencyUnavailableException("DataMotion statement timed out.");
        }
    }

    private async Task<IReadOnlyList<List<object?>>> ReadAllChunksAsync(
        StatementResponse response,
        CancellationToken cancellationToken)
    {
        if (response.Result is null)
            return [];

        var rows = new List<List<object?>>(response.Result.DataArray);
        var nextChunkIndex = response.Result.NextChunkIndex;
        var fetchedChunks = 1;
        var maxChunks = Math.Clamp(_options.MaxResultChunks, 1, 10_000);

        if (nextChunkIndex.HasValue && string.IsNullOrWhiteSpace(response.StatementId))
            throw new DependencyUnavailableException(
                "DataMotion returned chunked data without a statement id.");

        while (nextChunkIndex.HasValue)
        {
            if (fetchedChunks >= maxChunks)
                throw new DependencyUnavailableException(
                    $"DataMotion result exceeded the configured chunk limit ({maxChunks}).");

            var chunk = await GetChunkAsync(
                response.StatementId!,
                nextChunkIndex.Value,
                cancellationToken);

            rows.AddRange(chunk.DataArray);
            nextChunkIndex = chunk.NextChunkIndex;
            fetchedChunks++;
        }

        return rows;
    }

    private async Task<StatementResult> GetChunkAsync(
        string statementId,
        int chunkIndex,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"2.0/sql/statements/{Uri.EscapeDataString(statementId)}/result/chunks/{chunkIndex}");

        await ApplyHeadersAsync(request, cancellationToken);

        using var httpResponse = await httpClient.SendAsync(request, cancellationToken);
        if (!httpResponse.IsSuccessStatusCode)
        {
            logger.LogError(
                "DataMotion result chunk {ChunkIndex} failed with HTTP {StatusCode}.",
                chunkIndex,
                (int)httpResponse.StatusCode);

            throw new DependencyUnavailableException(
                $"DataMotion result chunk {chunkIndex} could not be retrieved.");
        }

        return await httpResponse.Content.ReadFromJsonAsync<StatementResult>(
                   cancellationToken: cancellationToken)
               ?? throw new DependencyUnavailableException(
                   $"DataMotion result chunk {chunkIndex} returned an empty response.");
    }

    private async Task ApplyHeadersAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await tokenProvider.GetTokenAsync(cancellationToken));

        request.Headers.TryAddWithoutValidation(
            SubscriptionHeader,
            _options.SubscriptionKey);
    }

    private void EnsureConfiguration()
    {
        if (!Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out _))
            throw new DependencyUnavailableException(
                "DataMotion BaseUrl is not configured with a valid absolute URI.");

        if (string.IsNullOrWhiteSpace(_options.Warehouse))
            throw new DependencyUnavailableException("DataMotion Warehouse is not configured.");

        if (string.IsNullOrWhiteSpace(_options.SubscriptionKey))
            throw new DependencyUnavailableException("DataMotion SubscriptionKey is not configured.");

        if (_options.PollIntervalMilliseconds is < 250 or > 10_000)
            throw new DependencyUnavailableException(
                "DataMotion PollIntervalMilliseconds is outside the supported range.");

        if (_options.MaxPollSeconds is <= 0 or > 300)
            throw new DependencyUnavailableException(
                "DataMotion MaxPollSeconds is outside the supported range.");

        if (_options.MaxResultChunks is <= 0 or > 10_000)
            throw new DependencyUnavailableException(
                "DataMotion MaxResultChunks is outside the supported range.");
    }

    private static DependencyUnavailableException CreateStatementFailure(
        StatementResponse response,
        string? state)
    {
        var errorCode = response.Status?.Error?.ErrorCode;

        return new DependencyUnavailableException(
            string.IsNullOrWhiteSpace(errorCode)
                ? $"DataMotion statement ended in state {state}."
                : $"DataMotion statement ended in state {state} with error code {errorCode}.");
    }

    private static object? Normalize(object? value)
    {
        if (value is not JsonElement element)
            return value;

        return element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var integer) => integer,
            JsonValueKind.Number when element.TryGetDecimal(out var number) => number,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => element.ToString()
        };
    }
}
