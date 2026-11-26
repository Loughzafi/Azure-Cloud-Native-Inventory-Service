$ErrorActionPreference = 'Stop'
$runId = [guid]::NewGuid().ToString('N')
$sourceName = "inventory-recovery-source-$runId"
$restoreName = "inventory-recovery-restore-$runId"
$tempDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "inventory-recovery-$runId"
$backupPath = Join-Path $tempDirectory 'inventory.dump'
$schemaPath = Join-Path $PSScriptRoot '..\..\db\schema.sql'
$createdContainers = [System.Collections.Generic.List[string]]::new()
$password = [guid]::NewGuid().ToString('N')

function Invoke-Docker {
    & docker @args
    if ($LASTEXITCODE -ne 0) {
        throw "Docker command failed with exit code $LASTEXITCODE."
    }
}

function Wait-Postgres([string] $container) {
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        & docker exec $container pg_isready -U inventory_app -d inventory 2>$null | Out-Null
        if ($LASTEXITCODE -eq 0) { return }
        Start-Sleep -Seconds 1
    }
    throw "PostgreSQL did not become ready in container $container."
}

try {
    New-Item -ItemType Directory -Path $tempDirectory | Out-Null
    foreach ($container in @($sourceName, $restoreName)) {
        $existing = & docker ps -aq --filter "name=^/$container$"
        if ($LASTEXITCODE -ne 0) { throw 'Unable to inspect Docker containers.' }
        if ($existing) { throw "Container name already exists: $container" }
        Invoke-Docker run --detach --name $container --network none --tmpfs /var/lib/postgresql/data `
            --env POSTGRES_USER=inventory_app --env POSTGRES_DB=inventory `
            --env "POSTGRES_PASSWORD=$password" postgres:16-alpine | Out-Null
        $createdContainers.Add($container)
        Wait-Postgres $container
    }

    Invoke-Docker cp $schemaPath "${sourceName}:/tmp/schema.sql"
    Invoke-Docker exec $sourceName psql -U inventory_app -d inventory -v ON_ERROR_STOP=1 -f /tmp/schema.sql | Out-Null
    Invoke-Docker exec $sourceName psql -U inventory_app -d inventory -v ON_ERROR_STOP=1 -c `
        "INSERT INTO inventory_items (id, sku, name, quantity) VALUES ('11111111-1111-4111-8111-111111111111', 'backup-item', 'Before backup', 7);" | Out-Null
    Invoke-Docker exec $sourceName pg_dump -U inventory_app -d inventory -Fc -f /tmp/inventory.dump
    Invoke-Docker cp "${sourceName}:/tmp/inventory.dump" $backupPath

    Invoke-Docker exec $sourceName psql -U inventory_app -d inventory -v ON_ERROR_STOP=1 -c `
        "INSERT INTO inventory_items (id, sku, name, quantity) VALUES ('22222222-2222-4222-8222-222222222222', 'after-backup', 'After backup', 3);" | Out-Null

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    Invoke-Docker stop $sourceName | Out-Null
    Invoke-Docker cp $backupPath "${restoreName}:/tmp/inventory.dump"
    Invoke-Docker exec $restoreName pg_restore -U inventory_app -d inventory --exit-on-error /tmp/inventory.dump
    $actual = @(& docker exec $restoreName psql -U inventory_app -d inventory -At -v ON_ERROR_STOP=1 -c `
        "SELECT sku || ':' || quantity FROM inventory_items ORDER BY sku;")
    if ($LASTEXITCODE -ne 0) { throw 'Failed to verify restored data.' }
    $stopwatch.Stop()
    if (@($actual).Count -ne 1 -or $actual[0] -ne 'backup-item:7') {
        throw "Unexpected restored data: $($actual -join ', ')"
    }

    [pscustomobject]@{
        dateUtc = [DateTimeOffset]::UtcNow.ToString('o')
        environment = 'Docker Desktop; two isolated PostgreSQL 16-alpine containers; synthetic data'
        measurement = 'source stop through verified restore to an already-running target'
        elapsedSeconds = [Math]::Round($stopwatch.Elapsed.TotalSeconds, 2)
        backupRowsRestored = 1
        postBackupRowsLost = 1
        limitation = 'Not an Azure recovery time or time-based RPO; does not include provisioning a target.'
    } | ConvertTo-Json
}
finally {
    foreach ($container in $createdContainers) {
        & docker rm --force $container | Out-Null
        if ($LASTEXITCODE -ne 0) { Write-Warning "Unable to remove test container $container." }
    }
    if (Test-Path -LiteralPath $backupPath) { Remove-Item -LiteralPath $backupPath -Force }
    if (Test-Path -LiteralPath $tempDirectory) { Remove-Item -LiteralPath $tempDirectory -Force }
}
