using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;

namespace IdbInvest.Offboarding.Databricks.Facade.Functions.Controllers;

public sealed class SwaggerController(IConfiguration configuration)
{
    private const string CacheControlHeader = "Cache-Control";
    private const string ContentSecurityPolicyHeader = "Content-Security-Policy";
    private const string ContentTypeHeader = "Content-Type";
    private const string HttpGetMethod = "get";
    private const string NoStoreDirective = "no-store";
    private const string NoSniffHeader = "X-Content-Type-Options";
    private const string NoSniffValue = "nosniff";
    private const string OpenApiFileName = "openapi.yaml";
    private const string OpenApiRoute = OpenApiFileName;
    private const string SwaggerBaseRoute = "swagger";
    private const string SwaggerIndexSuffix = "/index.html";
    private const string SwaggerIndexRoute = SwaggerBaseRoute + SwaggerIndexSuffix;
    private const string SwaggerInitializerSuffix = "/init.js";
    private const string SwaggerInitializerRoute = SwaggerBaseRoute + SwaggerInitializerSuffix;

    private readonly string _stylesheetUrl = configuration.GetRequiredSection("SwaggerUi:StylesheetUrl").Value!;
    private readonly string _bundleUrl = configuration.GetRequiredSection("SwaggerUi:BundleUrl").Value!;
    private readonly string _contentSecurityPolicy = configuration.GetRequiredSection("SwaggerUi:ContentSecurityPolicy").Value!;

    private const string SwaggerInitializer = """
window.onload = () => {
  SwaggerUIBundle({
    url: window.location.pathname.replace(/\/swagger(?:\/index\.html)?$/, '/openapi.yaml'),
    dom_id: '#swagger-ui',
    deepLinking: true,
    displayRequestDuration: true,
    persistAuthorization: false,
    tryItOutEnabled: true
  });
};
""";

    [Function("SwaggerUi")]
    public Task<HttpResponseData> SwaggerAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, HttpGetMethod, Route = SwaggerBaseRoute)] HttpRequestData req,
        CancellationToken cancellationToken) => CreateSwaggerResponseAsync(req, cancellationToken);

    [Function("SwaggerUiIndex")]
    public Task<HttpResponseData> SwaggerIndexAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, HttpGetMethod, Route = SwaggerIndexRoute)] HttpRequestData req,
        CancellationToken cancellationToken) => CreateSwaggerResponseAsync(req, cancellationToken);

    [Function("SwaggerInitializer")]
    public async Task<HttpResponseData> SwaggerInitializerAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, HttpGetMethod, Route = SwaggerInitializerRoute)] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        ApplyStandardHeaders(response, "application/javascript; charset=utf-8", includeNoSniff: true);
        await response.WriteStringAsync(SwaggerInitializer, cancellationToken);
        return response;
    }

    [Function("OpenApiDocument")]
    public async Task<HttpResponseData> OpenApiAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, HttpGetMethod, Route = OpenApiRoute)] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(AppContext.BaseDirectory, OpenApiFileName);
        if (!File.Exists(path))
        {
            var missing = req.CreateResponse(HttpStatusCode.NotFound);
            ApplyStandardHeaders(missing);
            await missing.WriteStringAsync("OpenAPI document was not found.", cancellationToken);
            return missing;
        }

        var yaml = await File.ReadAllTextAsync(path, cancellationToken);
        var response = req.CreateResponse(HttpStatusCode.OK);
        ApplyStandardHeaders(response, "application/yaml; charset=utf-8");
        await response.WriteStringAsync(yaml, cancellationToken);
        return response;
    }

    private async Task<HttpResponseData> CreateSwaggerResponseAsync(HttpRequestData req, CancellationToken cancellationToken)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        ApplyStandardHeaders(response, "text/html; charset=utf-8", includeNoSniff: true);
        response.Headers.Add(ContentSecurityPolicyHeader, _contentSecurityPolicy);
        await response.WriteStringAsync(CreateSwaggerHtml(req), cancellationToken);
        return response;
    }

    private static void ApplyStandardHeaders(HttpResponseData response, string? contentType = null, bool includeNoSniff = false)
    {
        if (contentType is not null)
        {
            response.Headers.Add(ContentTypeHeader, contentType);
        }

        response.Headers.Add(CacheControlHeader, NoStoreDirective);
        if (includeNoSniff)
        {
            response.Headers.Add(NoSniffHeader, NoSniffValue);
        }
    }

    private string CreateSwaggerHtml(HttpRequestData req)
    {
        var path = req.Url.AbsolutePath;
        var swaggerBasePath = path.EndsWith(SwaggerIndexSuffix, StringComparison.OrdinalIgnoreCase)
            ? path[..^SwaggerIndexSuffix.Length]
            : path.TrimEnd('/');
        var initializerPath = $"{swaggerBasePath}{SwaggerInitializerSuffix}";

        return $$"""
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>IDB Invest Offboarding API - Swagger</title>
  <link rel="stylesheet" href="{{_stylesheetUrl}}" />
</head>
<body>
  <div id="swagger-ui"></div>
  <script src="{{_bundleUrl}}"></script>
  <script src="{{initializerPath}}"></script>
</body>
</html>
""";
    }
}