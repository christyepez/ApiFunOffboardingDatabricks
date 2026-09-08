# Architecture Synchronization Matrix

This matrix proves alignment between the authoritative **OFFBOARDING INTEGRATION ARCHITECTURE (VIA APIM IN AZURE)** diagram and the implementation in this repository.

| Architecture element | Expected role | Repository / deployment evidence | Status |
| --- | --- | --- | --- |
| Consumer systems: PeopleSoft Facade / SaaS / Legacy | Consume offboarding data and perform their own internal deprovisioning | OpenAPI + APIM policy + responsibility boundary in `docs/ARCHITECTURE.md` | Aligned |
| Corporate APIM in `rg-[env]-idbinvest` | Gateway, JWT validation, throttling, correlation, backend routing | `deploy/apim/inbound-policy.xml`; APIM is referenced as existing corporate infrastructure | Aligned |
| Offboarding Front API | Versioned read-only API exposed to consumers | `/offboarding/v1/{resource}`, metadata and health routes in Function/OpenAPI/Postman | Aligned |
| Azure Function `fn-[env]-idbinvest-offboarding-facade` | Facade/query engine and anti-corruption layer | .NET 10 isolated Functions projects; Bicep parameter samples use the diagram naming | Aligned |
| Azure Databricks / SQL Warehouse | Source of approved offboarding consumption data | `DatabricksStatementRepository`, `DefaultAzureCredential`, Statement Execution API | Aligned |
| Logical exposure model | Prevent physical Databricks coupling | `resource-definitions.json` with `model`, `contractVersion`, aliases and whitelists | Aligned |
| HR offboarding eligibility | Restrict exposed population | Mandatory `status=OFFBOARDED` plus 30-day `terminationDate` lookback enforced by query planner | Aligned |
| VNet `vnet-[env]-intranet-extend` | Corporate network boundary | Environment-owned resource referenced by subnet IDs; not hardcoded | Aligned |
| APIM subnet `sn-[env]-apim-idbinvest-stv2` | APIM network segment | Existing corporate subnet; documented, not recreated | Aligned |
| Function integration subnet | Function outbound VNet integration | `vnetIntegrationSubnetId` parameter in Bicep | Aligned |
| Private endpoint subnet `sn-pvt-pe-[env]-idbinvest-offboarding` | Private endpoints | `privateEndpointSubnetId` + Function/Storage/Key Vault PE resources | Aligned |
| Key Vault `kv-[env]-idbi-offboarding` | Residual secrets/certificates | Bicep + configuration guidance; Managed Identity preferred | Aligned |
| Storage `sapidbioffboarding` / environment-specific equivalent | Azure Functions runtime/content dependency only | Bicep host/content storage; no business/workflow persistence | Aligned |
| Application Insights `ai-[env]-idbinvest-offboarding` | Function telemetry | Bicep + Application Insights worker telemetry | Aligned |
| Log Analytics `law-[env]-idbinvest-offboarding` | Central logs | Bicep workspace + diagnostic settings | Aligned |
| Alerts | Operational monitoring | Bicep HTTP 5xx metric alert and optional Action Group IDs | Aligned |
| Correlation | End-to-end request traceability | APIM `x-correlation-id`, Function RequestContext, response metadata | Aligned |
| Read-only boundary | No consumer deprovisioning performed by this solution | No ProcessEngine, ACBS writer, command endpoint, workflow state or deprovisioning adapter exists | Aligned |

## Naming note

The reference diagram contains `rg-[env]-idbinvest-visuallease` in the solution-resource area. For this repository that label is treated as a diagram/template artifact. Offboarding-owned resources use the Offboarding naming convention (`rg-[env]-idbinvest-offboarding` or the approved environment-specific equivalent). The Visual Lease solution is reference-only and is not modified by this project.

## Databricks path

The Function executes the approved Databricks SQL Statement Execution API using its dedicated workload identity. The APIM `Databricks API` label shown in the diagram is not interpreted as a mandatory additional Function-to-APIM hop; the current Confluence architecture states that the Function executes the Databricks Statement Execution API/SQL Warehouse using a dedicated service identity.