# IDB Invest - Offboarding Databricks Facade

> **Repository language standard:** English is the default language for source code, comments, API contracts, test names, assertion messages, commit messages, operational runbooks, and documentation.

Azure Functions facade that exposes governed Databricks Gold views as controlled REST resources. The repository follows the Visual Lease facade baseline (separate `src/` projects, isolated Azure Functions, services, repositories, DTOs, interfaces, and tests) while applying Clean Architecture boundaries.

## Architecture

```text
Consumers / Target Systems
          |
          | OAuth 2.0 / Microsoft Entra ID
          v
Azure API Management
          |  JWT validation, roles, rate limit, policies, correlation
          v
IdbInvest.Offboarding.Databricks.Facade.Functions
          |  HTTP Functions / controller boundary
          v
IdbInvest.Offboarding.Databricks.Facade.Application
          |  services + controlled query engine + resource registry
          v
IdbInvest.Offboarding.Databricks.Facade.Core
          |  DTOs + interfaces + models + exceptions
          ^
          |
IdbInvest.Offboarding.Databricks.Facade.Infrastructure
          |  repository + Databricks REST client + token provider
          v
Databricks Statement Execution API
          -> SQL Warehouse
          -> Unity Catalog
          -> approved Gold views
```

## Clean Architecture projects

- `Core`: DTOs, contracts, models, exceptions, and ports/interfaces.
- `Application`: query service, resource whitelist registry, filter parsing, and parameterized SQL query builder.
- `Infrastructure`: Databricks Statement Execution repository, authentication/token provider, and external-service communication.
- `Functions`: .NET 10 isolated Azure Functions. `Controllers` are HTTP trigger classes and remain thin.
- `Tests`: unit tests covering query security rules, pagination, filtering, count behavior, service orchestration, and metadata.

## Public endpoints

- `GET /api/offboarding/v1/{resource}` - controlled dynamic query.
- `GET /api/offboarding/v1/resources/{resource}/metadata` - public whitelist metadata.
- `GET /api/offboarding/v1/health` - service/dependency health.

Example:

```http
GET /api/offboarding/v1/employees?fields=employeeId,fullName,status&filter=status:eq:ACTIVE&filter=country:eq:EC&sort=-updatedAt&page=1&pageSize=100&includeTotal=true
```

Supported filter operators: `eq`, `ne`, `gt`, `gte`, `lt`, `lte`, `contains`.

No endpoint accepts raw SQL, catalog/schema/view names, or physical column names from a caller. Identifiers are resolved only from `resource-definitions.json`; caller values use Databricks parameter markers.

## Dynamic-query security model

The facade provides flexibility without allowing consumers to control SQL. Each resource defines:

- stable public resource name;
- approved Databricks source view;
- public-to-physical field mapping;
- selectable fields;
- filterable fields;
- sortable fields;
- maximum page size;
- default fields.

A request such as:

```http
GET /api/offboarding/v1/employees?fields=employeeId,status&filter=status:eq:ACTIVE
```

is translated internally to parameterized SQL. `ACTIVE` is sent as a Databricks statement parameter and is not concatenated into the SQL text.

## Databricks authentication and authorization

Production should use identity-based authentication. Recommended order:

1. Managed/Federated Identity when supported by the target Databricks design;
2. Service Principal OAuth;
3. PAT only for local development or exceptional temporary use.

Grant only:

- `CAN USE` on the designated SQL Warehouse;
- `USE CATALOG` on approved catalogs;
- `USE SCHEMA` on approved schemas;
- `SELECT` on approved API-facing views.

Do not grant runtime `OWNERSHIP`, `MODIFY`, or broad create permissions.

## Local development

Prerequisites:

- .NET 10 SDK
- Azure Functions Core Tools v4
- Azurite or valid Function storage
- Databricks DEV access when executing real queries

```bash
cp src/IdbInvest.Offboarding.Databricks.Facade.Functions/local.settings.sample.json \
   src/IdbInvest.Offboarding.Databricks.Facade.Functions/local.settings.json

dotnet restore
dotnet build
dotnet test

cd src/IdbInvest.Offboarding.Databricks.Facade.Functions
func start
```

Local base URL:

```text
http://localhost:7071/api
```

Never commit `local.settings.json`, PATs, access tokens, Function keys, client secrets, or other credentials.

## Automated tests

### Unit tests

Run:

```bash
dotnet test IdbInvest.Offboarding.Databricks.Facade.sln \
  --configuration Release \
  --collect:"XPlat Code Coverage"
```

Coverage focuses on security-sensitive query generation, field/operator whitelists, pagination, count queries, query-service behavior, and public metadata mapping.

### Postman / Newman tests

The repository includes functional and contract tests at:

```text
postman/IDBInvest-Offboarding-Databricks-Facade.postman_collection.json
```

The collection checks health, metadata, valid dynamic queries, normalized errors, unknown fields, invalid page sizes, unknown resources, correlation IDs, and accidental exposure of physical Databricks identifiers.

See [Postman test documentation](postman/README.md).

## API Management

`api/openapi.yaml` is the API contract and `deploy/apim/inbound-policy.xml` is the policy baseline.

Recommended ingress sequence:

1. Microsoft Entra ID application permission/app-role validation;
2. correlation ID handling;
3. rate limit/quota;
4. APIM Managed Identity to Function backend;
5. Function access restrictions to prevent bypassing APIM.

## Resource registry

`resource-definitions.json` is an API governance boundary. Example: public field `employeeId` may map internally to `employee_id`, but the physical name is never accepted from the consumer.

When adding a resource, update the registry, unit tests, OpenAPI contract, Postman tests, and documentation in the same pull request.

## CI/CD

`pipelines/azure-pipelines.yml` performs restore, build, unit tests with XPlat code coverage, Function publish, archive, and artifact publication.

Postman/Newman tests should execute after deployment to a DEV/test endpoint where the full APIM -> Function -> Databricks path is available.

## Documentation index

- [Architecture](docs/ARCHITECTURE.md)
- [Developer Guide](docs/DEVELOPER_GUIDE.md)
- [API Reference](docs/API_REFERENCE.md)
- [Configuration Reference](docs/CONFIGURATION.md)
- [Security Model](docs/SECURITY.md)
- [Testing Strategy](docs/TESTING.md)
- [Operations Runbook](docs/OPERATIONS.md)
- [Postman Functional Tests](postman/README.md)
- [OpenAPI contract](api/openapi.yaml)

## Production readiness notes

- `includeTotal=true` issues an additional `COUNT(1)` query and should be used deliberately on large views.
- The public response intentionally hides Databricks manifests, physical object names, and provider errors.
- For large-volume exports, implement deterministic cursor/keyset pagination or an asynchronous bulk-extract pattern instead of unbounded synchronous API responses.
- Production readiness requires final APIM networking, Entra roles, Databricks grants, Private Link/Private Endpoint decisions, observability, load testing, SLOs, and rollback validation.
