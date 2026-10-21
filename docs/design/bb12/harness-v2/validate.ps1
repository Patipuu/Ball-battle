param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'results-validation'))

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$dll = Join-Path $PSScriptRoot 'bin/Release/net10.0/TreeLabV2.dll'
$cases = @(
    @{ Name='root-heavy-A'; Seed=1501; Count=500; V=2; P=2; G=2; Start=1; Skill='A3'; Final='-'; Policy='greedy' },
    @{ Name='root-heavy-B'; Seed=1501; Count=500; V=2; P=2; G=2; Start=1; Skill='B3'; Final='-'; Policy='greedy' },
    @{ Name='volley-finale-A'; Seed=1501; Count=500; V=1; P=2; G=2; Start=0; Skill='A3'; Final='A'; Policy='greedy' },
    @{ Name='volley-finale-B'; Seed=1501; Count=500; V=1; P=2; G=2; Start=0; Skill='B3'; Final='B'; Policy='greedy' },
    @{ Name='root-heavy-A-random'; Seed=1501; Count=200; V=2; P=2; G=2; Start=1; Skill='A3'; Final='-'; Policy='random' }
)
foreach ($c in $cases) {
    $path = Join-Path $OutputDirectory ($c.Name + '.csv')
    Write-Host "$($c.Name): $($c.Count) seeds, $($c.Policy)"
    & dotnet $dll $c.Seed $c.Count $c.V $c.P $c.G $c.Start $c.Skill $c.Final $c.Policy $path | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "TreeLabV2 failed: $($c.Name)" }
}
$cases | ForEach-Object { Import-Csv -LiteralPath (Join-Path $OutputDirectory ($_.Name + '.csv')) } |
    Export-Csv -NoTypeInformation -LiteralPath (Join-Path $OutputDirectory 'all-validation.csv')
