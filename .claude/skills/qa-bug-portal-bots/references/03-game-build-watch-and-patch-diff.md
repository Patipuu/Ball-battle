# 03. Build game tự động + "dev đã đổi gì" theo mảng

## Tìm máy chủ cập nhật của game (client Cocos2d-x có cập nhật nóng)

Trên giả lập (root của LDPlayer: `adb shell "su -c '<lệnh>'"`; lệnh phức tạp: đẩy file .sh lên `/sdcard` rồi `su -c 'sh /sdcard/x.sh'`):
- `/data/data/<package>/files/patch/<N>/res/version.plist` -> khoá `versionUrl`, `versionUrlBackup`, `noticeUrl`, `cdnHostBackup`.
- `/data/data/<package>/files/patch/<N>/version.diff` -> JSON `{app_version, patch, patch_url, files:[{name,size,md5,patch}]}`.
- Tham số thật client gửi: tìm trong log của game (3Q: `files/tj_debug.log`) hoặc logcat: ví dụ `.../version?arch=x64&plat=android&app=3.1.0.0&min_patch=191&patch=438&channel=sgo87&tag=and01`.
- `scripts/probe-game-client-version.ps1 -Package <pkg>` làm các bước trên.

Gọi thiếu tham số thường 404. Với đủ tham số, máy chủ trả patch mới nhất + mọi file có `patch` > tham số `patch` gửi lên (mỗi file chỉ ghi lần đổi CUỐI).

## CDN

- File của patch N: `<patch_url><N>/<đường dẫn>` (3Q: `https://cdn.3qdc.wanmeivn.com:8443/patch/sgo/442/src/config/skill.lua`).
- CDN chỉ giữ file ở ĐÚNG patch nó đổi (429/431/432 không có, 430 có). Bản trước của file = patch gần nhất < N có file đó.
- Lua trên CDN là chữ thường (không mã hoá) ở 3Q; đầu file có `@desc` (tiếng Trung) và dev có ghi chú "3Q: ..." tiếng Việt ngay chỗ sửa (lý do sửa - nguồn tóm tắt tốt nhất).

## Bộ theo dõi (`game-build-watch.js`)

- Mỗi 5 phút (server VPS, chặn chạy chồng): GET `versionUrl&patch=<đã biết>`. Patch mới -> khai build `<app>.<patch>` (như Lead khai), lưu `game_patches` (file từng patch), cập nhật chỉ mục `game_files`, phân tích, đăng tin hệ thống vào phòng bot (mảng + 1 câu).
- Lần đầu: bắt đầu từ build Lead khai gần nhất, ghi bù các patch sau đó; sau lượt đầu nạp **chỉ mục đầy đủ** (`patch=min_patch`, ~130 KB, 1 lần, `game_index_full=1`) -> từ đó biết bản trước của mọi file không cần dò; file chưa từng có = file gốc trong APK.
- Dò CDN (khi chưa có chỉ mục): HEAD lùi tối đa 120 patch / file, **ngân sách 300 HEAD / patch / lượt**; hết ngân sách -> `skip_reason: probe_budget`, lượt sau làm tiếp phần thiếu (giữ phần đã xong). Lỗi tải -> `unreachable`, thử tối đa 3 lần. File > 6 MB -> `too_large` (đo: so 2,9 MB = 63 ms CPU). Patch > 40 file text -> `max_files`.
- `portal.env.json` `GAME_VERSION_URL`: đổi máy chủ / `off`.

## So code và tóm tắt

- `game-change-diff.js summarizeChange`: so đếm dòng (không phụ thuộc thứ tự), ngữ cảnh = hàm Lua gần nhất / bản ghi csv2lua `[id] = {` + `name/txt/title/desc`; chữ tiếng Việt mới; cặp bản dịch `["中文"] = "cũ" -> "mới"`; ghi chú dev mới (dòng `--` có chữ Việt). File chưa có bản cũ: chỉ giữ ghi chú dev.
- `game-change-summary.js summarizeAreas`: mỗi mảng tối đa 2 câu, ưu tiên ghi chú "3Q:" (vế sau "⇒"), rồi "Đổi chữ "A" thành "B"" / "Sửa chữ: "..." và N chỗ khác", "Chỉnh số liệu: <tên>", câu chung. Bỏ mã màu `#C0xRRGGBB#`, `#F20#`, tên biến, toạ độ. Đây là quy tắc, không phải AI; muốn câu tự nhiên hơn thì cần API Claude trên server (user quyết vì chi phí + gửi code ra ngoài).

## Bảng mảng (`game-areas.js`)

- Dựng từ dữ liệu của chính game: 3Q dùng `src/config/unlock.lua` (`feature` -> `name` tiếng Việt, ví dụ `arena` = "Diễn Võ Đường", `lover` = "Kim Ốc", `explorer` = "Thần khí"), chữ dịch `l10n/vn.lua`, chú thích đầu file. Không đoán.
- `PREFIX` (tiền tố đường dẫn dài nhất thắng, khớp cả `<prefix>_...`) cho `src/`; `KEYWORDS` cho `res/` (ảnh, uijson, spine). File không khớp -> "Khác (<thư mục>)".
- Kiểm phủ: chạy `areaOf` trên toàn bộ danh sách file (chỉ mục đầy đủ) tới khi 0 file "Khác". 3Q: 1068 file -> 0.
- Phát hiện phụ: thư mục `plans/` của dev (script .py, .tsv) lọt lên CDN công khai - map riêng "File nội bộ của dev (lọt lên CDN)" để thấy và báo dev.

## Dùng code game để chỉ chỗ cho bot QA

Khi agent "không kiểm được vì không biết màn ở đâu", tải file liên quan từ CDN và đọc: ví dụ màn Điểm danh 3Q chỉ tự bật lần đăng nhập đầu ngày (`CityView:checkSignIn`), bong bóng thoại Kim Ốc chỉ hiện khi tặng quà (`onUseItem -> onShowSpeek`), tên vật phẩm theo id trong `config/items.lua`.
