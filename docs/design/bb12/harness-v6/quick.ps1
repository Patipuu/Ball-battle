param([int]$FirstSeed = 1301, [int]$SeedCount = 200)
$ErrorActionPreference = 'Stop'
$dll = Join-Path $PSScriptRoot 'bin/Release/net10.0/TreeLabV6.dll'
$out = Join-Path $PSScriptRoot 'quick-results'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$cases = @(
    @{Name='root-A'; V=2; P=2; G=2; Start=1; Skill='A3'; Final='-'},
    @{Name='root-B'; V=2; P=2; G=2; Start=1; Skill='B3'; Final='-'},
    @{Name='finale-A'; V=1; P=2; G=2; Start=0; Skill='A3'; Final='A'},
    @{Name='finale-B'; V=1; P=2; G=2; Start=0; Skill='B3'; Final='B'}
)
foreach ($c in $cases) {
    Write-Host $c.Name
    $path = Join-Path $out ($c.Name + '.csv')
    & dotnet $dll $FirstSeed $SeedCount $c.V $c.P $c.G $c.Start $c.Skill $c.Final greedy $path | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed: $($c.Name)" }
}
