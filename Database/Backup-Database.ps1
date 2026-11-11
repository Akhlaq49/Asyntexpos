<#
.SYNOPSIS
    Daily database backup: dumps the database named in an appsettings file to a dated,
    compressed file and deletes backups older than the retention period.

.DESCRIPTION
    Reads the connection string from the given appsettings file (it never edits it).
    mysqldump only reads from the server (--single-transaction), so it is safe to run
    against production while the pharmacy is open.

    Schedule it once a day on the server (Windows Task Scheduler), for example:
      schtasks /Create /SC DAILY /ST 02:00 /TN "Pharmacy DB backup" ^
        /TR "powershell -ExecutionPolicy Bypass -File C:\app\Database\Backup-Database.ps1 -Settings appsettings.Production.json -OutDir D:\Backups"

    On a Linux host use the equivalent cron line with mysqldump | gzip.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\Database\Backup-Database.ps1 -Settings appsettings.Development.json -OutDir D:\Backups -KeepDays 30
#>
param(
    [string]$Settings = "appsettings.Production.json",
    [string]$OutDir = (Join-Path $env:USERPROFILE "PharmacyBackups"),
    [int]$KeepDays = 30,
    [string]$MySqlBin = ""
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path $PSScriptRoot -Parent

if (-not $MySqlBin) {
    $found = Get-ChildItem "C:\Program Files\MySQL" -Directory -ErrorAction SilentlyContinue |
        ForEach-Object { Join-Path $_.FullName "bin\mysqldump.exe" } |
        Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $found) { throw "mysqldump.exe not found. Pass -MySqlBin 'C:\Program Files\MySQL\MySQL Server X\bin'." }
    $MySqlBin = Split-Path $found -Parent
}

$json = Get-Content (Join-Path $projectRoot $Settings) -Raw | ConvertFrom-Json
$parts = @{}
foreach ($pair in $json.ConnectionStrings.DefaultConnection.Split(";")) {
    $kv = $pair.Split("=", 2)
    if ($kv.Count -eq 2) { $parts[$kv[0].Trim().ToLower()] = $kv[1].Trim() }
}
if (-not $parts["port"]) { $parts["port"] = "3306" }

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$sqlFile = Join-Path $OutDir "$($parts["database"])_$stamp.sql"

Write-Host "Backing up $($parts["database"]) from $($parts["server"]) ..."
$env:MYSQL_PWD = $parts["password"]
try {
    & "$MySqlBin\mysqldump.exe" --host=$($parts["server"]) --port=$($parts["port"]) --user=$($parts["user"]) `
        --single-transaction --routines --triggers --no-tablespaces `
        --default-character-set=utf8mb4 --result-file=$sqlFile $parts["database"]
    if ($LASTEXITCODE -ne 0 -and -not (Select-String -Path $sqlFile -Pattern "Dump completed" -Quiet)) {
        throw "mysqldump failed (exit code $LASTEXITCODE)"
    }
}
finally {
    Remove-Item Env:MYSQL_PWD -ErrorAction SilentlyContinue
}

Compress-Archive -Path $sqlFile -DestinationPath "$sqlFile.zip" -Force
Remove-Item $sqlFile
Write-Host "Saved $sqlFile.zip"

$cutoff = (Get-Date).AddDays(-$KeepDays)
Get-ChildItem $OutDir -Filter "$($parts["database"])_*.sql.zip" |
    Where-Object { $_.LastWriteTime -lt $cutoff } |
    ForEach-Object { Remove-Item $_.FullName; Write-Host "Removed old backup $($_.Name)" }
