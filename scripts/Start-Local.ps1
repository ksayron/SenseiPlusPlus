#requires -Version 7.1
[CmdletBinding()]
param(
    [ValidateSet('Backend', 'Client', 'Migrate', 'Check')]
    [string]$Service = 'Backend',
    [string]$EnvFile = (Join-Path $PSScriptRoot '../.env')
)

$ErrorActionPreference = 'Stop'
$senseiRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not (Test-Path -LiteralPath $EnvFile)) {
    throw 'Create the root .env from .env.example before starting the local app.'
}

# Literal values only: no shell evaluation, interpolation or expansion of secrets.
$senseiLineNumber = 0
foreach ($line in [IO.File]::ReadAllLines([IO.Path]::GetFullPath($EnvFile))) {
    $senseiLineNumber++
    $entry = $line.Trim()
    if (-not $entry -or $entry.StartsWith('#')) { continue }
    if ($entry -notmatch '^([A-Za-z_][A-Za-z0-9_]*)\s*=(.*)$') {
        throw "Invalid .env assignment on line $senseiLineNumber."
    }
    $name = $Matches[1]
    $value = $Matches[2].Trim()
    if ($value.Length -ge 2 -and (($value.StartsWith('"') -and $value.EndsWith('"')) -or
        ($value.StartsWith("'") -and $value.EndsWith("'")))) {
        $value = $value.Substring(1, $value.Length - 2)
    }
    [Environment]::SetEnvironmentVariable($name, $value, 'Process')
}

foreach ($provider in @('Google', 'GitHub')) {
    $enabled = [Environment]::GetEnvironmentVariable("Authentication__External__${provider}__Enabled")
    if ($enabled -and $enabled -notin @('true', 'false')) { throw "$provider Enabled must be true or false." }
    $hasId = -not [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable("Authentication__External__${provider}__ClientId"))
    $hasSecret = -not [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable("Authentication__External__${provider}__ClientSecret"))
    if ($enabled -eq 'true' -and (-not $hasId -or -not $hasSecret)) { throw "Fill $provider ClientId and ClientSecret in .env before enabling it." }
    if ($Service -eq 'Check') { Write-Host "$provider enabled=$($enabled -eq 'true'); ClientId configured=$hasId; ClientSecret configured=$hasSecret" }
}
if ($Service -eq 'Check') { return }

$senseiOrigin = if ($env:Authentication__External__PublicOrigin) { [uri]$env:Authentication__External__PublicOrigin } else { [uri]'https://localhost:5173/' }
if (-not $senseiOrigin.IsAbsoluteUri -or -not $senseiOrigin.IsLoopback -or $senseiOrigin.Scheme -notin @('http', 'https') -or
    $senseiOrigin.AbsolutePath -ne '/' -or $senseiOrigin.UserInfo -or $senseiOrigin.Query -or $senseiOrigin.Fragment) {
    throw 'Local PublicOrigin must be an HTTP(S) loopback origin, for example https://localhost:5173/.'
}
$senseiBackendUrl = if ($env:SENSEI_BACKEND_URL) { $env:SENSEI_BACKEND_URL } else { 'https://localhost:7082' }
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DOTNET_ENVIRONMENT = 'Development'
$env:AgentOAuth__Issuer = $senseiOrigin.AbsoluteUri
$env:AgentOAuth__WebOrigin = $senseiOrigin.AbsoluteUri

Push-Location $senseiRoot
try {
    if ($Service -in @('Backend', 'Migrate')) {
        $senseiConnection = [System.Data.Common.DbConnectionStringBuilder]::new()
        $senseiConnection['Host'] = 'localhost'
        foreach ($pair in @(@('Port', 'SENSEI_POSTGRES_PORT', '5432'), @('Database', 'SENSEI_POSTGRES_DB', 'sensei'),
            @('Username', 'SENSEI_POSTGRES_USER', 'sensei'), @('Password', 'SENSEI_POSTGRES_PASSWORD', 'sensei_local_dev'))) {
            $setting = [Environment]::GetEnvironmentVariable($pair[1])
            $senseiConnection[$pair[0]] = if ([string]::IsNullOrEmpty($setting)) { $pair[2] } else { $setting }
        }
        if (-not $env:ConnectionStrings__Sensei) { $env:ConnectionStrings__Sensei = $senseiConnection.get_ConnectionString() }
        if ($Service -eq 'Migrate') {
            & dotnet tool run dotnet-ef database update --project backend/src/Sensei.Host --startup-project backend/src/Sensei.Host --configuration Release --no-build
        }
        else {
            & dotnet run --project backend/src/Sensei.Host --configuration Release --no-launch-profile --urls $senseiBackendUrl
        }
    }
    else {
        $senseiPinnedNode = Join-Path $senseiRoot '.tmp/node-v24.21.0-win-x64'
        if (Test-Path (Join-Path $senseiPinnedNode 'node.exe')) { $env:PATH = $senseiPinnedNode + [IO.Path]::PathSeparator + $env:PATH }
        $env:SENSEI_API_PROXY_TARGET = $senseiBackendUrl
        if ($senseiOrigin.Scheme -eq 'https') {
            $senseiCertDir = Join-Path $env:LOCALAPPDATA 'Sensei/certs'
            New-Item -ItemType Directory -Force -Path $senseiCertDir | Out-Null
            if (-not $env:SENSEI_WEB_HTTPS_CERT) { $env:SENSEI_WEB_HTTPS_CERT = Join-Path $senseiCertDir 'localhost.pem' }
            if (-not $env:SENSEI_WEB_HTTPS_KEY) { $env:SENSEI_WEB_HTTPS_KEY = Join-Path $senseiCertDir 'localhost.key' }
            if (-not (Test-Path -LiteralPath $env:SENSEI_WEB_HTTPS_CERT) -or -not (Test-Path -LiteralPath $env:SENSEI_WEB_HTTPS_KEY)) {
                & dotnet dev-certs https --export-path $env:SENSEI_WEB_HTTPS_CERT --format PEM --no-password
                if ($LASTEXITCODE -ne 0) { throw 'Could not export the local HTTPS certificate.' }
            }
        }
        else {
            Remove-Item Env:SENSEI_WEB_HTTPS_CERT, Env:SENSEI_WEB_HTTPS_KEY -ErrorAction SilentlyContinue
        }
        Set-Location (Join-Path $senseiRoot 'client')
        & npm run dev -- --host $senseiOrigin.DnsSafeHost --port $senseiOrigin.Port --strictPort
    }
    if ($LASTEXITCODE -ne 0) { throw "$Service exited with code $LASTEXITCODE." }
}
finally { Pop-Location }
