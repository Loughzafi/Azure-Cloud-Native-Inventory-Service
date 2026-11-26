# Runbook: API testing

## Unit tests

Install the .NET 10 SDK. From the repository root, `dotnet test InventoryService.sln` runs the existing unit tests without Docker. The integration project is deliberately not in the solution and does not run implicitly. A missing Docker engine therefore does not prevent unit tests.

## Integration tests

These tests run the real API pipeline in an in-process test host. A test-only RSA issuer signs JWTs, and only the host's bearer validation configuration is replaced with the test issuer's public key; the production authentication and authorization policy remains in use. No real identity provider, credentials, existing database, or application auth bypass is used.

From the repository root:

```powershell
docker info
dotnet test tests\Inventory.Api.IntegrationTests\Inventory.Api.IntegrationTests.csproj
```

Docker must be reachable and able to obtain `postgres:16-alpine` and the Testcontainers resource-reaper image. The test creates a dedicated PostgreSQL container with a randomly assigned port, applies `db\schema.sql`, and disposes its own container and test host; it never connects to or resets the Compose database. Testcontainers also manages its own labeled reaper resource. In exceptional interruption, inspect and remove only resources attributable to the test run; never prune shared Docker resources. A Docker or image-pull failure is a test failure, not a skip. Tests cover JWT rejection and scope/role authorization, CRUD, validation, not-found, and normalized SKU conflicts.

## Local bounded load check

Install k6 separately. Use only a local, isolated development API and disposable/non-sensitive dataset; this read-only request returns up to 500 records to the generator. Do not run against production, shared data, or a target without permission. The script accepts only an HTTP loopback origin, disables redirects, does not persist responses, and makes at most 20 authenticated list requests with one virtual user and a one-second pause. Each request has a five-second timeout; the scenario is limited to two minutes. This is a smoke/load check, not a capacity benchmark.

Set a valid locally acquired bearer token without printing it or placing it in source control or command history. For example, assign `$env:INVENTORY_TOKEN` through a secure local mechanism, then:

```powershell
$env:INVENTORY_BASE_URL = 'http://127.0.0.1:8080'
$env:INVENTORY_ENVIRONMENT = 'local-dev'
k6 run tests\load\inventory-read.js
Remove-Item Env:\INVENTORY_TOKEN
```

The script prints the UTC run time, non-sensitive environment label, workload, completion count, checks and latency summary. A nonzero exit indicates failed thresholds or execution. Treat the output as the actual run result, not a prediction; no results are supplied by this runbook. Do not enable HTTP debugging or pass the token on the command line. Do not override the script's execution options with CLI or environment settings. Expired or insufficiently authorized tokens yield failed checks; renew securely and rerun. Clearing the environment variable does not revoke the token.
