param([Parameter(Mandatory=$true)][string]$Phase,[Parameter(Mandatory=$true)][string]$PreviousTree)
$ErrorActionPreference='Stop'
Set-Location E:/ball-battle
$nightOut='E:/ball-battle-night1-out'
$nightExcludes=Join-Path $nightOut 'tree-excludes.txt'
@('/docs/claude-runs/night1/','/docs/claude-runs/bbnight1.md','/plans/261015-bbnight1/') + @(Get-Content -LiteralPath (Join-Path $nightOut 'preexisting-untracked.txt') | ForEach-Object {'/'+$_}) | Set-Content -LiteralPath $nightExcludes -Encoding utf8
$env:GIT_INDEX_FILE='docs/claude-runs/night1/idx'
try {
    git read-tree $PreviousTree
    if($LASTEXITCODE -ne 0){throw 'read-tree failed'}
    git -c ('core.excludesFile='+$nightExcludes) add -A 2> (Join-Path $nightOut ('phase'+$Phase+'-index.log'))
    if($LASTEXITCODE -ne 0){throw 'alternate-index add failed'}
    $nightPreModified=@(Get-Content -LiteralPath (Join-Path $nightOut 'preexisting-modified.txt'))
    if($nightPreModified.Count -gt 0){git restore --staged --source=b60df20 -- $nightPreModified}
    $nightTree=git write-tree
    if($LASTEXITCODE -ne 0){throw 'write-tree failed'}
} finally { Remove-Item Env:GIT_INDEX_FILE }
$nightForbidden=@(git ls-tree -r --name-only $nightTree | Where-Object {$_ -match '(^|/)(Library|Temp|Builds|Logs|obj)/'})
git diff --binary $PreviousTree $nightTree | Set-Content -LiteralPath ('docs/claude-runs/night1/phase'+$Phase+'.diff') -Encoding utf8
@{Phase=$Phase;Previous=$PreviousTree;Tree=$nightTree;Forbidden=$nightForbidden} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $nightOut ('phase'+$Phase+'-tree.json'))
Copy-Item -LiteralPath (Join-Path $nightOut ('phase'+$Phase+'-tree.json')) -Destination docs/claude-runs/night1/
Write-Output ('T'+$Phase+'='+$nightTree)
git diff --stat $PreviousTree $nightTree
if($nightForbidden.Count -gt 0){Write-Warning ('Forbidden tree paths: '+($nightForbidden -join ', '))}
