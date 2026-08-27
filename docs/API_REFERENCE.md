# API Reference

## Base paths

Local Functions host:

```text
http://localhost:7071/api
```

Recommended APIM public path:

```text
/offboarding/v1
```

## Authentication

Production consumers should authenticate at API Management with Microsoft Entra ID OAuth 2.0. APIM should validate tokens and invoke the Function backend using Managed Identity or the approved backend authentication mechanism.

## Correlation

Clients may send:

```http
x-correlation-id: <client-generated-id>
```

If omitted, the service creates one. The correlation identifier is included in normalized responses/telemetry where applicable.

## Dynamic resource query

```http
GET /offboarding/v1/{resource}
```

Example:

```http
GET /offboarding/v1/employees?fields=employeeId,fullName,status&filter=status:eq:ACTIVE&sort=-updatedAt&page=1&pageSize=100&includeTotal=true
```

### Query parameters

| Parameter | Required | Description |
|---|---:|---|
| `fields` | No | Comma-separated public fields. Only selectable whitelist fields are accepted. |
| `filter` | No | `field:operator:value`. Multiple filters may be repeated or separated by `;`. |
| `sort` | No | Public sortable field. Prefix with `-` for descending order. |
| `page` | No | 1-based page number. |
| `pageSize` | No | Requested page size, bounded by each resource definition. |
| `includeTotal` | No | When `true`, performs an additional count query. Use carefully for large datasets. |

### Filter operators

- `eq`
- `ne`
- `gt`
- `gte`
- `lt`
- `lte`
- `contains`

All caller values are sent to Databricks as parameters. They are never embedded directly into SQL.

### Successful response

```json
{
  "data": [
    {
      "employeeId": "12345",
      "fullName": "Example User",
      "status": "ACTIVE"
    }
  ],
  "meta": {
    "resource": "employees",
    "page": 1,
    "pageSize": 100,
    "returned": 1,
    "total": 1,
    "hasMore": false,
    "correlationId": "7e8818a8-5c02-4ac8-a2f5-ec4c2907d314"
  }
}
```

## Resource metadata

```http
GET /offboarding/v1/resources/{resource}/metadata
```

Returns the public API field whitelist and capabilities. It intentionally does not expose physical Databricks view names or columns.

Example response:

```json
{
  "resource": "employees",
  "fields": [
    {
      "name": "employeeId",
      "type": "STRING",
      "filterable": true,
      "sortable": true,
      "selectable": true
    }
  ],
  "maxPageSize": 1000
}
```

## Health

```http
GET /offboarding/v1/health
```

The health endpoint verifies service availability and, depending on implementation/configuration, Databricks dependency reachability.

## Error contract

Expected status mapping:

| HTTP | Code | Meaning |
|---:|---|---|
| 400 | `INVALID_QUERY` | Unsupported field/operator, malformed filter, invalid pagination, or similar caller error. |
| 401 | platform/APIM | Missing or invalid authentication. |
| 403 | platform/APIM | Authenticated caller lacks authorization. |
| 404 | `RESOURCE_NOT_FOUND` | Resource is not registered. |
| 503 | `DEPENDENCY_UNAVAILABLE` | Databricks or another required dependency is unavailable. |
| 500 | `INTERNAL_ERROR` | Unexpected internal failure. |

Example:

```json
{
  "code": "INVALID_QUERY",
  "message": "Field 'password' is not selectable for resource 'employees'.",
  "correlationId": "c1eb4cc7-2d75-4f2d-bcfd-141196c0b700"
}
```

## Contract design rules

- Public names are stable and independent from physical Databricks names.
- Breaking changes require a new API version.
- New optional fields are generally backward-compatible but still require contract review.
- Raw SQL is never accepted.
- Physical catalog/schema/view/column identifiers are never accepted from consumers.
- Bulk integrations should use deterministic cursor/keyset pagination before production-scale use.
