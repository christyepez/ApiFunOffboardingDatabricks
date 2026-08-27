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
        var columns = response.Manifest?.Schema?.Columns.Select(x => x.Name).ToArray() ?? [];
        var data = response.Result?.DataArray ?? [];
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
        var payload = new StatementExecuteRequest(_options.WarehouseId, sql,
            parameters.Select(x => new StatementParameter(x.Name, x.Value, x.Type)).ToArray(),
            $"{Math.Clamp(_options.WaitTimeoutSeconds, 5, 50)}s");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/2.0/sql/statements") { Content = JsonContent.Create(payload) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokenProvider.GetTokenAsync(cancellationToken));
        var httpResponse = await httpClient.SendAsync(request, cancellationToken);
        if (!httpResponse.IsSuccessStatusCode)
        {
            var body = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("Databricks returned HTTP {StatusCode}. Body suppressed from external response. Internal body: {Body}", (int)httpResponse.StatusCode, body);
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
            throw new DependencyUnavailableException($"Databricks statement ended in state {state}.");
        if (string.IsNullOrWhiteSpace(response.StatementId))
            throw new DependencyUnavailableException("Databricks did not return a statement id.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.MaxPollSeconds));
        while (!timeout.IsCancellationRequested)
        {
            await Task.Delay(_options.PollIntervalMilliseconds, timeout.Token);
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/2.0/sql/statements/{Uri.EscapeDataString(response.StatementId)}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokenProvider.GetTokenAsync(timeout.Token));
            var httpResponse = await httpClient.SendAsync(request, timeout.Token);
            if (!httpResponse.IsSuccessStatusCode) throw new DependencyUnavailableException("Databricks statement polling failed.");
            response = await httpResponse.Content.ReadFromJsonAsync<StatementResponse>(cancellationToken: timeout.Token)
                       ?? throw new DependencyUnavailableException("Databricks polling returned an empty response.");
            state = response.Status?.State?.ToUpperInvariant();
            if (state == "SUCCEEDED") return response;
            if (state is "FAILED" or "CANCELED" or "CLOSED")
                throw new DependencyUnavailableException($"Databricks statement ended in state {state}.");
        }
        throw new DependencyUnavailableException("Databricks statement timed out.");
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
