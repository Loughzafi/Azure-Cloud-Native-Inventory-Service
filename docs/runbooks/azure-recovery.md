# Azure PostgreSQL recovery runbook (not exercised)

This procedure is a pre-deployment recovery plan. No Azure server or restore has been created or tested. Point-in-time restore creates a **new billable server**; it does not overwrite the source. It requires a separate cost review and explicit authorization, including permission to create the target and remove the test target afterward.

## Preparation

1. Identify the exact source subscription, resource group, server, region, owner, and incident scope. Verify access and record the time the incident began without publishing sensitive identifiers.
2. Confirm automated backup retention, earliest available restore time, and the desired UTC recovery point. Agree the allowable downtime and potential data loss with the owner. A restore point is not itself proof of a recovery time or data-loss outcome.
3. Check quota, expected restore-server charges, available private network/DNS, administrator access, and TLS validation. Choose a unique restore-server name and verify it does not already exist. Never overwrite or delete the source.
4. Approve an isolated validation plan and define how to verify application data without exposing private data or credentials in logs or evidence.

## Restore and validate

1. Start a timer at the agreed incident/recovery start point. Use the current [Microsoft point-in-time recovery documentation](https://learn.microsoft.com/en-us/azure/postgresql/backup-restore/concepts-backup-restore) and Azure portal or CLI to restore the source to a **new** server at the agreed UTC time.
2. Wait for the restore operation and server readiness. From an authorized private network location, validate DNS, certificate-verified TLS, access rights, schema, and selected data integrity checks.
3. Compare known committed markers or independent records against the recovery point. Record what was restored or missing and the last confirmed committed transaction time. Do not equate the selected restore point with measured data loss.
4. Record the time at which the agreed service-level verification succeeds and the elapsed duration. Note whether it includes provisioning, application reconnection, DNS changes, and human approval. This is the observed result for that exercise, not a general Azure RTO guarantee.
5. Do not redirect a live application to the restored server without a separately approved cutover and rollback plan.

## Cleanup and evidence

After confirming no needed data will be lost, obtain approval to delete the **specific new test server** and associated test-only resources. Check dependencies and retain the original server and backups. Record the operation outcome, configuration, workload size, times, data-loss observation, costs, and limitations; scrub identifiers and secrets. Never publish dumps or raw logs containing customer data.

Azure Flexible Server automatic backups support point-in-time recovery within the configured retention period. On-demand backups are not supported for the Burstable tier in the current Microsoft documentation. The local `pg_dump` exercise in [local-recovery.md](local-recovery.md) is a separate mechanism and does not validate Azure-managed backups.
