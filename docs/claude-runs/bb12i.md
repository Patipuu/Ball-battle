# BB-12i: icon riêng cho cây skill của từng bóng

Trạng thái: **đã triển khai trong workspace, chưa commit**. Claude đã thêm `TreeIconArt.cs`, nối icon theo bóng và node trong `TreeIcons.cs`/`TreeView.cs`, và chụp các trạng thái UI trong `bb12i/`. Bản handoff này được xác minh từ file tại workspace vì link chia sẻ Claude không tải được nội dung phiên.

## Kết quả

- VITALITY và POWER dùng icon chung; mỗi bóng có sáu icon riêng cho INSTINCT, OPENER, skill A/B và FINALE A/B. Icon skill LV2/LV3 cùng nhánh dùng chung hình, khác số cấp.
- Icon mở có màu thân bóng; icon đã mua màu vàng; icon khóa màu xám. Thẻ hiện icon của node đang chọn.
- Đã mở rộng `BbTree2Tests` để kiểm tra đủ **10 bóng**, gồm `chorus` và `serpent`. Test kiểm tra key icon riêng, sprite không bị dùng lại sai node, kích thước 16 pixel, và chữ của mọi thẻ nằm trong khung.
- Ảnh chụp 1x/3x ở `docs/claude-runs/bb12i/`: cây trống, cây mua đến giữa, thẻ sẵn sàng, thẻ đã mua, thẻ bị khóa.

## Xác minh ngày 16/10/2026

- Unity EditMode, filter `BallBattle.Tests.BbTree2Tests`: **6 test, 5 đạt, 0 lỗi, 1 bỏ qua** (`CaptureIconTree` cần `BB_TREE2_OUT`; ảnh đã có từ lượt Claude chụp). Kết quả: `bb12i-editmode.xml`.
- Unity biên dịch script thành công. Lượt đầu có `-quit` thoát trước test runner; lượt thứ hai bỏ `-quit` đã tạo XML kết quả.
- Chưa chạy lại toàn bộ EditMode hoặc cân bằng Sim trong handoff này, vì phần tiếp nối chỉ mở rộng kiểm tra icon, không đổi luật trận.

## Điểm còn mở

- `bb14.md` báo WRECKER đạt 76% và BGLGXLQ 73% ở Run LV10, vượt ngưỡng 70%; cần quyết định cân bằng cho bộ 10 bóng trước khi coi cả bản BB14 là đạt gate.
- Sprite logo BGLGXLQ, ảnh bạn của tác giả và tên chính thức của HUNTER vẫn là placeholder hoặc chờ dữ liệu như ghi trong `bb14.md`.
