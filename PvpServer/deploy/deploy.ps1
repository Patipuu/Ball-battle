<#
  Publish the Ball Battle PvP server and install or update it on an Oracle Cloud Always Free Ubuntu VM.

  First time:   .\PvpServer\deploy\deploy.ps1 -Ip 1.2.3.4 -Key C:\Users\you\.ssh\oracle.key -WriteClientUrl
  Update later: .\PvpServer\deploy\deploy.ps1 -Ip 1.2.3.4 -Key C:\Users\you\.ssh\oracle.key

  -Domain      your own domain (A record -> the VM IP); default is <ip-with-dashes>.sslip.io, no account needed
  -User        SSH user of the image (Ubuntu images: ubuntu)
  -WriteClientUrl  also write the HTTPS URL into BallBattleUnity/Assets/Resources/pvp-server.txt for the next build
#>
param(
    [Parameter(Mandatory = $true)][string]$Ip,
    [Parameter(Mandatory = $true)][string]$Key,
    [string]$Domain = '',
    [string]$User = 'ubuntu',
    [switch]$WriteClientUrl
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$target = "$User@$Ip"
$ssh = @('-i', $Key, '-o', 'StrictHostKeyChecking=accept-new', '-o', 'ConnectTimeout=15')

# OpenSSH on Windows refuses a private key other users can read (a downloaded key usually is).
& icacls $Key /inheritance:r /grant:r "$($env:USERNAME):R" | Out-Null

Write-Host '== Checking the VM'
$arch = (& ssh @ssh $target 'uname -m').Trim()
$rid = switch ($arch) { 'aarch64' { 'linux-arm64' } 'x86_64' { 'linux-x64' } default { throw "Unsupported VM architecture '$arch'." } }
Write-Host "   $arch -> $rid"

Write-Host '== Publishing (self-contained, no .NET install needed on the VM)'
$out = Join-Path $repo "Builds\pvp-publish\$rid"
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
& dotnet publish (Join-Path $repo 'PvpServer\PvpServer.csproj') -c Release -r $rid --self-contained true `
    -p:InvariantGlobalization=true -p:DebugType=none -o $out -nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

Write-Host '== Uploading'
& ssh @ssh $target 'rm -rf ~/bbpvp-upload && mkdir -p ~/bbpvp-upload'
if ($LASTEXITCODE -ne 0) { throw 'ssh failed.' }
& scp @ssh -q -r $out "${target}:~/bbpvp-upload/"
if ($LASTEXITCODE -ne 0) { throw 'scp of the app failed.' }
& scp @ssh -q (Join-Path $PSScriptRoot 'setup.sh') "${target}:~/bbpvp-upload/setup.sh"
if ($LASTEXITCODE -ne 0) { throw 'scp of setup.sh failed.' }

Write-Host '== Installing on the VM (takes a minute the first time)'
$ErrorActionPreference = 'Continue'   # apt and caddy write progress to stderr; judge by the exit code instead
$log = & ssh @ssh $target "mv ~/bbpvp-upload/$rid ~/bbpvp-upload/app && sed -i 's/\r$//' ~/bbpvp-upload/setup.sh && sudo bash ~/bbpvp-upload/setup.sh $Domain" 2>&1
$code = $LASTEXITCODE
$ErrorActionPreference = 'Stop'
$log | ForEach-Object { Write-Host "   $_" }
if ($code -ne 0) { throw 'Setup on the VM failed (see above).' }
$url = ($log | Where-Object { "$_" -like 'PVP_URL=*' } | Select-Object -Last 1) -replace '^PVP_URL=', ''
if (-not $url) { throw 'Setup did not report the URL.' }

Write-Host "== Checking $url/health from here (the first certificate can take ~30 s)"
$ok = $false
for ($i = 0; $i -lt 20; $i++) {
    try { $h = Invoke-RestMethod "$url/health" -TimeoutSec 10; if ($h.status -eq 'ok') { $ok = $true; break } } catch { Start-Sleep 3 }
}
if (-not $ok) {
    Write-Warning "No HTTPS answer yet. Check the VCN security list allows TCP 80 and 443 (see DEPLOY-ORACLE.md, step 4)."
    exit 1
}
Write-Host "PASS: $url is live."

if ($WriteClientUrl) {
    $file = Join-Path $repo 'BallBattleUnity\Assets\Resources\pvp-server.txt'
    [IO.File]::WriteAllText($file, $url)
    Write-Host "Wrote $url to $file - rebuild WebGL and Android to use it."
}
