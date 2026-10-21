# Deploy server PvP lên Render (miễn phí) + Neon Postgres

Thay cho Oracle (21/10). Server chạy trên Render free (Docker), dữ liệu nằm ở Neon Postgres free.
Cả hai đặt ở **Singapore** cho gần Việt Nam.

## Cần biết trước
- Render free **ngủ sau 15 phút không ai gọi**. Người đầu tiên vào PvP sau đó chờ khoảng 1 phút: game hiện `SERVER ĐANG DẬY, CHỜ 1 PHÚT` và tự thử lại.
- Ổ đĩa của Render bị xoá mỗi lần khởi động lại, vì vậy dữ liệu (tài khoản, MMR, lịch sử, bot) lưu ở Neon: một dòng trong bảng `pvp_state`, tự tạo lần đầu.
- 750 giờ chạy miễn phí mỗi tháng, đủ cho 1 server chạy cả tháng.

## Việc bạn làm (cần tài khoản)
1. **Neon** (https://neon.tech): đăng nhập (có thể bằng GitHub).
   - Create project → Region **AWS Asia Pacific (Singapore)** → Postgres mặc định.
   - Mục *Connection string*: copy chuỗi `postgresql://...neon.tech/neondb?sslmode=require...`. Đây là mật khẩu, đừng gửi công khai.
2. **Render** (https://render.com): đăng nhập bằng GitHub, cho phép Render đọc repo `Patipuu/Ball-battle`.
3. Trên Render chọn **New → Blueprint** → chọn repo `Ball-battle`. Render đọc `render.yaml` ở gốc repo:
   - service `ballbattle-pvp`, Docker, free, Singapore, health `/health`.
   - Render hỏi `DATABASE_URL`: dán chuỗi Neon ở bước 1 → **Apply**.
4. Chờ build xong (lần đầu khoảng 5–10 phút). Render cho địa chỉ dạng `https://ballbattle-pvp.onrender.com` (nếu tên đã có người dùng thì sẽ có đuôi khác). Gửi địa chỉ đó cho Claude.

## Việc Claude làm sau khi có địa chỉ
- Kiểm tra `https://<địa chỉ>/health` và chơi thử một trận PvP với bot.
- Ghi địa chỉ vào `Assets/Resources/pvp-server.txt`, build lại WebGL (itch) và APK.

## Bảo trì
- Mỗi lần push thay đổi trong `PvpServer/` hoặc `Assets/Scripts/Sim/` lên nhánh đã chọn, Render tự build lại.
- Tắt bot: Render → service → Environment → `PVP_BOTS` = `0`.
- Sao lưu: Neon có lịch sử khôi phục (restore) trong vài giờ gần nhất trên gói free. Muốn chắc ăn thì định kỳ chạy `pg_dump` với chuỗi kết nối.
- Chạy local như cũ: không đặt `DATABASE_URL` → dùng file `pvp-data.json`.
