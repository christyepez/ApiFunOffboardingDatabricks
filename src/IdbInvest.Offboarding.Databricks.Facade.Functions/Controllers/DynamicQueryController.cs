using System.Net;
using IdbInvest.Offboarding.Databricks.Facade.Core.DTO;
using IdbInvest.Offboarding.Databricks.Facade.Core.Interfaces;
using IdbInvest.Offboarding.Databricks.Facade.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace IdbInvest.Offboarding.Databricks.Facade.Functions.Controllers;

public sealed class DynamicQueryController(IQueryService service, ILogger<DynamicQueryController> logger)
{
    [Function("OffboardingDynamicQuery")]
    public async Task<HttpResponseData> GetAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "offboarding/v1/{resource}")] HttpRequestData req,
        string resource,
        CancellationToken cancellationToken)
    {
        var correlationId = RequestContext.GetCorrelationId(req);
        var request = new QueryRequestDto(
            req.Query["fields"],
            req.Query.GetValues("filter") ?? [],
            req.Query["sort"],
            RequestContext.GetInt(req, "page", 1),
            RequestContext.GetInt(req, "pageSize", 100),
            RequestContext.GetBool(req, "includeTotal", false));

        logger.LogInformation("Query resource {Resource}; correlation {CorrelationId}; page {Page}; pageSize {PageSize}", resource, correlationId, request.Page, request.PageSize);
        var result = await service.QueryAsync(resource, request, correlationId, cancellationToken);
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("x-correlation-id", correlationId);
        response.Headers.Add("Cache-Control", "no-store");
        await response.WriteAsJsonAsync(result, cancellationToken);
        return response;
    }
}
