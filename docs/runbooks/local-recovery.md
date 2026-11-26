# Local PostgreSQL backup and restore exercise

This exercise uses two new, isolated PostgreSQL containers with temporary in-memory data directories and synthetic records. It does not use or modify the Compose database volume. It requires a running Docker Engine and access to `postgres:16-alpine`.

From the repository root in PowerShell:

```powershell
.\tests\recovery\Exercise-LocalRestore.ps1
```

The script applies the checked-in schema, inserts one record, takes a custom-format `pg_dump`, and inserts one more record after the backup. It then stops the source, restores the backup into the already-running target, and checks that the first record exists and the later record is absent. It reports elapsed time from source stop through verified restore, the number of records recovered, and the number of post-backup records lost.

The script uses unique container names, temporary storage, a generated throwaway password, and no published ports. It removes only the containers and backup file it created. If cleanup reports a warning, inspect and remove only the named exercise container. The result is a local demonstration, **not an Azure RTO or a time-based RPO**, and excludes creating a new recovery server.

For real data, follow a separately reviewed Azure backup, restore, access-control, and retention procedure. Do not copy production data into this exercise or publish backup files.
