# 01. Portal mẫu và dựng cho game mới

## Kiến trúc (repo mẫu `3q-bug-portal`, mốc b02fc99)

- **Front end:** HTML + JS thuần, không build (`public/`). Mỗi trang 1 file `*-page.js`, dùng chung `portal-shared-helpers.js` (nav, lỗi tiếng Việt, định dạng) và `portal-session.js` (ai được vào trang nào).
- **API:** các module `cloudflare/*-rpc.js` gom vào `bug-portal-api-core.js` (`ALL_RPC`). Quyền từng RPC ở `rpc-access-rules.js` (public / session / self / read / dev / lead / room).
- **Server VPS:** `server/vps-node-server.mjs` (Node 24): phục vụ `public/`, RPC tại `/sb/rest/v1/rpc/<tên>`, ảnh `/sb/storage/v1/object/...`, MCP `/mcp`. DB = `node:sqlite` qua `d1-sqlite-adapter.mjs` (giả API D1: prepare/bind/all/first/run/batch, hỗ trợ `?1`).
- **DB:** `cloudflare/d1-bug-portal-schema.sql` chạy mỗi lần dịch vụ khởi động (`create table if not exists`). Cột thêm sau: `LATE_COLUMNS` trong `server/vps-database-setup.mjs` (bảng đã có trên prod không tự thêm cột!). Bảng mới phải thêm vào CUỐI `cloudflare/backup-tables.js`.
- **Vai trò:** tester (QA, slot QA-A..E), dev, viewer, lead. Tester chỉ thấy bug của mình (trừ Patch note: cả team đọc; và bot QA `.bot`: đọc cả team).
- **Đăng nhập:** Google Sign-In (Client ID của Lead), phiên cookie; mã Lead (hash sha256 trong `portal.env.json`) là đường cứu hộ.

## Phần riêng của game (phải đổi khi sang game mới)

| Chỗ | Đổi gì |
|---|---|
| `public/*.html` (title, brand), `portal-styles.css`, `DESIGN.md`, `PRODUCT.md` | Tên game, màu / logo nếu muốn |
| `cloudflare/d1-seed-test-cases.sql`, `d1-seed-tours.sql` | Test case + tuyến khám phá của game mới (giữ cột; `module` nên trùng tên mảng game) |
| `public/js/guide-data.js`, `guide.html` | Hướng dẫn QA cho game mới |
| `cloudflare/game-areas.js` | Bảng đường dẫn file client -> tên mảng (dựng từ dữ liệu game, xem 03) |
| `cloudflare/game-build-watch.js` (`GAME_VERSION_URL` mặc định, `MIN_PATCH`) hoặc `portal.env.json` `GAME_VERSION_URL` (`off` = tắt) | Máy chủ cập nhật của game mới |
| `cloudflare/bot-room-rpc.js` `DEFECT_RULES` | Kiểu lỗi hay gặp của game mới (regex + câu hỏi cho dev) |
| `cloudflare/mcp-gateway.js` `INSTRUCTIONS`, `SERVER_INFO` | Tên game trong hướng dẫn cho Claude |
| `deploy-vps.ps1` tham số | `-RemoteRoot C:\app\<slug> -ServiceName <slug> -AppPort <cổng>`; tên package trong bundle |
| `SECRETS.local.txt` (không commit) | `LEAD_CODE`, `GOOGLE_CLIENT_ID` của game mới |
| Thuật ngữ tiếng Việt trong luật phân tích (Bạc / Nguyên Bảo / Đồng...) | Theo thuật ngữ game mới |

`scripts/clone-portal-for-new-game.ps1` sao repo và in đủ danh sách file có chữ của game cũ để rà.

## Deploy

```powershell
npm run test:d1; npm run test:mcp            # phải ALL PASS
powershell -ExecutionPolicy Bypass -File .\deploy-vps.ps1 -LocalTest   # dựng bản thử run\vps-bundle, chạy:
node run\vps-bundle\server\vps-node-server.mjs run\vps-local\portal.env.json   # http://localhost:3510
powershell -ExecutionPolicy Bypass -File .\deploy-vps.ps1 -RemoteRoot C:\app\<slug> -ServiceName <slug> -AppPort <cổng>
```
- Deploy chỉ thay mã, giữ `data\`. Sau deploy kiểm chỉ đọc: trang trả 200, bảng mới có (`vps-admin-cli.mjs` op `sql`, chỉ `select`).
- Tên miền: `C:\caddy\them-site.ps1 -TenMien <tên> -Cong <cổng>` (chạy `-ThuKhong` trước; chỉ sau khi DNS trỏ đúng). Google Cloud: chỉ tài khoản của Lead, thêm origin https.
- Sao lưu tự động 7 ngày / lần (`server/vps-backup.mjs`, giữ 8 bản). Trước mỗi lần ghi prod bằng tay: `vacuum into 'C:/app/<slug>/data/backups/pre-<việc>-<yymmdd-hhmm>.sqlite'`.

## SQL chỉ đọc trên prod

```powershell
function Q($sql) { $b64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes((@{op='sql'; sql=$sql} | ConvertTo-Json -Compress)))
  ssh vps40 "node C:\app\<slug>\app\server\vps-admin-cli.mjs C:\app\<slug>\portal.env.json $b64" }
Q "select status, count(*) n from bugs group by status"
```
Lệnh nhiều dòng / có ngoặc kép gửi qua ssh: dùng `powershell -EncodedCommand <base64 UTF-16>` (ngoặc kép bị nuốt nếu gửi thẳng).

## Test

- `npm run test:d1`: RPC thật trên SQLite trong bộ nhớ (quyền, vòng đời bug, patch note, build tự động với phản hồi giả).
- `npm run test:mcp`: cổng MCP + phòng bot (token, vai trò, `.bot`, kết luận, giới hạn tin).
- Chụp giao diện: `run/capture-bot-room.mjs`, `run/capture-patch-notes.mjs` (Playwright + Edge, bản chạy thử 3510).
- Đo tải: `run/measure-vps-load.mjs` trên bản sao DB prod ở cổng khác (xoá bản sao sau khi đo).
