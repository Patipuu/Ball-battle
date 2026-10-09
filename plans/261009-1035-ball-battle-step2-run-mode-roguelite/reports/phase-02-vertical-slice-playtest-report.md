# Phase 2 — lát cắt dọc Run mode: báo cáo chơi thử (2026-10-09)

## Đã có
- Chơi trọn 1 run 8 trận / 3 boss khổng lồ trong Editor: Run menu → chọn 1/3 vũ khí → màn Chuẩn bị (xem đối thủ, 3 thẻ, xu, đổi bộ) → trận (×2) → kết quả → … → tổng kết + mở khóa.
- Thoát giữa chừng rồi tiếp tục: ở màn Chuẩn bị → về đúng màn đó; **đang trong trận (kể cả đã thấy kết quả) → vào lại đúng trận đó**, không đổi được build.
- 3 trait tạm: Heavy (to, nặng, chậm, +HP), Spiky (chạm thân gây sát thương), Vampire (hút máu).
- Ảnh: `reports/phase-02-captures/` (title, prepare, trận có tag trait, boss khổng lồ, tổng kết).

## Bot (2.000 run mỗi chính sách, song song ≈ 35 s)
| Chính sách | Thắng run | Trận thắng TB | Hạ 0/1/2/3 boss |
|---|---|---|---|
| Ngẫu nhiên | 11.2% | 4.08 | 22.6 / 39.3 / 30.9 / 7.3% |
| Tham lam | 40.5% | 6.08 | 5.3 / 16.2 / 46.8 / 31.8% |

Mục tiêu Phase 7: ngẫu nhiên 5–20%, tham lam 30–60% → đang trong khoảng. Với bộ số ban đầu, bot gần như không thắng nổi (0% / 2.3%, dòng A của sweep), nên đã hạ:
- Boss: **160 / 220 / 300 HP, 0 / 1 / 2 trait** (plan: 250/350/500, 1/2/3).
- Thắng trận hồi **30%** HP tối đa (plan: 20%).
- Đối thủ thường ít trait hơn ở màn 1–2.
(Bảng so sánh các phương án: `SimTests/RunDifficultySweep.cs`.)

## Code review & sửa
- Thoát sau khi thấy kết quả để chọn lại thẻ → khóa run khi bắt đầu trận (`FightLocked`).
- Lưu file lỗi làm kẹt game → lưu không bao giờ ném lỗi; kết quả trận không áp dụng 2 lần.
- Heavy cho HP "miễn phí" giữa các trận → Heavy giữ tỉ lệ HP; boss + Heavy không phình quá r=24.
- HP lẻ (99.99) vẫn mời thẻ hồi máu → HP làm tròn điểm nguyên.
- Đổi phiên bản save xóa hết tiến trình → giữ tiến trình, chỉ bỏ run dở; run chứa nội dung không còn tồn tại bị bỏ khi mở.
- Thêm nội dung (Phase 3/4) làm đổi đối thủ của run đang dở → run chụp lại danh sách vũ khí/trait đối thủ lúc bắt đầu.
- Thẻ trait sẽ lấn át khi có 10 trait → cả nhóm trait dùng chung một trọng số.
- Kích thước/HP Heavy chỉ hiện sau đếm ngược → áp spawn hook trước đếm ngược.
- Nhỏ: hòa hiện "DRAW -1 LIFE"; tổng kết hiện tên thứ mở khóa thật; bỏ run cũ vẫn tính 1 lần chơi.

## Để lại
- Phase 3: luật trait hợp vũ khí (Twin Blade, Parry Master, Poison Tip), Reflect với khiên/Bubble.
- Phase 5: arena trong Run (preview + kiểm tra chỗ spawn boss).
- Phase 7: bot chưa dùng đổi vũ khí / xem trước đối thủ; tinh chỉnh lại sau khi có đủ nội dung.

## Cảm nhận
Chơi thử qua MCP mới kiểm tra được luồng và hình (ảnh ở trên). Với 3 trait, chiều sâu lựa chọn thẻ còn mỏng như plan đã dự; đánh giá lại sau Phase 3. **Cần bạn chơi thử 1–2 run để cho cảm nhận thật.**
