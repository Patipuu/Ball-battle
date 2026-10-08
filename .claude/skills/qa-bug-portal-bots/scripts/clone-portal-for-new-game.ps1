# Sao portal mẫu (3Q) sang thư mục mới cho game khác: bỏ dữ liệu / bí mật / run / node_modules / .git, tạo git mới,
# rồi in danh sách file còn chữ của game cũ để rà (đổi theo references/01-portal-template-and-new-game-setup.md).
#   & clone-portal-for-new-game.ps1 -Target C:\Users\Admin\Documents\ChatGPT\<slug>-bug-portal -Slug <slug>
param(
  [Parameter(Mandatory)][string]$Target,
  [Parameter(Mandatory)][string]$Slug,
  [string]$Source = 'C:\Users\Admin\Documents\ChatGPT\3q-bug-portal',
  [string[]]$OldWords = @('3Q', 'Đại Chiến', 'wanmeivn', 'tamquoc', 'kuromon', '3q-bug-portal', 'Bạc', 'Nguyên Bảo')
)
if (Test-Path $Target) { throw "Đã có $Target - chọn thư mục mới" }
$skip = @('run', 'node_modules', '.git', '.wrangler', '.dist-cf', '.dist-cloud', 'supabase\.temp', '.impeccable\review', '.impeccable\mocks')
$skipFiles = @('SECRETS.local.txt', '*.log', 'session-secret*.txt', 'google-client-id.txt', 'bot-qa-mcp-token.txt')
robocopy $Source $Target /E /XD ($skip | ForEach-Object { Join-Path $Source $_ }) /XF $skipFiles /NFL /NDL /NJH /NJS /NP | Out-Null
Set-Location $Target
git init -q; git add -A; git commit -q -m "chore: start $Slug bug portal from the 3Q portal template"
"Đã sao vào $Target (git mới). Việc tiếp theo:"
"  1. Tạo SECRETS.local.txt (LEAD_CODE=..., GOOGLE_CLIENT_ID=...)"
"  2. Đổi phần riêng của game (references/01): seed test case / tuyến, game-areas.js, GAME_VERSION_URL, DEFECT_RULES, tên game"
"  3. npm install; npm run test:d1; npm run test:mcp"
"  4. deploy-vps.ps1 -RemoteRoot C:\app\$Slug -ServiceName $Slug -AppPort <cổng trống>"
"== File còn chữ của game cũ (rà từng chỗ):"
Get-ChildItem -Recurse -File -Include *.js, *.mjs, *.html, *.css, *.ps1, *.sql, *.json, *.md, *.toml |
  Where-Object { $_.FullName -notmatch '\\node_modules\\' } |
  ForEach-Object { $f = $_; $n = ($OldWords | ForEach-Object { (Select-String -Path $f.FullName -Pattern ([regex]::Escape($_)) -Encoding utf8).Count } | Measure-Object -Sum).Sum; if ($n) { [pscustomobject]@{ File = $f.FullName.Replace("$Target\", ''); Hits = $n } } } |
  Sort-Object Hits -Descending | Format-Table -AutoSize | Out-String -Width 200
