using Microsoft.Azure.Functions.Worker.Http;

namespace IdbInvest.Offboarding.Databricks.Facade.Functions.Http;

internal static class RequestContext
{
    public static string GetCorrelationId(HttpRequestData req)
    {
        if (req.Headers.TryGetValues("x-correlation-id", out var values))
        {
            var value = values.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(value) && value.Length <= 128) return value;
        }
        return Guid.NewGuid().ToString("D");
    }

    public static int GetInt(HttpRequestData req, string key, int defaultValue) =>
        int.TryParse(req.Query[key], out var value) ? value : defaultValue;

    public static bool GetBool(HttpRequestData req, string key, bool defaultValue) =>
        bool.TryParse(req.Query[key], out var value) ? value : defaultValue;
}
