# Tìm máy chủ cập nhật (versionUrl) + tham số thật của client game Cocos2d-x trên giả lập có root (LDPlayer). Chỉ đọc.
#   & probe-game-client-version.ps1 -Package com.kuromon.tamquoc3q [-Serial emulator-5556]
# In ra: các thư mục patch, khoá trong res/version.plist (versionUrl, versionUrlBackup, noticeUrl, cdnHost...),
# đầu version.diff (app_version, patch, patch_url), và URL /version đầy đủ tìm thấy trong log của game.
# Sau đó thử: Invoke-WebRequest "<versionUrl>?<tham số>&patch=<min_patch>" -> phải ra JSON có patch + files.
param(
  [Parameter(Mandatory)][string]$Package,
  [string]$Adb = 'E:\LDPlayer\LDPlayer14\adb.exe',
  [string]$Serial = 'emulator-5556'
)
$sh = @"
cd /data/data/$Package/files 2>/dev/null || { echo 'NO_FILES_DIR'; exit 0; }
echo '== patch dirs'; ls patch 2>/dev/null | tail -n 12
P=`$(ls patch 2>/dev/null | sort -n | tail -n 1)
echo "== newest patch: `$P"
echo '== version.plist keys'; grep -a -o -E '<key>[^<]+</key>|<string>[^<]+</string>' patch/`$P/res/version.plist 2>/dev/null | head -n 30
echo '== version.diff head'; head -c 300 patch/`$P/version.diff 2>/dev/null; echo
echo '== version URLs seen in logs'; grep -a -h -o -E 'https?://[^ "]*version\?[^ ")]+' *.log 2>/dev/null | sort -u | tail -n 5
"@
$tmp = Join-Path $env:TEMP 'qa-probe-version.sh'
[IO.File]::WriteAllText($tmp, $sh.Replace("`r`n", "`n"))
& $Adb -s $Serial push $tmp /sdcard/qa-probe-version.sh | Out-Null
& $Adb -s $Serial shell "su -c 'sh /sdcard/qa-probe-version.sh'"
"== logcat (version URL)"
& $Adb -s $Serial logcat -d 2>$null | Select-String -Pattern 'https?://\S*version\?\S+' | Select-Object -Last 3 | ForEach-Object { $_.Matches[0].Value }
