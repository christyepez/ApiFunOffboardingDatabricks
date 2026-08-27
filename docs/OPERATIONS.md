# Operations Runbook

## Scope

This runbook covers day-to-day operation of the Offboarding Databricks Facade across DEV, QA, and PROD.

## Primary dependencies

- Azure API Management
- Azure Function App
- Azure Storage required by Functions
- Application Insights
- Log Analytics
- Azure Key Vault where secrets are required
- Azure Databricks workspace
- Databricks SQL Warehouse
- Unity Catalog catalogs/schemas/views
- Microsoft Entra ID

## Health verification

Start with:

```http
GET /offboarding/v1/health
```

Then verify the dependency chain in order:

1. APIM gateway reachable;
2. token accepted by APIM;
3. APIM backend call reaches Function;
4. Function runtime is healthy;
5. Function identity can acquire required credentials;
6. Databricks workspace is reachable;
7. SQL Warehouse is running/available;
8. identity has `CAN USE`, `USE CATALOG`, `USE SCHEMA`, and `SELECT` privileges;
9. target Gold view exists and is queryable.

## Common incidents

### 401 Unauthorized

Check Entra token audience, issuer, expiry, APIM JWT policy, and required app role/scope.

### 403 Forbidden

Check APIM authorization policy and application roles. For backend failures, verify APIM-to-Function identity/access restrictions.

### 400 INVALID_QUERY

This normally indicates a consumer contract issue: unsupported field, operator, sort, filter format, or page size. Do not bypass whitelist validation to fix a consumer request.

### 404 RESOURCE_NOT_FOUND

Confirm the public resource is registered in `resource-definitions.json` and deployed in the current environment.

### 503 DEPENDENCY_UNAVAILABLE

Check Databricks workspace/warehouse availability, DNS/network connectivity, identity/token acquisition, and Databricks permissions.

### High latency

Review:

- Function p95 latency;
- APIM backend duration;
- SQL Warehouse queue/runtime;
- query selectivity;
- requested page size;
- `includeTotal=true` usage;
- cold starts depending on Function plan;
- network path/private endpoint resolution.

## Observability

At minimum capture:

- correlation ID;
- API resource;
- HTTP method/status;
- request duration;
- APIM backend duration;
- Function duration;
- Databricks statement duration/status where safe;
- dependency availability;
- exception category without sensitive payloads.

Do not log access tokens, PATs, Function keys, secrets, authorization headers, or confidential response payloads.

## Recommended alerts

- Function 5xx error rate above agreed threshold;
- APIM 5xx increase;
- p95 latency above SLO;
- repeated Databricks timeout/failure;
- Key Vault access denied events;
- Function unavailable;
- abnormal request volume/rate-limit events;
- budget/cost anomaly for Function, APIM, and Databricks.

## Deployment validation

After every environment deployment:

1. run health check;
2. run unit tests in pipeline;
3. run Postman/Newman collection against the deployed endpoint;
4. verify APIM authentication behavior;
5. execute one valid dynamic query;
6. execute one invalid-field negative query;
7. inspect Application Insights for correlated traces;
8. confirm no secrets appear in logs.

## Rollback

Rollback should restore the last known-good Function package, APIM policy/configuration, and resource registry together when contract behavior changed. Avoid rolling back only one layer if OpenAPI, registry, and implementation were changed as one release.

## Capacity and cost

Protect Databricks and Function consumption with bounded page sizes, APIM rate limits, quotas, query timeouts, and careful use of `includeTotal`. For high-volume export use cases, design a bulk/asynchronous integration rather than increasing synchronous API limits indefinitely.
