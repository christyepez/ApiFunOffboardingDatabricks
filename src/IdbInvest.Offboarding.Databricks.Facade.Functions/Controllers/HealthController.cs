using System.Net;
using IdbInvest.Offboarding.Databricks.Facade.Core.Interfaces;
using IdbInvest.Offboarding.Databricks.Facade.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace IdbInvest.Offboarding.Databricks.Facade.Functions.Controllers;

public sealed class HealthController(IDatabricksRepository repository)
{
    [Function("OffboardingLiveness")]
    public async Task<HttpResponseData> LiveAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "offboarding/v1/health/live")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var correlationId = RequestContext.GetCorrelationId(req);
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("x-correlation-id", correlationId);
        response.Headers.Add("Cache-Control", "no-store");
        await response.WriteAsJsonAsync(new
        {
            status = "ok",
            service = "Offboarding.Databricks.Facade",
            correlationId
        }, cancellationToken);
        return response;
    }

    [Function("OffboardingReadiness")]
    public Task<HttpResponseData> ReadyAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "offboarding/v1/health/ready")] HttpRequestData req,
        CancellationToken cancellationToken) => DependencyHealthAsync(req, cancellationToken);

    [Function("OffboardingHealthCheck")]
    public Task<HttpResponseData> HealthCheckAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "offboarding/v1/healthcheck")] HttpRequestData req,
        CancellationToken cancellationToken) => DependencyHealthAsync(req, cancellationToken);

    [Function("OffboardingHealth")]
    public Task<HttpResponseData> GetAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "offboarding/v1/health")] HttpRequestData req,
        CancellationToken cancellationToken) => DependencyHealthAsync(req, cancellationToken);

    private async Task<HttpResponseData> DependencyHealthAsync(HttpRequestData req, CancellationToken cancellationToken)
    {
        var correlationId = RequestContext.GetCorrelationId(req);
        var dbx = await repository.PingAsync(cancellationToken);
        var response = req.CreateResponse(dbx ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable);
        response.Headers.Add("x-correlation-id", correlationId);
        response.Headers.Add("Cache-Control", "no-store");
        await response.WriteAsJsonAsync(new
        {
            status = dbx ? "ok" : "degraded",
            service = "Offboarding.Databricks.Facade",
            databricks = dbx ? "ok" : "unavailable",
            correlationId
        }, cancellationToken);
        return response;
    }
}
