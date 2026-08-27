using System.Net;
using IdbInvest.Offboarding.Databricks.Facade.Core.Interfaces;
using IdbInvest.Offboarding.Databricks.Facade.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace IdbInvest.Offboarding.Databricks.Facade.Functions.Controllers;

public sealed class HealthController(IDatabricksRepository repository)
{
    [Function("OffboardingHealth")]
    public async Task<HttpResponseData> GetAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "offboarding/v1/health")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var correlationId = RequestContext.GetCorrelationId(req);
        var dbx = await repository.PingAsync(cancellationToken);
        var response = req.CreateResponse(dbx ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable);
        response.Headers.Add("x-correlation-id", correlationId);
        await response.WriteAsJsonAsync(new { status = dbx ? "ok" : "degraded", service = "Offboarding.Databricks.Facade", databricks = dbx ? "ok" : "unavailable", correlationId }, cancellationToken);
        return response;
    }
}
