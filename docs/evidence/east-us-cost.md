# East US cost gate

## Preliminary minimum, 2026-11-26

The user-selected region is **East US** and the requested budget ceiling is **under USD $100/month**. The intended gateway is Application Gateway Standard v2. This is a preliminary lower bound, not an itemized total for the planned architecture.

The [Azure Retail Prices API](https://prices.azure.com/api/retail/prices?%24filter=serviceName%20eq%20%27Application%20Gateway%27%20and%20armRegionName%20eq%20%27eastus%27%20and%20productName%20eq%20%27Application%20Gateway%20Standard%20v2%27%20and%20priceType%20eq%20%27Consumption%27) returned these USD consumption meters for East US on 2026-11-26:

| Meter | Unit price | Assumption | Estimated monthly cost |
| --- | ---: | --- | ---: |
| Standard fixed cost | $0.20/hour | 730 hours running | $146.00 |
| Standard capacity units | $0.008/unit-hour | Usage dependent | Additional |
| PostgreSQL Flexible Server B1MS compute | $0.017/hour | 730 hours; no HA | $12.41 |
| Container Registry Premium registry unit | $1.6666/day | 30 days; one registry | $50.00 |
| Container Apps Standard vCPU active | $0.000024/vCPU-second | One 0.25-vCPU replica active for 730 hours | $15.77 |
| Container Apps Standard memory active | $0.000003/GiB-second | 0.5 GiB active for 730 hours | $3.94 |

The gateway fixed charge **alone exceeds the $100/month ceiling by $46** for a continuously running gateway. The three fixed gateway, PostgreSQL compute and registry meters total approximately **$208.41/month**. With the illustrative continuously active Container Apps replica, the five rows total approximately **$228.12/month** before other charges or any applicable free grants. B1MS, Premium, and Container Apps sizing are an unapproved illustrative scenario, not confirmed template configuration or availability.

The total excludes gateway capacity units, public IP, PostgreSQL storage/backup, Container Apps request charges, networking, registry storage beyond any included allowance, Key Vault, telemetry, taxes, and other usage. Container Apps may bill idle rather than active usage and may qualify for subscription-level free grants; no free grants are assumed above. Azure retail prices are estimates and may differ from actual billed rates or agreements. The API's returned effective dates were 2019-05-09 for gateway, 2021-12-01 for B1MS, and 2018-01-01 for the Premium registry unit; query again before a deployment decision.

On 2026-11-26, the user chose to retain Application Gateway Standard v2 as the design direction despite the overrun. This decision **does not approve a budget increase or provisioning**. No Azure resources have been provisioned. Do not deploy the proposed continuously running gateway until a complete estimate, an explicit budget decision, and separate deployment approval are obtained. An itemized estimate requires finalized SKUs, operating hours, storage, request volume, and monitoring assumptions.
