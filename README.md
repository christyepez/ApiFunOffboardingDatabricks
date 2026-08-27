# IDB Invest - Offboarding Databricks Facade

Azure Functions facade that exposes governed Databricks Gold views as controlled REST resources. The repository follows the Visual Lease facade baseline (separate `src/` projects + isolated Functions + tests) while applying Clean Architecture boundaries.

## Architecture

```text
Consumers
   |
Azure API Management
   |  OAuth2/Entra ID, rate limit, policies, correlation
   v
IdbInvest.Offboarding.Databricks.Facade.Functions
   |  HTTP Functions (controller boundary)
   v
IdbInvest.Offboarding.Databricks.Facade.Application
   |  services + query builder + resource registry
   v
IdbInvest.Offboarding.Databricks.Facade.Core
   |  DTOs + interfaces + models + exceptions
   ^
   |
IdbInvest.Offboarding.Databricks.Facade.Infrastructure
   |  repository + Databricks REST client + token provider
   v
Databricks SQL Statement Execution API -> SQL Warehouse -> Unity Catalog Gold views
```

## Projects

- `Core`: DTOs, contracts, models, exceptions and ports/interfaces.
- `Application`: query service, whitelist registry and parameterized SQL query builder.
- `Infrastructure`: Databricks Statement Execution repository, OAuth/Managed Identity token provider and HTTP resilience.
- `Functions`: .NET 10 isolated Azure Functions. `Controllers` are HTTP trigger classes; they replace MVC controllers in the serverless model.
- `Tests`: unit tests for the security-sensitive query builder.

## Endpoints

- `GET /api/offboarding/v1/{resource}` dynamic controlled query.
- `GET /api/offboarding/v1/resources/{resource}/metadata` public whitelist metadata.
- `GET /api/offboarding/v1/health` dependency health.

Example:

```http
GET /api/offboarding/v1/employees?fields=employeeId,fullName,status&filter=status:eq:ACTIVE&filter=country:eq:EC&sort=-updatedAt&page=1&pageSize=100&includeTotal=true
```

Supported filter operators: `eq`, `ne`, `gt`, `gte`, `lt`, `lte`, `contains`.

No endpoint accepts raw SQL, catalog/schema/view names, or physical column names from a caller. Identifiers are resolved only from `resource-definitions.json`; filter values use Databricks parameter markers.

## Databricks authentication

Production default is `DefaultAzureCredential`. In Azure this allows the Function identity/service principal configuration selected by the platform. Grant only:

- `CAN USE` on the SQL Warehouse.
- `USE CATALOG` on the approved catalog.
- `USE SCHEMA` on the approved schema.
- `SELECT` on approved API-facing views.

`PAT` mode is included only for local development and should never be committed. Copy `local.settings.sample.json` to `local.settings.json` and supply local values outside Git.

## Local run

Prerequisites: .NET 10 SDK, Azure Functions Core Tools v4, Azurite (or a valid Function storage connection).

```bash
cp src/IdbInvest.Offboarding.Databricks.Facade.Functions/local.settings.sample.json \
   src/IdbInvest.Offboarding.Databricks.Facade.Functions/local.settings.json

dotnet restore
dotnet build
dotnet test
cd src/IdbInvest.Offboarding.Databricks.Facade.Functions
func start
```

Local base URL: `http://localhost:7071/api`.

## Resource registry

`resource-definitions.json` is the API governance boundary. Example `employees` maps public field `employeeId` to physical column `employee_id`. Add resources only through reviewed configuration; never allow the caller to supply `source` or `column`.

## APIM

`api/openapi.yaml` is the initial API contract and `deploy/apim/inbound-policy.xml` is the policy baseline. Recommended ingress chain:

1. Entra ID application permission/app role validation.
2. Correlation id.
3. Rate limit / quota by consumer.
4. APIM Managed Identity to Function backend.
5. Function access restricted so consumers cannot bypass APIM.

## Notes

- `includeTotal=true` issues a second `COUNT(1)` statement and should be used intentionally for large views.
- The public response intentionally does not expose Databricks manifests, statement errors, or physical object names.
- For very large datasets, replace page/offset with a deterministic cursor/keyset implementation before production-scale bulk integrations.
