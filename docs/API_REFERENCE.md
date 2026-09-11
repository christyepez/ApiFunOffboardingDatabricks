# Offboarding Data Exposure API

## 1. Purpose

The Offboarding Data Exposure API is a read-only governed facade that exposes HR-approved offboarding candidates from curated Databricks Gold views. Consumer systems such as ACBS, Workiva, SaaS platforms, legacy applications, and future integrations retrieve eligible records and remain responsible for their own downstream deprovisioning actions.

The API does not execute offboarding actions, does not accept arbitrary SQL, and does not expose physical Databricks catalog, schema, view, or column identifiers.

## 2. Logical contract

Current resource:

```text
employees
```

Logical model:

```text
employee-offboarding-candidate
```

Contract version:

```text
1.0
```

Server-enforced eligibility rules for `employees`:

```text
status = OFFBOARDED
terminationDate >= UTC today - 30 days
```

Consumer filters are additive and cannot remove these mandatory rules.

## 3. Base URLs

Local Azure Functions host:

```text
http://localhost:7071/api
```

Example local endpoint:

```text
http://localhost:7071/api/offboarding/v1/employees
```

DEV through API Management:

```text
https://<apim-host>/offboarding/v1
```

Replace `<apim-host>` with the actual IDB Invest API Management gateway host. The repository intentionally does not hard-code a corporate gateway hostname that has not been confirmed.

## 4. Authentication and authorization

Local development can run without APIM authentication when using the Functions host directly.

Hosted consumers should call the API through API Management using a Microsoft Entra ID OAuth 2.0 bearer token:

```http
Authorization: Bearer <access-token>
```

APIM is expected to validate the token and enforce the approved authorization model. The API is read-only.

## 5. Correlation and cache control

Clients may send:

```http
x-correlation-id: client-generated-identifier
```

Allowed characters are letters, numbers, `.`, `_`, `:`, and `-`, with a maximum length of 128 characters. If the header is omitted or invalid, the API generates a correlation identifier.

Application responses include:

```http
x-correlation-id: <resolved-id>
Cache-Control: no-store
```

## 6. Endpoints

### 6.1 Liveness

```http
GET /offboarding/v1/health/live
```

Does not call Databricks. Use it to verify that the Function process is alive.

Example response:

```json
{
  "status": "ok",
  "service": "Offboarding.Databricks.Facade",
  "correlationId": "health-live-001"
}
```

Expected HTTP status:

```text
200
```

### 6.2 Readiness

```http
GET /offboarding/v1/health/ready
```

Checks the Databricks dependency.

Healthy example:

```json
{
  "status": "ok",
  "service": "Offboarding.Databricks.Facade",
  "databricks": "ok",
  "correlationId": "health-ready-001"
}
```

Degraded example:

```json
{
  "status": "degraded",
  "service": "Offboarding.Databricks.Facade",
  "databricks": "unavailable",
  "correlationId": "health-ready-002"
}
```

Expected status codes:

```text
200 when Databricks is reachable
503 when Databricks is unavailable or not configured correctly
```

### 6.3 Healthcheck

```http
GET /offboarding/v1/healthcheck
```

End-to-end dependency health alias. Returns `200` when healthy and `503` when Databricks is unavailable.

### 6.4 Dependency health alias

```http
GET /offboarding/v1/health
```

Backward-compatible alias for dependency health.

### 6.5 Corporate platform health

The Function host also exposes the platform-level health endpoint:

```http
GET /api/health
```

It returns `200` with `Healthy` or `503` with `Unhealthy`. This endpoint sits outside the versioned consumer contract.

### 6.6 Resource metadata

```http
GET /offboarding/v1/resources/{resource}/metadata
```

Example:

```http
GET /offboarding/v1/resources/employees/metadata
```

Example response:

```json
{
  "resource": "employees",
  "model": "employee-offboarding-candidate",
  "contractVersion": "1.0",
  "fields": [
    {
      "name": "employeeId",
      "type": "STRING",
      "filterable": true,
      "sortable": true,
      "selectable": true
    },
    {
      "name": "email",
      "type": "STRING",
      "filterable": true,
      "sortable": false,
      "selectable": true
    }
  ],
  "maxPageSize": 1000
}
```

The metadata endpoint exposes only logical/public names.

### 6.7 Dynamic resource query

```http
GET /offboarding/v1/{resource}
```

Minimal request using server defaults:

```http
GET /offboarding/v1/employees
```

Full example:

```http
GET /offboarding/v1/employees?fields=employeeId,fullName,email,status,department,country,terminationDate,updatedAt&filter=status:eq:OFFBOARDED&sort=-terminationDate,employeeId&page=1&pageSize=100&includeTotal=false
```

Multiple filters use repeated `filter` parameters:

```http
GET /offboarding/v1/employees?filter=status:eq:OFFBOARDED&filter=country:eq:US&page=1&pageSize=100
```

A semicolon-separated filter expression is also accepted by the current parser:

```text
filter=status:eq:OFFBOARDED;country:eq:US
```

## 7. Query parameters

| Parameter | Required | Default | Description |
|---|---:|---:|---|
| `fields` | No | resource defaults | Comma-separated public selectable fields. |
| `filter` | No | none | `field:operator:value`. May be repeated. |
| `sort` | No | resource default | Comma-separated sortable fields; `-` means descending. |
| `page` | No | `1` | 1-based page number. |
| `pageSize` | No | `100` | Maximum for `employees` is `1000`. |
| `includeTotal` | No | `false` | When `true`, executes an additional count query. |

Supported operators:

```text
eq
ne
gt
gte
lt
lte
contains
```

