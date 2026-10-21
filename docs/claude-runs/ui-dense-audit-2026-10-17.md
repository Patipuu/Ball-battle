# Rà soát chữ trong UI — 2026-10-17

- Đã kiểm tra 52 trạng thái màn hình ở cả tiếng Anh và tiếng Việt (104 ảnh tại `ui-dense-shots/`), gồm menu, hướng dẫn, chọn bóng, thẻ đầu ván, chuẩn bị trận, rương, sổ tay từng bóng, kết quả, tổng kết, các hộp xác nhận và màn giao đấu.
- Đã đo bằng font pixel thật mọi loại thẻ: thẻ chỉ số, hồi máu, kỹ năng, đổi vũ khí cho từng bóng và nội tại cấp 1/2. Kiểm tra cả chữ nằm trong khung thẻ, khung nút và hộp xác nhận; kiểm tra chồng lấn giữa các dòng.
- Sửa câu mô tả thẻ tốc độ tiếng Việt từ 219 px xuống 194 px (cột rộng 209 px); rút gọn tiêu đề bước 3 của hướng dẫn để nằm trong khung; nới ô chọn bóng thêm 1 px để tên SPARKLER không chạm viền.
- Tính độ rộng theo câu đã dịch trước khi bố trí tiêu đề thẻ, gợi ý trùm và câu hỏi xác nhận. Nút Tỉ lệ của rương được tăng từ 22 px lên 24 px, đúng cỡ chạm tối thiểu của giao diện.

## Kiểm tra

- Toàn bộ Unity EditMode trên dự án gốc sau khi sửa: 575 bài, 550 đạt, 0 lỗi, 25 bỏ qua (`ui-dense-original-full.xml`).
- Chụp ảnh UI riêng: 1 bài đạt; 104 ảnh đã tạo và xem lại các màn nhiều chữ.

Chưa tạo commit.
