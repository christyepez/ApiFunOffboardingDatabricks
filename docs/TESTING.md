# Testing Strategy

English is the default language for all automated test names, assertion messages, reports, and testing documentation.

## Test layers

### Unit tests

Unit tests validate deterministic logic without Azure or Databricks dependencies.

Current priority areas:

- resource whitelist enforcement;
- query field selection;
- filter parsing;
- operator mapping;
- parameterization of caller values;
- sorting rules;
- pagination limits;
- count query generation;
- query service pagination behavior;
- metadata mapping;
- normalized application behavior.

Run:

```bash
dotnet test IdbInvest.Offboarding.Databricks.Facade.sln --configuration Release --collect:"XPlat Code Coverage"
```

### Functional/API tests with Postman

The Postman collection validates the externally observable contract:

- health;
- metadata;
- dynamic query response envelope;
- paging metadata;
- invalid field rejection;
- page-size validation;
- unknown resource handling;
- absence of physical Databricks identifiers in public responses.

See `postman/README.md`.

### Integration tests

Integration tests should run against a dedicated DEV Databricks SQL Warehouse and approved test views. They should verify:

- authentication;
- Statement Execution API connectivity;
- parameter serialization;
- asynchronous statement polling;
- row mapping from Databricks arrays into public field dictionaries;
- count queries;
- timeout and provider-error handling.

Integration tests must not use production datasets.

### APIM tests

After deployment to DEV, validate:

- valid Entra token succeeds;
- missing token returns 401;
- wrong role/scope returns 403;
- APIM forwards correlation IDs;
- rate limiting works;
- backend credentials are not exposed;
- direct Function bypass is blocked according to network design.

## Coverage expectations

Recommended baseline:

- Core/Application query logic: >= 85% line coverage;
- security-sensitive query builder branches: >= 90%;
- overall repository: >= 75% while infrastructure adapters are increasingly covered through integration tests.

Coverage percentage is not a substitute for meaningful assertions. Security boundaries and error branches must be explicitly tested.

## Test data

Use synthetic test records. Avoid personal, confidential, or production-derived data in source-controlled fixtures.

## Negative testing

Each public resource should include tests for:

- unknown field;
- non-selectable field;
- non-filterable field;
- non-sortable field;
- invalid operator;
- malformed filter;
- page <= 0;
- pageSize <= 0;
- pageSize above configured maximum;
- unknown resource;
- Databricks unavailable;
- invalid/expired authorization token at APIM.

## CI expectations

Pull request validation should execute:

1. restore;
2. build;
3. unit tests with coverage;
4. static/security analysis as configured;
5. OpenAPI/Postman JSON validation;
6. package publication only after tests pass.

Postman/Newman tests should run after a DEV deployment or against an ephemeral test host where Databricks connectivity is available.

## Definition of done for a new endpoint/resource

A new resource is not complete until unit tests, OpenAPI changes, Postman functional tests, security review, and English documentation are included in the same pull request.