All caller values are parameterized before being sent to Databricks.

## 8. Public employee fields

Current public fields are:

```text
employeeId
fullName
email
status
department
country
terminationDate
updatedAt
```

These names are part of the public logical contract. Consumers must not depend on physical Databricks identifiers.

## 9. Successful query response

```json
{
  "data": [
    {
      "employeeId": "12345",
      "fullName": "Example User",
      "email": "example.user@iadb.org",
      "status": "OFFBOARDED",
      "department": "Finance",
      "country": "US",
      "terminationDate": "2026-09-01",
      "updatedAt": "2026-09-10T15:10:00Z"
    }
  ],
  "meta": {
    "resource": "employees",
    "model": "employee-offboarding-candidate",
    "contractVersion": "1.0",
    "page": 1,
    "pageSize": 100,
    "returned": 1,
    "total": null,
    "hasMore": false,
    "correlationId": "postman-42f279ce-55cb-4af3-9794-98b29cf49d29"
  }
}
```

When `includeTotal=true`, `meta.total` contains the result of the additional count query.

## 10. Error contract

Application errors use:

```json
{
  "code": "INVALID_QUERY",
  "message": "Field 'password' is not selectable for resource 'employees'.",
  "correlationId": "postman-error-001"
}
```

Expected mapping:

| HTTP | Code | Meaning |
|---:|---|---|
| `400` | `INVALID_QUERY` | Invalid fields, filters, pagination, booleans, or other caller input. |
| `401` | APIM/platform | Missing or invalid authentication. |
| `403` | APIM/platform | Caller lacks authorization. |
| `404` | `RESOURCE_NOT_FOUND` | Logical resource is not exposed. |
| `429` | APIM/platform | Rate limit exceeded. |
| `503` | `DEPENDENCY_UNAVAILABLE` or health payload | Databricks unavailable, timeout, invalid hosted configuration, or dependency failure. |
| `500` | `INTERNAL_ERROR` | Unexpected internal failure. |

Caller-supplied PII is not echoed in normalized malformed-filter errors.

## 11. cURL examples

### Local liveness

```bash
curl -i "http://localhost:7071/api/offboarding/v1/health/live"
```

### Local metadata

```bash
curl -i \
  -H "x-correlation-id: curl-metadata-001" \
  "http://localhost:7071/api/offboarding/v1/resources/employees/metadata"
```

### Local query

```bash
curl -G \
  -H "x-correlation-id: curl-query-001" \
  --data-urlencode "fields=employeeId,fullName,email,status,department,country,terminationDate,updatedAt" \
  --data-urlencode "filter=status:eq:OFFBOARDED" \
  --data-urlencode "sort=-terminationDate,employeeId" \
  --data-urlencode "page=1" \
  --data-urlencode "pageSize=100" \
  --data-urlencode "includeTotal=false" \
  "http://localhost:7071/api/offboarding/v1/employees"
```

### DEV through APIM

```bash
curl -G \
  -H "Authorization: Bearer <access-token>" \
  -H "x-correlation-id: curl-dev-001" \
  --data-urlencode "page=1" \
  --data-urlencode "pageSize=100" \
  "https://<apim-host>/offboarding/v1/employees"
```

## 12. Postman

Repository artifacts:

```text
postman/IDBInvest_Offboarding_Postman_Collection.json
postman/local.postman_environment.json
postman/dev-apim.postman_environment.json
```

### Local

Import the collection and `local.postman_environment.json`, then select:

```text
Offboarding Databricks Facade - Local
```

The local environment uses:

```text
baseUrl = http://localhost:7071/api
```

### DEV/APIM

Import `dev-apim.postman_environment.json`, then select:

```text
Offboarding Databricks Facade - DEV APIM
```

Set:

```text
baseUrl = https://<actual-apim-host>
accessToken = <Microsoft Entra ID access token>
```

The collection contains an `Authorization: Bearer {{accessToken}}` header on protected scenarios where needed. If a request has that header disabled, enable it for the APIM execution or configure bearer authentication at collection level in Postman.

Recommended execution order:

```text
1. Health / Liveness
2. Health / Corporate Health Standard (Function-host only)
3. Health / Readiness
4. Metadata / Employee metadata contract
5. Dynamic Query / Query employees with defaults
6. Dynamic Query / Query employees with filters and paging
7. Dynamic Query / Query employees with multiple filters
8. Dynamic Query negative cases
```

## 13. Swagger / OpenAPI

The canonical OpenAPI contract is:

```text
api/openapi.yaml
```

It includes:

- endpoint descriptions;
- request parameter examples;
- response examples;
- bearer authentication scheme;
- logical contract metadata;
- health `200/503` examples;
- normalized `400/404/503` error examples.

Use this file to import the contract into Swagger Editor, API Management, Postman, Redocly, or another OpenAPI 3.0-compatible tool.

## 14. Security and governance rules

- Read-only API.
- No arbitrary SQL.
- Physical Databricks identifiers are never accepted from consumers.
- Public fields, filters, and sorts are allow-listed.
- SQL values are parameterized.
- Mandatory offboarding eligibility filters are server-side.
- Results use deterministic default sorting.
- Page size is bounded.
- Databricks chunk count is bounded.
- Truncated Databricks result manifests fail closed.
- Dependency failures are normalized.
- `Cache-Control: no-store` is emitted for application responses.
- Correlation identifiers are propagated for support and telemetry.

## 15. Consumer responsibilities

Consumers retrieve the governed offboarding population and execute their own internal business process. The API does not disable accounts, remove permissions, modify ACBS assignments, or execute downstream deprovisioning workflows.