# Developer Guide

## Purpose

This service is an Azure Functions facade that exposes approved Databricks Gold views through a governed REST API. Consumers never send SQL, physical catalog/schema/view names, or physical column names.

English is the default language for source code, comments, commit messages, tests, API descriptions, operational runbooks, and repository documentation.

## Solution structure

```text
src/
  IdbInvest.Offboarding.Databricks.Facade.Core
  IdbInvest.Offboarding.Databricks.Facade.Application
  IdbInvest.Offboarding.Databricks.Facade.Infrastructure
  IdbInvest.Offboarding.Databricks.Facade.Functions
tests/
  IdbInvest.Offboarding.Databricks.Facade.Tests
api/
postman/
deploy/
pipelines/
docs/
```

### Core

Contains DTOs, interfaces, exceptions, and domain-neutral models. It must not depend on Azure Functions, Databricks SDK details, or infrastructure packages.

### Application

Contains use cases and query orchestration. `QueryService` coordinates the resource registry, query builder, and Databricks repository. `QueryBuilder` owns whitelist enforcement and parameterized SQL generation.

### Infrastructure

Contains adapters for Databricks Statement Execution API, authentication/token acquisition, and external service communication.

### Functions

Contains HTTP trigger classes acting as the API controller boundary. Keep them thin: parse HTTP input, call application services, map responses, and delegate exception handling to middleware.

## Adding a new API resource

1. Create or identify a dedicated API-facing Databricks view.
2. Grant the runtime identity only `SELECT` on that view plus required catalog/schema usage permissions.
3. Add a resource definition to `resource-definitions.json`.
4. Map public API field names to physical Databricks columns.
5. Mark each field as selectable/filterable/sortable explicitly.
6. Define a conservative `maxPageSize`.
7. Add unit tests for allowed and denied fields/operators.
8. Add Postman tests for the public contract.
9. Update OpenAPI and API documentation.
10. Deploy to DEV and validate through APIM before promoting.

## Coding rules

- Keep Functions/controllers free of business and query-construction logic.
- Use interfaces across architectural boundaries.
- Never concatenate caller-provided values into SQL.
- Never accept physical Databricks identifiers from an HTTP request.
- Prefer immutable DTOs/records for contracts.
- Use cancellation tokens for external I/O.
- Return normalized API errors and correlation IDs.
- Do not expose Databricks manifests, statement IDs, stack traces, or raw provider errors to consumers.

## Local development

Prerequisites:

- .NET 10 SDK
- Azure Functions Core Tools v4
- Azurite or valid AzureWebJobsStorage
- access to a Databricks test workspace/warehouse when running integration queries

```bash
dotnet restore
dotnet build
dotnet test
cd src/IdbInvest.Offboarding.Databricks.Facade.Functions
func start
```

The default local base URL is `http://localhost:7071/api`.

## Configuration

Use environment variables/Application Settings in Azure. Local secrets belong in `local.settings.json`, which must remain excluded from Git.

Expected configuration categories include:

- Databricks host/workspace URL;
- SQL Warehouse ID;
- authentication mode;
- optional local PAT only for developer testing;
- Application Insights connection string;
- resource registry configuration.

Production should use Managed Identity, workload identity, or Service Principal OAuth rather than a static PAT.

## Pull request definition of done

A change is ready when:

- solution builds without warnings treated as errors where configured;
- unit tests pass;
- new behavior is covered by tests;
- Postman collection is updated for externally visible behavior;
- OpenAPI matches the implementation;
- no secrets are introduced;
- documentation is updated in English;
- security boundaries remain intact.
