# Postman Functional Tests

English is the default language for all test names, assertions, console messages, and documentation in this repository.

## Files

- `IDBInvest-Offboarding-Databricks-Facade.postman_collection.json`: functional and contract test collection.
- `local.postman_environment.json`: local environment template.

## Coverage

The collection validates:

- health endpoint availability;
- metadata contract;
- dynamic query contract;
- pagination metadata;
- correlation identifiers;
- rejection of unknown fields;
- rejection of page sizes above the configured resource limit;
- unknown resource handling;
- prevention of physical Databricks identifiers leaking through the public contract.

## Run in Postman

1. Start the Azure Functions host locally.
2. Import the collection and local environment.
3. Select `Offboarding Databricks Facade - Local`.
4. If Function authorization is enabled, set `functionKey`.
5. If APIM/Entra authentication is under test, set `accessToken` and enable the Authorization header for the applicable requests.
6. Run the complete collection.

## Run with Newman

Install Newman:

```bash
npm install -g newman
```

Run:

```bash
newman run postman/IDBInvest-Offboarding-Databricks-Facade.postman_collection.json \
  -e postman/local.postman_environment.json \
  --reporters cli,junit \
  --reporter-junit-export TestResults/postman-results.xml
```

For a deployed environment, override `baseUrl` without changing the collection:

```bash
newman run postman/IDBInvest-Offboarding-Databricks-Facade.postman_collection.json \
  --env-var baseUrl=https://<apim-host>/<api-base-path> \
  --env-var accessToken=<token>
```

Do not commit access tokens, Function keys, PATs, or client secrets into Postman environments.
