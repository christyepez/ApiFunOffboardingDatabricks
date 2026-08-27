using System.Net;
using IdbInvest.Offboarding.Databricks.Facade.Core.Interfaces;
using IdbInvest.Offboarding.Databricks.Facade.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace IdbInvest.Offboarding.Databricks.Facade.Functions.Controllers;

public sealed class MetadataController(IQueryService service)
{
    [Function("OffboardingResourceMetadata")]
    public async Task<HttpResponseData> GetAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "offboarding/v1/resources/{resource}/metadata")] HttpRequestData req,
        string resource,
        CancellationToken cancellationToken)
    {
        var correlationId = RequestContext.GetCorrelationId(req);
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("x-correlation-id", correlationId);
        await response.WriteAsJsonAsync(service.GetMetadata(resource), cancellationToken);
        return response;
    }
}
