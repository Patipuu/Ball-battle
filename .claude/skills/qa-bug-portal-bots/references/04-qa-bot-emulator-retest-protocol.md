# 04. Bot QA kiểm lại bug trên giả lập (LDPlayer + adb)

## Chuẩn bị

- LDPlayer: 1 instance riêng cho QA (3Q: instance 1 = `emulator-5556`). Instance khác có thể của dự án khác: KHÔNG đụng, KHÔNG `adb kill-server`, không bật / tắt instance.
- adb của LDPlayer (3Q: `E:\LDPlayer\LDPlayer14\adb.exe`), màn 1280x720.
- RAM: đo trước và giữa lượt; dưới 0,7 GB thì dừng (giả lập bị tắt ngang khi RAM thấp). Thủ phạm hay gặp: Edge nhiều tab.
- Tài khoản test + mật khẩu: file cục bộ gitignored (3Q: `qa/fixtures/qa-test-accounts.local.md`, dòng `| tên | mật khẩu |`). Gõ bằng `scripts/emulator/type-secret.ps1` (đọc dòng, `input text`, không in). Đăng nhập hỏng -> dừng, hỏi user (không thử biến thể - bộ phân loại an toàn cũng chặn).
- Script: `scripts/emulator/` (shot, tap-shot, crop, type-secret). Ảnh lưu `after/`, tiền tố theo lượt (`R1-`, `R2-`...).

## Phân nhóm trước khi giao (đỡ tốn lượt)

| Nhóm | Ví dụ | Xử lý |
|---|---|---|
| Sửa nằm trong APK, máy đang APK cũ | Back Android, màn tải cập nhật | Cần APK mới -> ghi chú |
| Cần 2 máy cùng lúc | Kết bạn 2 chiều | Ghi chú |
| Cần GM / dữ liệu đặc biệt | Hòm thư đầy, bao lì xì, vật phẩm hiếm | Nhờ Lead xin GM |
| Cần tài khoản mới tinh | Bước hướng dẫn tân thủ | Xin tài khoản mới |
| Màn chỉ hiện theo điều kiện | Điểm danh lần đầu trong ngày | Hẹn thời điểm |
| Kiểm được ngay | Chữ, giao diện, cấu hình | Giao agent theo tài khoản |

## Lượt agent

- Mỗi lượt 1 tài khoản, ~10-20 bug, chạy nền (một giả lập -> các lượt nối tiếp, không song song).
- Lời giao việc: `assets/retest-agent-prompt-template.md` (môi trường, đăng nhập, luật cứng, danh sách bug: mã | lỗi | mong đợi | chỗ xem, đầu ra).
- Luật cứng: 1 thao tác / lần + chụp; đóng popup bằng X (3Q: phím Back tắt game ngay); cấm Bán / Phân giải / Rời bang / Nạp / tiêu tiền tệ cao cấp / phiếu / đổi cài đặt, skin; cho phép rõ ràng từng thứ được tiêu (thể lực, quà miễn phí, tối đa N quà để kiểm thoại...); ghi build ở màn đăng nhập; mỗi kết luận = ảnh đúng chỗ + zoom + chép chữ; không chắc = KHONG_KIEM_DUOC.
- Mất mạng ("Lỗi mạng" > 2 lần / nút im): khởi động lại game 1 lần, lặp lại thì dừng.
- Cập nhật game: force-stop + start (nút "khởi động lại" trong game có thể không tải bản mới).

## Controller (phiên chính) sau mỗi lượt

1. **Mở 2-3 ảnh zoom** của kết luận Đạt quan trọng và ảnh của mọi kết luận "một phần / cần PO" - không tin lời agent suông (từng có agent bịa).
2. Mọi kết luận Không đạt / một phần: so ảnh với dòng **Kết quả mong đợi** của phiếu (đọc phiếu trên portal), không theo tên phiếu. 08/10 agent cho L-036 "Không đạt" vì hiểu tên "ngắt dòng giữa chữ và số" quá rộng; mong đợi thật là "không cắt giữa chữ hay số" và đã đạt - Lead phát hiện từ ảnh.
3. Kết luận phán xét (ví dụ hình thú trong hiệu ứng có phải Pokémon không, chữ chạm viền có tính là đạt không) -> ghi chú chờ Lead / PO, không tự kết luận.
4. Gom gói kết luận (02), user `--apply`, Lead duyệt. Kiểm ảnh đầu tiên của từng kết luận (thẻ duyệt chỉ hiện 3 ảnh đầu).
5. Lỗi mới thấy -> tra code / cấu hình game trên CDN tìm gốc (file + id) trước khi đề xuất bản nháp bug.
6. Báo tài nguyên đã tiêu.

## Bảng kết quả (bắt buộc đúng cột để script đọc)

```
| Mã | Kết luận | Build | Ảnh | Đã thấy |
| L-067 | DA_SUA | 3.1.0.0.445 | R1-L067z-lucchien.png, R1-VT-03-boiduong.png | Bồi dưỡng: nhãn cạnh số "332703" ghi "LỰC CHIẾN". |
```
