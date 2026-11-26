# Runbook: Azure deployment readiness

## Status

**Blocked: no Azure infrastructure templates or deployment have been implemented.** Do not treat this document as a deployment procedure or run resource-creation commands. It records the gates that must be completed before the first deployment.

As of 2026-11-26, no Bicep modules have been generated or validated and no Azure deployment has been attempted. A PostgreSQL Flexible Server schema lookup returned an unsupported API version error; this must be resolved or verified against authoritative resource documentation before implementation.

East US is the chosen region. Application Gateway is the chosen public entry point; the intended backend is an internal Container Apps environment and a private PostgreSQL Flexible Server. These are decisions, not evidence that the combined design is deployable or that its cost has been accepted.

## Implementation blockers and required inputs

- Verify PostgreSQL Flexible Server and other selected resource schemas and required properties before generating Bicep. Obtain a local Bicep compiler to build the resulting modules; review warnings as well as errors.
- Confirm resource group, subscription and naming/tagging inputs, address spaces and non-overlapping subnet sizes, existing networks and peering, private DNS ownership, and East US SKU/feature availability and quota. Avoid collisions with existing networks and resources.
- Confirm gateway tier, capacity, WAF choice, public DNS name, TLS certificate source and rotation plan. Validate the gateway-to-internal-Container-Apps topology, backend DNS, host header, HTTPS/SNI, certificate trust and probe behavior. Internal ingress alone does **not** prevent other reachable network clients from bypassing the gateway; define and validate supported network controls that allow the gateway while denying other paths, including peered networks. If those controls cannot be demonstrated, do not claim the boundary is satisfied.
- Choose PostgreSQL version, compute/storage tier, backup retention and high-availability settings. Validate delegated subnet/private DNS and disable public access. Define a private, TLS-verified connection, database/schema initialization, separate least-privilege application login, administrator access and credential rotation. The local `db/schema.sql` is initialization, not a migration or recovery procedure.
- Define a secure bootstrap for the database credential and Key Vault secret, including how authorized operators supply and rotate them without checking in plaintext, emitting them as outputs, or exposing them in logs. Specify Key Vault network access, secret name and a dedicated workload managed identity with only the required secret-read and ACR pull permissions. Plan role-assignment propagation and private DNS/egress for registry, Key Vault and identity endpoints.
- Supply an existing, appropriately authorized Entra authority and API audience; configure `Authentication__Authority` and `Authentication__Audience`. Map the Key Vault reference to `ConnectionStrings__Postgres`. The application's anonymous `/health/live` and `/health/ready` endpoints differ: readiness requires PostgreSQL. Confirm container image/tag, port 8080, scaling and resource limits.
- Select Log Analytics retention, diagnostic categories, alert recipients and spend controls. The application currently writes JSON console logs; Application Insights traces and metrics are not established merely by creating an Azure resource and require separate instrumentation and verification.
- Agree recovery objectives, restore testing, data retention and a non-destructive change/rollback process. Bicep incremental deployment does not by itself guarantee that every resource update preserves data; review replacements, deletions and shared-resource effects explicitly.

## Pricing inputs and approval

Prepare an East US itemized estimate before any provisioning. Include at least Application Gateway tier and capacity units/hours, public IP, Container Apps compute and minimum replicas, PostgreSQL compute/storage/backup and HA, ACR tier/storage, Key Vault operations, Log Analytics ingestion/retention, Application Insights and alerts, data transfer and any private endpoints or other network services. Record expected hours, usage, SKU choices, assumptions, taxes and uncertainty; obtain current regional prices rather than treating an estimate as a quote.

### Unapproved pricing baseline

The following is a concrete **pricing scenario**, not a user-approved SKU selection, template configuration, complete estimate, or availability guarantee. It assumes East US, USD retail pricing, 730 hours and 30 days of continuous operation, no reservations, no tax, and no account-specific discounts:

| Component | SKU and sizing to price | Qualification |
| --- | --- | --- |
| Application Gateway | `Standard_v2`, autoscale minimum 0, maximum 2 | No WAF; compare `WAF_v2` if WAF is required. Fixed charge applies even at minimum 0; capacity units and public IP are additional. |
| Public IP | `Standard`, static IPv4, one address | Zone behavior and regional availability still need confirmation. |
| Container Apps | Workload profiles environment, `Consumption` profile; one app, 0.25 vCPU, 0.5 GiB, minimum 1 and maximum 2 replicas | Price compute based on active/idle time, replicas, requests and applicable subscription free grants. Internal VNet-integrated environment; no dedicated workload profile assumed. |
| PostgreSQL Flexible Server | `Burstable`, `Standard_B1ms` (B1MS), PostgreSQL 16, 32 GiB storage, 7-day backup retention, locally redundant backup, HA disabled | Demo assumption only: burstable does not support HA; capacity, storage, backup consumption and version/region availability need verification. |
| Container Registry | `Premium`, one registry, no geo-replication; one private endpoint | Premium is necessary for ACR private endpoints; account for included and excess storage, endpoint and DNS costs. Confirm private image-pull connectivity. |
| Key Vault | `Standard`, RBAC authorization, one vault and one private endpoint | Price secret operations and endpoint/DNS costs; certificate lifecycle and gateway identity permissions require design. |
| Monitoring | Log Analytics `Pay-as-you-go` (`PerGB2018`), 30-day analytics retention; workspace-based Application Insights | Price ingestion, retention beyond included allowances, alert rules and notifications; application telemetry instrumentation is separate. |
| Private network and DNS | VNet with gateway, Container Apps, PostgreSQL and private-endpoint subnets; private DNS zones/links and two private endpoints | Price endpoint-hours, processed data, DNS queries/zones and transfer. Confirm subnet sizing, NSG and egress requirements. |

