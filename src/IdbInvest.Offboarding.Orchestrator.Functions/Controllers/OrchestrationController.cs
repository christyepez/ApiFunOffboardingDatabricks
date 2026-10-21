using System.Net;
using System.Text.Json;
using IdbInvest.Offboarding.Orchestrator.Application.Interfaces;
using IdbInvest.Offboarding.Orchestrator.Core.Exceptions;
using IdbInvest.Offboarding.Orchestrator.Core.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace IdbInvest.Offboarding.Orchestrator.Functions.Controllers;

public sealed class OrchestrationController(IOffboardingOrchestrator orchestrator)
{
    [Function("StartOffboardingOrchestration")]
    public async Task<HttpResponseData> StartAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "offboarding/v1/orchestrations")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var request = await JsonSerializer.DeserializeAsync<StartRequest>(
            req.Body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web),
            cancellationToken);

        if (request is null)
            throw new InvalidOrchestrationRequestException("Request body is required.");

        var targets = ParseTargets(request.Targets);
        var command = new StartOrchestrationCommand(
            request.EmployeeId ?? string.Empty,
            targets,
            request.IdempotencyKey);

        var result = await orchestrator.ExecuteAsync(command, cancellationToken);
        var status = result.IsDuplicate ? HttpStatusCode.OK : HttpStatusCode.Accepted;
        var response = req.CreateResponse(status);
        response.Headers.Add("Cache-Control", "no-store");

        await response.WriteAsJsonAsync(ToResponse(result), cancellationToken);
        return response;
    }

    [Function("GetOffboardingOrchestration")]
    public async Task<HttpResponseData> GetAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "offboarding/v1/orchestrations/{orchestrationId}")] HttpRequestData req,
        string orchestrationId,
        CancellationToken cancellationToken)
    {
        var orchestration = await orchestrator.GetAsync(orchestrationId, cancellationToken);
        if (orchestration is null)
            return req.CreateResponse(HttpStatusCode.NotFound);

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Cache-Control", "no-store");
        await response.WriteAsJsonAsync(ToResponse(orchestration, false), cancellationToken);
        return response;
    }

    [Function("OffboardingOrchestratorHealth")]
    public async Task<HttpResponseData> HealthAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "offboarding/v1/orchestrator/health")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Cache-Control", "no-store");
        await response.WriteAsJsonAsync(
            new
            {
                status = "Healthy",
                service = "Offboarding.Orchestrator"
            },
            cancellationToken);
        return response;
    }

    [Function("OffboardingOrchestratorReadiness")]
    public async Task<HttpResponseData> ReadinessAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "offboarding/v1/orchestrator/ready")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var readiness = await orchestrator.GetReadinessAsync(cancellationToken);
        var response = req.CreateResponse(
            readiness.IsReady ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable);
        response.Headers.Add("Cache-Control", "no-store");
        await response.WriteAsJsonAsync(
            new
            {
                status = readiness.IsReady ? "Ready" : "Degraded",
                service = "Offboarding.Orchestrator",
                dataExposure = readiness.DataExposureAvailable ? "ok" : "unavailable",
                stateStore = readiness.StateStoreAvailable ? "ok" : "unavailable"
            },
            cancellationToken);
        return response;
    }

    private static IReadOnlyCollection<TargetSystem> ParseTargets(
        IReadOnlyCollection<string>? targets)
    {
        if (targets is null || targets.Count == 0)
            return [];

        var result = new List<TargetSystem>(targets.Count);
        foreach (var target in targets)
        {
            if (!Enum.TryParse<TargetSystem>(target, true, out var parsed))
                throw new InvalidOrchestrationRequestException(
                    $"Unsupported target system '{target}'.");

            result.Add(parsed);
        }

        return result;
    }

    private static object ToResponse(OrchestrationExecutionResult result)
        => ToResponse(result.Orchestration, result.IsDuplicate);

    private static object ToResponse(
        OffboardingOrchestration orchestration,
        bool isDuplicate)
        => new
        {
            orchestrationId = orchestration.OrchestrationId,
            employeeId = orchestration.EmployeeId,
            idempotencyKey = orchestration.IdempotencyKey,
            startedAtUtc = orchestration.StartedAtUtc,
            completedAtUtc = orchestration.CompletedAtUtc,
            status = orchestration.Status.ToString(),
            isDuplicate,
            targets = orchestration.Targets.Select(x => new
            {
                target = x.Target.ToString(),
                success = x.Success,
                status = x.Status,
                message = x.Message
            })
        };

    private sealed record StartRequest(
        string? EmployeeId,
        IReadOnlyCollection<string>? Targets,
        string? IdempotencyKey);
}
