# 05. Sự cố đã gặp và cách đã sửa (3Q, 30/09 - 08/10/2026)

| Sự cố | Gốc | Cách xử lý / luật |
|---|---|---|
| Agent kiểm lại "bịa" kết luận | Không bắt buộc bằng chứng | Mỗi kết luận phải ảnh + zoom + chữ trên màn; controller tự mở ảnh đối chiếu |
| Agent bấm nhầm tiêu phiếu / tài nguyên | Nhiều thao tác liền, không nhìn màn | 1 thao tác / lần + chụp; danh sách cấm; ghi tài nguyên đã tiêu |
| Kiểm tra MCP trên prod lấy mất bug thật | Gọi công cụ ghi khi chỉ định kiểm | Prod chỉ đọc; sao lưu trước khi ghi; khôi phục từ bản sao |
| Kết luận Đạt cũ đóng bug sau khi dev sửa lại | Không gắn kết luận với lần sửa | `bug_since` + huỷ kết luận chờ khi Đã sửa lần mới |
| Mọi token tester được coi là bot | Điều kiện `role=tester && token` | Bot = tester + token + email `.bot` |
| Ghi prod bằng script bị bộ phân loại Claude Code chặn | Thao tác sửa dữ liệu dùng chung | Chạy thử để user thấy, user tự chạy `--apply` (hoặc thêm quyền) |
| Giới hạn tin chặn đăng ghi chú | Đếm cả phân tích tự động + thẻ kết luận | Chỉ đếm `kind = 'text'` |
| Patch note "chưa so được" hàng loạt | Trần 300 HEAD / patch, 2 file mới ăn hết, file còn lại bị bỏ | Làm tiếp lượt sau + chỉ mục đầy đủ + `skip_reason` |
| "Không tải được" với file lớn | Giới hạn 800 KB, gắn nhầm lý do | 6 MB (đo CPU), lý do `too_large` riêng |
| Dòng kiểu cũ không được làm lại | Dữ liệu lưu trước khi có `skip_reason` | Coi `{skipped:true}` không lý do là cần làm lại |
| Patch note đầy tên file / hàm | Hiện số liệu thô cho người | Tóm tắt 1-2 câu / mảng; thô chỉ cho bot |
| Agent tải VPS "ước đoán" sai | Không đo | Đo thật trên bản sao DB prod; ghi đè báo cáo ước đoán |
| Agent để quên script trong gốc repo, lọt vào commit | `git add -A` | Xem `git status` trước commit; gỡ bằng commit mới |
| PowerShell: hàm tên `R` không chạy | `R` là alias Invoke-History | Đặt tên khác (`Rep`) |
| PowerShell: thay chữ hàng loạt làm hỏng file | Mảng 1 phần tử bị "trải" thành ký tự (`$pr[0]` = chữ cái đầu) | Dùng Edit tool hoặc `[ordered]@{}`; đọc lại file sau khi thay |
| Ngoặc kép mất khi gửi lệnh qua ssh | Lớp shell từ xa | `-EncodedCommand` (base64 UTF-16) |
| `adb shell su -c "..."` báo `Unknown id` | Cú pháp su của LDPlayer | `adb shell "su -c 'sh /sdcard/x.sh'"` |
| Đăng nhập tài khoản test báo sai mật khẩu | Dev đổi mật khẩu khi sửa bug | Dừng, hỏi user; cập nhật file mật khẩu không in |
| Kết luận "Không đạt" sai (L-036) | Agent so với tên phiếu, không so với Kết quả mong đợi; ảnh gắn thẻ là 3 ảnh bình thường | Bảng giao việc chép nguyên Kết quả mong đợi; ảnh đầu = ảnh chứng minh; controller so lại mọi Không đạt; sai thì Lead "Không duyệt" rồi gửi kết luận mới |
| Màn cần kiểm "không tìm thấy" | Màn chỉ hiện theo điều kiện | Tra code game trên CDN trước khi giao lại |
| Game báo "Có phiên bản mới" lặp mãi | Nút khởi động lại trong game không tải bản mới | Force-stop + start app |
| Giả lập tắt ngang | RAM trống < 0,7 GB | Đo RAM trong lượt; nhờ user đóng bớt Edge |

## Nguyên tắc rút ra

- Số liệu phải đo / trích nguồn; báo cáo agent có nguồn yếu thì tự mở nguồn gốc kiểm (ISTQB, Google SRE, Anthropic, bài báo).
- Không tự duyệt thay Lead; câu hỏi phán xét (thiết kế hay lỗi) chuyển Lead / PO.
- Máy chủ / CDN của game không phải của mình: ít request, có ngân sách.
