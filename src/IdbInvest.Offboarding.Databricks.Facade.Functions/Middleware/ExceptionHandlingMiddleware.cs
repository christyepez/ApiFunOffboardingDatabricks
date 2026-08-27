using System.Net;
using IdbInvest.Offboarding.Databricks.Facade.Core.DTO;
using IdbInvest.Offboarding.Databricks.Facade.Core.Exceptions;
using IdbInvest.Offboarding.Databricks.Facade.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;

namespace IdbInvest.Offboarding.Databricks.Facade.Functions.Middleware;

public sealed class ExceptionHandlingMiddleware(ILogger<ExceptionHandlingMiddleware> logger) : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var req = await context.GetHttpRequestDataAsync();
            if (req is null) throw;
            var correlationId = RequestContext.GetCorrelationId(req);
            var api = ex as ApiException;
            var status = api is null ? 500 : api.StatusCode;
            var code = api?.Code ?? "INTERNAL_ERROR";
            var message = api?.Message ?? "An unexpected error occurred.";
            if (status >= 500) logger.LogError(ex, "Request failed. CorrelationId {CorrelationId}", correlationId);
            else logger.LogWarning(ex, "Request rejected. CorrelationId {CorrelationId}", correlationId);
            var response = req.CreateResponse((HttpStatusCode)status);
            response.Headers.Add("x-correlation-id", correlationId);
            response.Headers.Add("Cache-Control", "no-store");
            await response.WriteAsJsonAsync(new ApiErrorDto(code, message, correlationId));
            context.GetInvocationResult().Value = response;
        }
    }
}
