# Offboarding Integration Architecture

## Authoritative physical reference

The supplied "OFFBOARDING INTEGRATION ARCHITECTURE (VIA APIM IN AZURE)" diagram is the physical architecture baseline for this repository. The implementation must remain synchronized with that design and with the current Confluence architecture assessment.

## Responsibility boundary

Consumer systems (PeopleSoft Facade, SaaS, Legacy and future systems) call the Offboarding Front API published in the existing corporate APIM. The Offboarding Function is a read-only façade: it validates the logical contract, executes governed queries against approved Databricks consumption views, and returns stable JSON. It never performs deprovisioning in consumer systems.

## Physical topology mapping

| Diagram component | Repository / deployment interpretation |
| --- | --- |
| Azure VNet `vnet-[env]-intranet-extend` | Existing corporate VNet; referenced by ID, not created by this solution unless platform ownership changes. |
| APIM subnet `sn-[env]-apim-idbinvest-stv2` | Existing corporate APIM network segment. |
| Function integration subnet `sn-[env]-asp/idbinvest-offboarding` | Outbound VNet integration for the Function App. Exact corporate subnet name/ID is an environment input. |
| Private endpoint subnet `sn-pvt-pe-[env]-idbinvest-offboarding` | Private endpoints for Function, Storage and Key Vault. |
| Corporate APIM RG `rg-[env]-idbinvest` | Existing shared APIM resource group. APIM is not recreated by this solution. |
| Offboarding Front API | APIM API that exposes `/offboarding/v1/...` to authorized consumers. |
| Databricks | Azure Databricks / SQL Warehouse accessed by the Function with a dedicated identity. |
| Offboarding RG `rg-[env]-idbinvest-offboarding` | Resource group that owns the Function and solution-specific dependencies. The `visuallease` label in the reference diagram is treated as a template artifact and is not used by this solution. |
| Function `fn-[env]-idbinvest-offboarding-facade` | .NET 10 isolated Azure Function façade. |
| Key Vault `kv-[env]-idbi-offboarding` | Residual secrets/certificates only; Managed Identity remains preferred. |
| Storage `sapidbioffboarding` / corporate standard | Azure Functions runtime/content dependency only; not application workflow persistence. Exact globally unique name is environment-specific. |
| Application Insights `ai-[env]-idbinvest-offboarding` | Function telemetry and dependency traces. |
| Log Analytics `law-[env]-idbinvest-offboarding` | Centralized logs and diagnostics. |
| Alerts | Azure Monitor alert rules connected to approved Action Groups. |

## Request flow

1. Consumer obtains an Entra ID token and calls the Offboarding Front API in APIM.
2. APIM validates issuer/audience/role, applies throttling and correlation ID, then authenticates to the Function using Managed Identity.
3. EasyAuth protects the Function backend and restricts callers to the approved APIM identity.
4. The Function validates resource/fields/filters/sort/page against the versioned JSON resource definition and mandatory server-side offboarding guardrails.
5. The Function queries Databricks Statement Execution API directly using `DefaultAzureCredential` / the approved workload identity and only approved Unity Catalog views.
6. The Function maps physical columns to the public logical model and returns the response to APIM.
7. APIM returns the read-only offboarding data to the consumer; the consumer owns its internal deprovisioning process.

## Clean Architecture mapping

`Functions -> Application -> Core` and `Infrastructure -> Core`. Application never depends on Infrastructure. Dependency injection composes implementations at the Function host.

- `Core`: public DTOs, resource definitions, interfaces and domain-safe exceptions.
- `Application`: resource registry, mandatory exposure guardrails, validation and safe query planning.
- `Infrastructure`: Databricks authentication/Statement Execution adapter.
- `Functions`: HTTP transport, correlation, health endpoints and middleware.

## Security boundaries

- No arbitrary SQL and no caller-provided physical catalog/schema/view/column names.
- Mandatory server-side eligibility filters cannot be removed by a consumer.
- `employeeId` is the primary logical identifier; email is auxiliary correlation data, never the sole identity key.
- APIM owns consumer authentication/authorization and rate limits.
- APIM -> Function uses Managed Identity + EasyAuth; Function keys are not the primary trust mechanism.
- Function -> Databricks is read-only and uses least-privilege `CAN USE`, `USE CATALOG`, `USE SCHEMA`, `SELECT` grants.
- Private networking is enabled when the approved VNet/subnet/private-DNS IDs are supplied.
- Logs never include SQL bodies, tokens, full dependency error bodies or response payloads.

## Contract independence

`resource-definitions.json` maps the public logical model (`model`, `contractVersion`, aliases and capabilities) to physical Databricks objects. Consumers integrate with the logical contract and never with the physical Databricks schema.
