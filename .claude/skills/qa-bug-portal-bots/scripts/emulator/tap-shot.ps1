# Làm ĐÚNG 1 thao tác rồi chụp màn (luật 1 thao tác / lần).
#   & tap-shot.ps1 -Act "tap 642 500" -Name R1-02.png [-Wait 3]
#   Act: "tap X Y" | "swipe x1 y1 x2 y2 ms" | "keyevent N" | "text abc"   (cẩn thận: keyevent 4 = Back, có game tắt ngay)
param(
  [string]$Act,
  [Parameter(Mandatory)][string]$Name,
  [int]$Wait = 3,
  [string]$OutDir = (Join-Path (Get-Location) 'after'), # ảnh của lượt kiểm, không để trong thư mục skill
  [string]$Adb = 'E:\LDPlayer\LDPlayer14\adb.exe',
  [string]$Serial = 'emulator-5556'
)
if ($Act) { $p = $Act.Trim().Split(' '); & $Adb -s $Serial shell input @p }
Start-Sleep -Seconds $Wait
& (Join-Path $PSScriptRoot 'shot.ps1') -Name $Name -OutDir $OutDir -Adb $Adb -Serial $Serial
