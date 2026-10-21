using System.Net;
using IdbInvest.Offboarding.Orchestrator.Core.Exceptions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;

namespace IdbInvest.Offboarding.Orchestrator.Functions.Middleware;

public sealed class ExceptionHandlingMiddleware(
    ILogger<ExceptionHandlingMiddleware> logger) : IFunctionsWorkerMiddleware
{
    public async Task Invoke(
        FunctionContext context,
        FunctionExecutionDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var request = await context.GetHttpRequestDataAsync();
            if (request is null)
                throw;

            var (status, code, message) = ex switch
            {
                InvalidOrchestrationRequestException =>
                    (HttpStatusCode.BadRequest, "INVALID_ORCHESTRATION_REQUEST", ex.Message),
                OffboardingCandidateNotFoundException =>
                    (HttpStatusCode.NotFound, "OFFBOARDING_CANDIDATE_NOT_FOUND", ex.Message),
                _ =>
                    (HttpStatusCode.InternalServerError, "INTERNAL_ERROR", "An unexpected error occurred.")
            };

            var correlationId = GetCorrelationId(request);

            if ((int)status >= 500)
                logger.LogError(ex, "Orchestrator request failed. CorrelationId {CorrelationId}", correlationId);
            else
                logger.LogWarning(ex, "Orchestrator request rejected. CorrelationId {CorrelationId}", correlationId);

            var response = request.CreateResponse(status);
            response.Headers.Add("x-correlation-id", correlationId);
            response.Headers.Add("Cache-Control", "no-store");
            await response.WriteAsJsonAsync(
                new
                {
                    code,
                    message,
                    correlationId
                });

            context.GetInvocationResult().Value = response;
        }
    }

    private static string GetCorrelationId(HttpRequestData request)
    {
        if (request.Headers.TryGetValues("x-correlation-id", out var values))
        {
            var value = values.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(value) && value.Length <= 128)
                return value;
        }

        return Guid.NewGuid().ToString("D");
    }
}
