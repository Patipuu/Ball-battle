<#
  Build both PvP clients from the current repo, using the server URL in BallBattleUnity/Assets/Resources/pvp-server.txt:
    Builds/BallBattle-webgl-itch.zip   (upload to itch.io as the HTML5 game)
    Builds/Android/BallBattle.apk      (upload to itch.io as the Android download)

  .\PvpServer\deploy\build-clients.ps1            # both
  .\PvpServer\deploy\build-clients.ps1 -Only apk  # or -Only webgl

  The project's own editor builds WebGL. The APK needs an editor with Android Build Support: the project's editor if that
  module is installed, otherwise another installed 6000.x editor that has it, on a copy of the project in D:\bb-android (so
  the main project's Library is not reimported for Android). The Android key is keys\ballbattle.keystore (back it up: every
  future APK update must be signed with the same key, or players have to uninstall first).
#>
param([ValidateSet('both', 'apk', 'webgl')][string]$Only = 'both', [string]$UnityRoot = 'D:\Unity', [string]$AndroidCopy = 'D:\bb-android',
      [string]$AndroidEditor = '')
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$project = Join-Path $repo 'BallBattleUnity'
$version = ((Get-Content (Join-Path $project 'ProjectSettings\ProjectVersion.txt') | Select-String '^m_EditorVersion:').Line -split ':\s*')[1].Trim()
$url = (Get-Content (Join-Path $project 'Assets\Resources\pvp-server.txt') -Raw).Trim()
Write-Host "Project editor $version, PvP server $url"
if ($url -notlike 'https://*') { Write-Warning 'pvp-server.txt is not an HTTPS URL: PvP will only work on this PC. Run deploy.ps1 -WriteClientUrl first.' }

function Run-Unity($exe, $path, $method, $log, [string[]]$extra = @()) {
    $argv = @('-batchmode', '-quit', '-projectPath', $path, '-executeMethod', "BallBattle.EditorTools.BuildScript.$method", '-logFile', $log) + $extra
    $p = Start-Process $exe -ArgumentList $argv -PassThru -WindowStyle Hidden
    $p.WaitForExit()
    $line = Select-String $log -Pattern '\[BuildScript\]' | Select-Object -Last 1
    if ($line) { Write-Host "   $($line.Line)" }
    if ($p.ExitCode -ne 0) { throw "$method failed (exit $($p.ExitCode)); see $log" }
}

if ($Only -ne 'apk') {
    Write-Host '== WebGL for itch.io'
    Run-Unity "$UnityRoot\$version\Editor\Unity.exe" $project 'BuildWebGLItch' (Join-Path $repo 'Builds\webgl-itch-build.log')
}

if ($Only -ne 'webgl') {
    Write-Host '== Android APK'
    $editor = $null
    if (Test-Path "$UnityRoot\$version\Editor\Data\PlaybackEngines\AndroidPlayer\SDK") { $editor = $version; $path = $project }
    else {
        # Prefer an editor of the same 6000.x minor as the project (smallest version jump), then the newest.
        $minor = ($version -split '\.')[0..1] -join '.'
        $candidates = @(Get-ChildItem -LiteralPath $UnityRoot -Directory | ForEach-Object { $_.Name } |
            Where-Object { $_ -like '6000.*' -and (Test-Path -LiteralPath (Join-Path $UnityRoot "$_\Editor\Data\PlaybackEngines\AndroidPlayer\SDK")) })
        Write-Host "   editors with Android: $($candidates -join ', ')"
        $editor = if ($AndroidEditor) { $AndroidEditor } else {
            @($candidates | Where-Object { $_ -like "$minor.*" }) + @($candidates | Sort-Object -Descending) | Select-Object -First 1 }
        if (-not $editor) { throw 'No installed Unity 6000.x editor has Android Build Support. Add it in Unity Hub (Installs > ... > Add modules > Android).' }
        $path = Join-Path $AndroidCopy 'BallBattleUnity'
        Write-Host "   $version has no Android module; building with $editor on a copy in $path"
        foreach ($d in 'Assets', 'Packages', 'ProjectSettings') { robocopy (Join-Path $project $d) (Join-Path $path $d) /MIR /NFL /NDL /NJH /NJS /NP | Out-Null }
        $m = Join-Path $path 'Packages\manifest.json'
        $j = Get-Content $m -Raw | ConvertFrom-Json
        $j.dependencies.PSObject.Properties.Remove('com.coplaydev.unity-mcp')   # editor-only tooling, not needed to build
        $j | ConvertTo-Json -Depth 5 | Set-Content $m
        Remove-Item (Join-Path $path 'Packages\packages-lock.json') -ErrorAction SilentlyContinue
    }
    $keys = Join-Path $repo 'keys'
    if (Test-Path "$keys\ballbattle.keystore") {
        $env:BB_KEYSTORE = "$keys\ballbattle.keystore"
        $env:BB_KEYSTORE_PASS = (Get-Content "$keys\keystore-pass.txt" -Raw).Trim()
    }
    try { Run-Unity "$UnityRoot\$editor\Editor\Unity.exe" $path 'BuildAndroid' (Join-Path $repo 'Builds\android-build.log') @('-buildTarget', 'Android', '-bbBuildOut', (Join-Path $repo 'Builds\Android')) }
    finally { Remove-Item Env:BB_KEYSTORE_PASS -ErrorAction SilentlyContinue }
}
Write-Host 'Done.'
