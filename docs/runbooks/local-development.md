# Runbook: local development

## Prerequisites

- .NET 10 SDK
- Docker Compose and a running Docker Engine
- A Microsoft Entra app registration configured for the API's delegated `inventory.access` scope and/or `Inventory.ReadWrite` app role

## Start

From the repository root in PowerShell:

```powershell
docker info
dotnet publish src\Inventory.Api\Inventory.Api.csproj --configuration Release --output publish /p:UseAppHost=false
$env:AUTHORITY = "https://login.microsoftonline.com/<tenant-id>/v2.0"
$env:API_AUDIENCE = "api://<api-application-id>"
docker compose up --build -d --wait
```

Replace placeholders locally. Do not commit tenant IDs, client secrets, access tokens, or real database credentials. The example database password and default Compose authority/audience are for local development only. The API container connects to PostgreSQL over the Compose network.

## Verify

- `GET http://localhost:8080/health/live` should return HTTP 200 while the container is running.
- `GET http://localhost:8080/health/ready` should return HTTP 200 when PostgreSQL is reachable and HTTP 503 when it is not.
- Inventory routes should return HTTP 401 without a token and require the configured scope or role with a token.
- `dotnet test InventoryService.sln` runs the focused validation unit tests.
- API integration and bounded local load tests are separate opt-in workflows; see [API testing](api-testing.md). Unit tests do not require Docker.
- `docker compose ps` should show the PostgreSQL service as healthy and the API service as running.
- The API process runs as the non-root UID supplied by the official ASP.NET runtime image.

## Troubleshoot

- If `docker info` cannot connect to the Docker Engine, start Docker Desktop / the configured engine, then retry. Compose configuration validation alone does not mean PostgreSQL is running.
- If readiness is 503, inspect the PostgreSQL container status with `docker compose ps` and logs with `docker compose logs postgres`. Verify the local connection string and that the schema initialized.
- If the API exits at startup, check logs with `docker compose logs api` and verify the configured connection string, HTTPS `AUTHORITY`, and `API_AUDIENCE`.
- If an inventory request returns 401/403, verify token issuer, audience, expiry, and scope/role claims. Do not log or publish the token.

## Stop and reset

Stop containers while preserving local database contents:

```powershell
docker compose down
```

Only if the local database contents can be discarded, remove this project's Compose volume:

```powershell
docker compose down --volumes
```

The volume removal permanently deletes local database data. This does not affect Azure resources. The generated `publish/` directory can be removed after the containers are stopped; it is ignored by Git.
