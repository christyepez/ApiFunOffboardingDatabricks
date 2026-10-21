using System.Text.RegularExpressions;
using IdbInvest.Offboarding.Databricks.Facade.Core.Exceptions;
using Microsoft.Azure.Functions.Worker.Http;

namespace IdbInvest.Offboarding.Databricks.Facade.Functions.Http;

internal static partial class RequestContext
{
    [GeneratedRegex("^[A-Za-z0-9._:-]{1,128}$")]
    private static partial Regex CorrelationPattern();

    public static string GetCorrelationId(HttpRequestData req)
    {
        if (req.Headers.TryGetValues("x-correlation-id", out var values))
        {
            var value = values.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(value) && CorrelationPattern().IsMatch(value)) return value;
        }
        return Guid.NewGuid().ToString("D");
    }

    public static int GetInt(HttpRequestData req, string key, int defaultValue)
    {
        var raw = req.Query[key];
        if (string.IsNullOrWhiteSpace(raw)) return defaultValue;
        return int.TryParse(raw, out var value) ? value : throw new InvalidQueryException($"{key} must be an integer.");
    }

    public static bool GetBool(HttpRequestData req, string key, bool defaultValue)
    {
        var raw = req.Query[key];
        if (string.IsNullOrWhiteSpace(raw)) return defaultValue;
        return bool.TryParse(raw, out var value) ? value : throw new InvalidQueryException($"{key} must be true or false.");
    }
}
