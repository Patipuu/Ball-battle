# Script dùng lại

| Script | Chạy ở | Việc |
|---|---|---|
| `emulator/shot.ps1` | Máy QA | Chụp màn giả lập -> `after/<tên>.png` |
| `emulator/tap-shot.ps1` | Máy QA | 1 thao tác adb (tap / swipe / keyevent / text) rồi chụp |
| `emulator/crop.ps1` | Máy QA | Cắt + phóng to vùng ảnh làm bằng chứng |
| `emulator/type-secret.ps1` | Máy QA | Gõ mật khẩu tài khoản test từ file cục bộ, không in |
| `probe-game-client-version.ps1` | Máy QA | Tìm `versionUrl`, `patch_url`, tham số /version trong client (root) |
| `build-verdict-seed.mjs` | Máy QA | Bảng kết quả kiểm lại -> gói `room-seed.json` + `img/` |
| `apply-verdict-seed.mjs` | VPS | Chạy thử / `--apply` gói kết luận vào Phòng bot qua RPC thật |
| `submit-bug-drafts.mjs` | VPS | Gửi lỗi mới (`drafts.json` + `img/`) thành bản nháp bug của Bot QA, Lead duyệt ở "Nháp bot" |
| `clone-portal-for-new-game.ps1` | Máy dev | Sao portal mẫu sang game mới + liệt kê chỗ phải đổi |

Mặc định (adb, serial, file tài khoản, email Lead / bot, tên miền) là của bộ 3Q - truyền tham số khi dùng cho game khác.

Gửi gói kết luận:
```powershell
node build-verdict-seed.mjs --out <gói> --shots <after> --file "<bảng1.md>|<tài khoản, build>" --file "<bảng2.md>|..."
Copy-Item apply-verdict-seed.mjs <gói>\
scp -r <gói> vps40:C:/app/<slug>/
ssh vps40 "node C:\app\<slug>\<gói>\apply-verdict-seed.mjs C:\app\<slug>\portal.env.json"            # chạy thử
ssh vps40 "node C:\app\<slug>\<gói>\apply-verdict-seed.mjs C:\app\<slug>\portal.env.json --apply"    # user chạy
```
