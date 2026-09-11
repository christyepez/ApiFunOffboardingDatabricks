using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace IdbInvest.Offboarding.Databricks.Facade.Functions.Controllers;

public sealed class SwaggerController
{
    private const string SwaggerHtml = """
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>IDB Invest Offboarding API - Swagger</title>
  <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/swagger-ui-dist@5/swagger-ui.css" />
  <style>body{margin:0;background:#fafafa}.topbar{display:none}</style>
</head>
<body>
  <div id="swagger-ui"></div>
  <script src="https://cdn.jsdelivr.net/npm/swagger-ui-dist@5/swagger-ui-bundle.js"></script>
  <script>
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
  </script>
</body>
</html>
""";

    [Function("SwaggerUi")]
    public async Task<HttpResponseData> SwaggerAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "swagger")] HttpRequestData req,
        CancellationToken cancellationToken)
        => await CreateSwaggerResponseAsync(req, cancellationToken);

    [Function("SwaggerUiIndex")]
    public async Task<HttpResponseData> SwaggerIndexAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "swagger/index.html")] HttpRequestData req,
        CancellationToken cancellationToken)
        => await CreateSwaggerResponseAsync(req, cancellationToken);

    [Function("OpenApiDocument")]
    public async Task<HttpResponseData> OpenApiAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "openapi.yaml")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "openapi.yaml");
        if (!File.Exists(path))
        {
            var missing = req.CreateResponse(HttpStatusCode.NotFound);
            missing.Headers.Add("Cache-Control", "no-store");
            await missing.WriteStringAsync("OpenAPI document was not found.", cancellationToken);
            return missing;
        }

        var yaml = await File.ReadAllTextAsync(path, cancellationToken);
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/yaml; charset=utf-8");
        response.Headers.Add("Cache-Control", "no-store");
        await response.WriteStringAsync(yaml, cancellationToken);
        return response;
    }

    private static async Task<HttpResponseData> CreateSwaggerResponseAsync(HttpRequestData req, CancellationToken cancellationToken)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/html; charset=utf-8");
        response.Headers.Add("Cache-Control", "no-store");
        response.Headers.Add("X-Content-Type-Options", "nosniff");
        response.Headers.Add("Content-Security-Policy", "default-src 'none'; script-src https://cdn.jsdelivr.net; style-src 'unsafe-inline' https://cdn.jsdelivr.net; img-src data:; connect-src 'self' https:; font-src https://cdn.jsdelivr.net data:");
        await response.WriteStringAsync(SwaggerHtml, cancellationToken);
        return response;
    }
}

