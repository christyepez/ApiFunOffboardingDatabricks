using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using IdbInvest.Offboarding.Orchestrator.Core.Interfaces;
using IdbInvest.Offboarding.Orchestrator.Core.Models;

namespace IdbInvest.Offboarding.Orchestrator.Infrastructure.State;

public sealed class AzureTableOrchestrationStateStore(TableClient tableClient)
    : IOrchestrationStateStore
{
    private const string PartitionKeyValue = "offboarding";
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<OffboardingOrchestration?> GetByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var indexRowKey = BuildIdempotencyRowKey(idempotencyKey);
        var index = await GetEntityOrNullAsync(indexRowKey, cancellationToken);
        if (index is null ||
            !index.TryGetValue("OrchestrationId", out var idValue) ||
            idValue is not string orchestrationId ||
            string.IsNullOrWhiteSpace(orchestrationId))
            return null;

        return await GetByIdAsync(orchestrationId, cancellationToken);
    }

    public async Task<OffboardingOrchestration?> GetByIdAsync(
        string orchestrationId,
        CancellationToken cancellationToken)
    {
        var entity = await GetEntityOrNullAsync(
            BuildOrchestrationRowKey(orchestrationId),
            cancellationToken);

        if (entity is null ||
            !entity.TryGetValue("Payload", out var payloadValue) ||
            payloadValue is not string payload ||
            string.IsNullOrWhiteSpace(payload))
            return null;

        return JsonSerializer.Deserialize<OffboardingOrchestration>(
            payload,
            JsonOptions);
    }

    public async Task SaveAsync(
        OffboardingOrchestration orchestration,
        CancellationToken cancellationToken)
    {
        await tableClient.CreateIfNotExistsAsync(cancellationToken);

        var orchestrationEntity = new TableEntity(
            PartitionKeyValue,
            BuildOrchestrationRowKey(orchestration.OrchestrationId))
        {
            ["Payload"] = JsonSerializer.Serialize(orchestration, JsonOptions),
            ["IdempotencyKey"] = orchestration.IdempotencyKey,
            ["EmployeeId"] = orchestration.EmployeeId,
            ["Status"] = orchestration.Status.ToString(),
            ["StartedAtUtc"] = orchestration.StartedAtUtc,
            ["CompletedAtUtc"] = orchestration.CompletedAtUtc
        };

        var idempotencyEntity = new TableEntity(
            PartitionKeyValue,
            BuildIdempotencyRowKey(orchestration.IdempotencyKey))
        {
            ["OrchestrationId"] = orchestration.OrchestrationId,
            ["IdempotencyKey"] = orchestration.IdempotencyKey
        };

        var actions = new[]
        {
            new TableTransactionAction(
                TableTransactionActionType.UpsertReplace,
                orchestrationEntity),
            new TableTransactionAction(
                TableTransactionActionType.UpsertReplace,
                idempotencyEntity)
        };

        await tableClient.SubmitTransactionAsync(actions, cancellationToken);
    }

    private async Task<TableEntity?> GetEntityOrNullAsync(
        string rowKey,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await tableClient.GetEntityAsync<TableEntity>(
                PartitionKeyValue,
                rowKey,
                cancellationToken: cancellationToken);

            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    private static string BuildOrchestrationRowKey(string orchestrationId)
        => $"O|{orchestrationId.Trim()}";

    private static string BuildIdempotencyRowKey(string idempotencyKey)
    {
        var hash = SHA256.HashData(
            Encoding.UTF8.GetBytes(idempotencyKey.Trim()));

        return $"I|{Convert.ToHexString(hash)}";
    }
}
