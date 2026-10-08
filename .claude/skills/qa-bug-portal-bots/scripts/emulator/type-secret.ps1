# Gõ mật khẩu tài khoản test vào ô đang chọn trên giả lập, KHÔNG in ra màn / log.
# File tài khoản cục bộ (gitignored), dòng dạng markdown: | <tài khoản> | <mật khẩu> | ...
#   & type-secret.ps1 -Account tester12
# Tắt "con mắt" (ẩn mật khẩu) trước khi gõ để ảnh chụp không lộ mật khẩu. Đăng nhập hỏng: dừng, hỏi user, không thử biến thể.
param(
  [Parameter(Mandatory)][string]$Account,
  [string]$Fixture = 'C:\Users\Admin\Documents\ChatGPT\3Q đại chiến\qa\fixtures\qa-test-accounts.local.md',
  [string]$Adb = 'E:\LDPlayer\LDPlayer14\adb.exe',
  [string]$Serial = 'emulator-5556'
)
$line = Get-Content $Fixture -Encoding UTF8 | Where-Object { $_ -match "^\|\s*$([regex]::Escape($Account))\s*\|" } | Select-Object -First 1
if (-not $line) { 'NO_ACCOUNT'; exit 1 }
$pw = ($line.Split('|')[2]).Trim()
if (-not $pw) { 'NO_PW'; exit 1 }
& $Adb -s $Serial shell input text $pw
"typed len=$($pw.Length)"
