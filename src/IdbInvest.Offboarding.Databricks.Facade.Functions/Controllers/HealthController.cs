using System.Net;
using IdbInvest.Offboarding.Databricks.Facade.Core.Interfaces;
using IdbInvest.Offboarding.Databricks.Facade.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace IdbInvest.Offboarding.Databricks.Facade.Functions.Controllers;

public sealed class HealthController(IDatabricksRepository repository)
{
    private const string CacheControlHeader = "Cache-Control";
    private const string CorrelationHeader = "x-correlation-id";
    private const string NoStoreDirective = "no-store";
    private const string ServiceName = "Offboarding.Databricks.Facade";

    [Function("Health")]
    public async Task<HttpResponseData> CorporateHealthAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var correlationId = RequestContext.GetCorrelationId(req);
        var response = req.CreateResponse(HttpStatusCode.OK);
        ApplyStandardHeaders(response, correlationId);
        await response.WriteAsJsonAsync(new
        {
            status = "Healthy",
            service = ServiceName,
            correlationId
        }, cancellationToken);
        return response;
    }

    [Function("OffboardingHealth")]
    public async Task<HttpResponseData> DependencyHealthAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "offboarding/v1/health")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var correlationId = RequestContext.GetCorrelationId(req);
        var databricksHealthy = await repository.PingAsync(cancellationToken);
        var response = req.CreateResponse(databricksHealthy ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable);
        ApplyStandardHeaders(response, correlationId);
        await response.WriteAsJsonAsync(new
        {
            status = databricksHealthy ? "ok" : "degraded",
            service = ServiceName,
            databricks = databricksHealthy ? "ok" : "unavailable",
            correlationId
        }, cancellationToken);
        return response;
    }

    private static void ApplyStandardHeaders(HttpResponseData response, string correlationId)
    {
        response.Headers.Add(CorrelationHeader, correlationId);
        response.Headers.Add(CacheControlHeader, NoStoreDirective);
    }
}