Public [Azure Retail Prices API](https://prices.azure.com/api/retail/prices) responses queried on 2026-11-26 for `eastus`, USD, consumption showed `Application Gateway Standard v2` fixed cost **$0.20/hour**, PostgreSQL Flexible Server Burstable BS Series `B1MS` **$0.017/hour**, and Container Registry `Premium Registry Unit` **$1.6666/day**. For the assumptions above those three meters alone are approximately **$208.41/month** ($146.00 + $12.41 + $50.00), before capacity units, storage, Container Apps, public IP, networking, monitoring, other charges and taxes. This is a partial retail illustration, not a quotation or full estimate; actual billing and contracted prices can differ. The API also returned Standard v2 capacity units at $0.008/unit-hour, charged according to actual or reserved capacity. Recheck all rates and terms in the Azure pricing calculator before a decision.

The partial illustration already exceeds the under-$100/month target. Obtain a budget decision **and a separate explicit deployment approval** before provisioning. Pricing and local validation are not approval to create or modify cloud resources.

Useful design references: [Application Gateway and internal Container Apps](https://learn.microsoft.com/en-us/azure/container-apps/waf-app-gateway), [Container Apps ingress IP restrictions](https://learn.microsoft.com/en-us/azure/container-apps/ip-restrictions), [Container Apps workload profiles](https://learn.microsoft.com/en-us/azure/container-apps/workload-profiles-overview), [PostgreSQL high availability](https://learn.microsoft.com/en-us/azure/postgresql/high-availability/how-to-configure-high-availability), and [ACR SKUs](https://learn.microsoft.com/en-us/azure/container-registry/container-registry-skus). Container Apps allow-list restrictions are a candidate control, not proof of gateway-only access: validate the actual source IP seen by ingress, behavior across network paths and the inability to spoof or bypass the intended boundary.

## Local and pre-deployment validation

After schemas are verified and modules are implemented:

1. Run `bicep build` on each deployable entry point, record compiler version and results, and resolve or document warnings. Inspect parameters, resource dependencies, naming rules, secure values, role scopes and outputs; never output secrets.
2. Verify templates specify internal Container Apps ingress, private-only PostgreSQL, private DNS and managed identities. Review the gateway-only access controls and their platform compatibility; static build alone cannot prove effective isolation or connectivity.
3. Before any future deployment, separately review the approved resource scope and run validation/what-if only when authorized; neither substitutes for testing the actual network boundary.
4. Following a separately approved deployment, test gateway access and attempt bypass from other reachable network locations; test DNS, TLS, database public-access denial, image pull, Key Vault secret resolution, valid/invalid JWTs, probes and telemetry. Record sanitized results and limitations. These checks have not been performed.

## Required gates

1. Confirm supported East US SKUs for Application Gateway, Container Apps, PostgreSQL Flexible Server, ACR, Key Vault, and monitoring.
2. Complete the itemized monthly estimate and compare it with the requested under-$100/month ceiling. The partial retail illustration above exceeds the ceiling; obtain a new budget decision before deploying.
3. Implement Bicep and validate a fresh deployment without embedding credentials or tenant-specific identifiers in checked-in files. A fresh deployment requires the separate approval described above.
4. Confirm the network design: public TLS terminates at the selected gateway; the API backend is restricted to that gateway; PostgreSQL is not publicly reachable; DNS, database TLS, and required outbound paths work.
5. Configure dedicated managed identity permissions for image pull and Key Vault reads. Map the PostgreSQL secret to the application's expected configuration key.
6. Configure Entra token authority/audience and scope/role assignments, health probes, resource limits, scaling, diagnostics, alerts, backup, and budget notifications.
7. Review the teardown procedure and expected data loss before creating resources.

## First-deployment verification (after the gates pass)

- Run Bicep validation and what-if before applying the deployment.
- Verify the API is reachable only through the intended public entry point.
- Verify a valid authorized request succeeds and missing/invalid identity is denied.
- Verify the database has no public ingress and the API can connect over its private path.
- Verify managed identity secret retrieval, diagnostic telemetry, health probes, and actionable alert delivery.
- Record environment, date, configuration, observed results, cost, and limitations in `docs/evidence/`. Remove tenant identifiers and sensitive values.

## Rollback and teardown

No Azure rollback or teardown command is documented until the infrastructure resource graph and data-retention behavior are known. Before the first deployment, document dependency order, database backup/export, expected data loss, retained shared resources, and a reviewed teardown command. Never delete a resource group or shared resource without verifying its exact scope and dependencies.

## Incident, backup, and restore

Cloud incident triage and backup/restore are untested. Before production-shaped use, add service-specific alert response steps, backup retention and restore procedures, and a timed recovery exercise with observed RTO and data-loss outcome. Do not report targets as measured results.
