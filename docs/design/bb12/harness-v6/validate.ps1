param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'results-validation'))
$ErrorActionPreference = 'Stop'
$env:BB12_WEAPON_FILTER = ''
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$dll = Join-Path $PSScriptRoot 'bin/Release/net10.0/TreeLabV6.dll'
$cases = @(
    @{Name='root-A'; V=2; P=2; G=2; Start=1; Skill='A3'; Final='-'},
    @{Name='root-B'; V=2; P=2; G=2; Start=1; Skill='B3'; Final='-'},
    @{Name='hive-finale-A'; V=0; P=2; G=1; Start=1; Skill='A3'; Final='A'},
    @{Name='hive-finale-B'; V=0; P=2; G=1; Start=1; Skill='B3'; Final='B'}
)
foreach ($c in $cases) {
    $path = Join-Path $OutputDirectory ($c.Name + '.csv')
    Write-Host "$($c.Name): seeds 2001-2500"
    if ($c.Name -eq 'root-B') {
        $detail = Join-Path $OutputDirectory 'root-B-fights.csv'
        & dotnet $dll 2001 500 $c.V $c.P $c.G $c.Start $c.Skill $c.Final greedy $path $detail | Out-Null
    } else {
        & dotnet $dll 2001 500 $c.V $c.P $c.G $c.Start $c.Skill $c.Final greedy $path | Out-Null
    }
    if ($LASTEXITCODE -ne 0) { throw "Failed: $($c.Name)" }
}
$cases | ForEach-Object { Import-Csv -LiteralPath (Join-Path $OutputDirectory ($_.Name + '.csv')) } |
    Export-Csv -NoTypeInformation -LiteralPath (Join-Path $OutputDirectory 'all-validation.csv')
