param([Parameter(Mandatory=$true)][string]$Phase,[ValidateSet('Sim','Edit')][string]$Suite)
$ErrorActionPreference='Stop'
Set-Location E:/ball-battle
$nightOut='E:/ball-battle-night1-out'
$nightTag='phase'+$Phase+'-'+$Suite.ToLowerInvariant()
$env:BB_NIGHT1_OUT=$nightOut
$env:BB_NIGHT1_PHASE=$Phase.Substring(0,1)
$nightWatch=[Diagnostics.Stopwatch]::StartNew()
if($Suite -eq 'Sim') {
    & dotnet test BallBattleUnity/SimTests/SimTests.csproj --no-restore -p:UseSharedCompilation=false --logger ('trx;LogFileName='+$nightTag+'.trx') --results-directory $nightOut --logger 'console;verbosity=normal' *> (Join-Path $nightOut ($nightTag+'.log'))
    $nightExit=$LASTEXITCODE
} else {
    $nightArgs=@('-batchmode','-projectPath','E:/ball-battle/BallBattleUnity','-runTests','-testPlatform','EditMode','-testResults',(Join-Path $nightOut ($nightTag+'.xml')),'-logFile',(Join-Path $nightOut ($nightTag+'.log')))
    $nightProcess=Start-Process -FilePath 'D:/Unity/6000.5.1f1/Editor/Unity.exe' -ArgumentList $nightArgs -WindowStyle Hidden -PassThru -Wait
    $nightExit=$nightProcess.ExitCode
}
@{Exit=$nightExit;Seconds=$nightWatch.Elapsed.TotalSeconds} | ConvertTo-Json -Compress | Set-Content -LiteralPath (Join-Path $nightOut ($nightTag+'.done'))
Get-ChildItem -Path (Join-Path $nightOut ($nightTag+'.*')) | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination E:/ball-battle/docs/claude-runs/night1/ }
Write-Output ($nightTag+' exit='+$nightExit+' seconds='+$nightWatch.Elapsed.TotalSeconds)
exit $nightExit
