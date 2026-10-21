param(
    [int]$FirstSeed = 1301,
    [int]$SeedCount = 200,
    [ValidateSet('greedy', 'random')][string]$Policy = 'greedy',
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'results'),
    [string]$WeaponFilter = ''
)

$ErrorActionPreference = 'Stop'
$env:BB12_WEAPON_FILTER = $WeaponFilter
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$dll = Join-Path $PSScriptRoot 'bin/Release/net10.0/TreeLabV6.dll'
if (-not (Test-Path -LiteralPath $dll)) { throw "Build TreeLabV6.csproj first" }
$all = [System.Collections.Generic.List[object]]::new()
$buildIndex = 0
foreach ($v in 0..2) {
    foreach ($p in 0..2) {
        foreach ($g in 0..2) {
            $tier1 = $v + $p + $g
            foreach ($start in 0..1) {
                foreach ($skill in @('L1', 'A2', 'B2', 'A3', 'B3')) {
                    $skill2 = $skill -ne 'L1'
                    $skill3 = $skill.EndsWith('3')
                    if ($start -eq 1 -and ($g -lt 1 -or $tier1 -lt 2)) { continue }
                    if ($skill2 -and ($g -lt 1 -or $tier1 -lt 2)) { continue }
                    $lower = $tier1 + 2 * $start + [int]$skill2
                    if ($skill3 -and $lower -lt 5) { continue }
                    foreach ($final in @('-', 'A', 'B')) {
                        if ($final -ne '-' -and ($g -lt 1 -or -not $skill3 -or $skill[0] -ne $final[0])) { continue }
                        $spent = $lower + [int]$skill3 + 3 * [int]($final -ne '-')
                        if ($spent -ne 10) { continue }
                        $buildIndex++
                        $name = ('b{0:d2}_v{1}p{2}g{3}s{4}{5}{6}' -f $buildIndex, $v, $p, $g, $start, $skill, $final)
                        $path = Join-Path $OutputDirectory ($name + '.csv')
                        Write-Host "$buildIndex/18 $name seed=$FirstSeed count=$SeedCount policy=$Policy"
                        & dotnet $dll $FirstSeed $SeedCount $v $p $g $start $skill $final $Policy $path | Out-Null
                        if ($LASTEXITCODE -ne 0) { throw "TreeLabV2 failed: $name" }
                        Import-Csv -LiteralPath $path | ForEach-Object { $all.Add($_) }
                    }
                }
            }
        }
    }
}
if ($buildIndex -ne 18) { throw "Expected 18 LV10 builds, found $buildIndex" }
$all | Export-Csv -NoTypeInformation -LiteralPath (Join-Path $OutputDirectory 'all-lv10.csv')
Write-Host "Saved $($all.Count) rows to $(Join-Path $OutputDirectory 'all-lv10.csv')"
