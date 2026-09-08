# Clean Architecture mapping

## Dependency rule

`Functions -> Application -> Core` and `Infrastructure -> Core`. Application never depends on Infrastructure. Dependency injection composes implementations at the Function host.

## Visual Lease baseline retained

The prior Visual Lease facade used separated `src` projects, an isolated worker Function project, a backend-client abstraction and a dedicated test project. This solution preserves that faÃ§ade pattern, but replaces the Visual Lease backend client with an `IDatabricksRepository` and adds a governed dynamic query engine.

## Request flow

1. APIM authenticates and authorizes the consumer.
2. `DynamicQueryController` translates HTTP query parameters into `QueryRequestDto`.
3. `QueryService` resolves the exposed resource and requests a query plan.
4. `QueryBuilder` validates fields/operators/sort against the resource whitelist and emits parameterized SQL.
5. `DatabricksStatementRepository` invokes `/api/2.0/sql/statements`, waits/polls with a bounded timeout, and maps rows to public field names.
6. The Function returns a stable API DTO independent from Databricks response envelopes.

## Security boundaries

- No arbitrary SQL.
- No caller-provided physical identifiers.
- Maximum page size per resource.
- Values parameterized with Databricks named parameter markers.
- Application role/JWT enforcement belongs in APIM and optionally Function EasyAuth/network restrictions.
- Function -> Databricks identity has read-only SQL permissions.
- PAT authentication is local-only.

## Responsibility boundary

The facade owns data exposure only. It identifies and publishes governed offboarding records from Databricks; it does not orchestrate or execute deprovisioning in ACBS, Workiva, PeopleSoft, or any future consumer. Consumer applications own their internal offboarding transactions, retries, state, and audit evidence.

The logical data contract is configuration-driven through esource-definitions.json. model and contractVersion make the public contract independent from physical Databricks objects and from the identity of the consuming system.
