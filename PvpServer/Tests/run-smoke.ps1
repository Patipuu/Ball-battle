$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$dataDir = Join-Path $env:TEMP ("bb-pvp-http-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $dataDir | Out-Null
$env:PVP_DATA_PATH = Join-Path $dataDir 'data.json'
$server = Start-Process -FilePath 'dotnet' -ArgumentList @('run','--no-build','-c','Release','--project',(Join-Path $repo 'PvpServer\PvpServer.csproj'),'--urls','http://127.0.0.1:5088') -WorkingDirectory $repo -WindowStyle Hidden -PassThru
try {
    $ready = $false
    for ($i = 0; $i -lt 30; $i++) {
        Start-Sleep -Milliseconds 400
        try { $null = Invoke-RestMethod -Uri 'http://127.0.0.1:5088/health'; $ready = $true; break } catch { }
    }
    if (-not $ready) { throw 'PvP server did not start on localhost.' }
    & (Join-Path $PSScriptRoot 'smoke.ps1')
    if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { throw 'PvP smoke test failed.' }
}
finally {
    Stop-Process -Id $server.Id -Force -ErrorAction SilentlyContinue
    $resolved = [System.IO.Path]::GetFullPath($dataDir)
    $tempRoot = [System.IO.Path]::GetFullPath($env:TEMP).TrimEnd('\') + '\'
    if ($resolved.StartsWith($tempRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
