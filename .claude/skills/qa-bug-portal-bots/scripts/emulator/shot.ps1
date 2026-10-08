# Chụp màn hình giả lập Android -> <OutDir>\<Name>.  Ví dụ: & shot.ps1 -Name R1-01-sanh.png
# Mặc định theo bộ 3Q: adb của LDPlayer 14, instance 1 (emulator-5556). KHÔNG dùng serial của dự án khác.
param(
  [Parameter(Mandatory)][string]$Name,
  [string]$OutDir = (Join-Path (Get-Location) 'after'), # ảnh của lượt kiểm, không để trong thư mục skill
  [string]$Adb = 'E:\LDPlayer\LDPlayer14\adb.exe',
  [string]$Serial = 'emulator-5556'
)
New-Item -ItemType Directory -Force $OutDir | Out-Null
$dst = Join-Path $OutDir $Name
& $Adb -s $Serial shell screencap -p /sdcard/qa-shot.png
& $Adb -s $Serial pull /sdcard/qa-shot.png $dst | Out-Null
if (Test-Path $dst) { "OK $dst" } else { "FAIL $dst" }
