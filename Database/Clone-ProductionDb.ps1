<#
.SYNOPSIS
    Copies the deployed MySQL database (appsettings.Production.json) into the
    local MySQL database (appsettings.Development.json) - schema and data.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\Database\Clone-ProductionDb.ps1
#>
param(
    [string]$MySqlBin = "C:\Program Files\MySQL\MySQL Server 26.7\bin"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path $PSScriptRoot -Parent

function Get-ConnectionParts([string]$settingsFile) {
    $json = Get-Content (Join-Path $projectRoot $settingsFile) -Raw | ConvertFrom-Json
    $parts = @{}
    foreach ($pair in $json.ConnectionStrings.DefaultConnection.Split(";")) {
        $kv = $pair.Split("=", 2)
        if ($kv.Count -eq 2) { $parts[$kv[0].Trim().ToLower()] = $kv[1].Trim() }
    }
    if (-not $parts["port"]) { $parts["port"] = "3306" }
    return $parts
}

$src = Get-ConnectionParts "appsettings.Production.json"
$dst = Get-ConnectionParts "appsettings.Development.json"

if ($dst["server"] -notin @("localhost", "127.0.0.1")) {
    throw "Target in appsettings.Development.json is '$($dst["server"])', not a local server. Refusing to overwrite it."
}

$dumpFile = Join-Path $env:TEMP "ReactPosDb_production_$(Get-Date -Format yyyyMMdd_HHmmss).sql"

Write-Host "Dumping $($src["database"]) from $($src["server"]) ..."
$env:MYSQL_PWD = $src["password"]
& "$MySqlBin\mysqldump.exe" --host=$($src["server"]) --port=$($src["port"]) --user=$($src["user"]) `
    --single-transaction --routines --triggers --events --no-tablespaces `
    --set-gtid-purged=OFF --column-statistics=0 --default-character-set=utf8mb4 `
    --result-file=$dumpFile $src["database"]
if ($LASTEXITCODE -ne 0) { throw "mysqldump failed (exit code $LASTEXITCODE)" }

Write-Host "Recreating local database $($dst["database"]) ..."
$env:MYSQL_PWD = $dst["password"]
$db = $dst["database"]
& "$MySqlBin\mysql.exe" --host=$($dst["server"]) --port=$($dst["port"]) --user=$($dst["user"]) `
    -e "DROP DATABASE IF EXISTS ``$db``; CREATE DATABASE ``$db`` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"
if ($LASTEXITCODE -ne 0) { throw "Could not recreate local database (exit code $LASTEXITCODE)" }

Write-Host "Importing into local $db ..."
& "$MySqlBin\mysql.exe" --host=$($dst["server"]) --port=$($dst["port"]) --user=$($dst["user"]) `
    --default-character-set=utf8mb4 $db -e "source $($dumpFile -replace '\\', '/')"
if ($LASTEXITCODE -ne 0) { throw "Import failed (exit code $LASTEXITCODE)" }

Remove-Item Env:MYSQL_PWD
Write-Host "Done. Dump kept at $dumpFile"
