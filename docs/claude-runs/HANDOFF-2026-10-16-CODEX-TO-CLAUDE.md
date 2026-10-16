# Handoff 16/10/2026 — Codex → Claude

Phú yêu cầu commit và trả công việc lại cho Claude. Đọc file này cùng `bb12g.md`, `bb12h.md`, `bb12i.md`, `bb14.md`, `bb15-balance.md`. Workspace `E:\ball-battle`, Unity 6000.5.1f1. Branch `dev/step3-view`.

## Đã có trong code

- BB12g: cây skill riêng từng bóng, tối đa 10 điểm ở LV10, 18 build tiêu đủ 10 điểm; bỏ SKILL UP card; respec miễn phí; Run nhận cây từ save.
- BB12h/i: node dạng icon riêng theo bóng, giữ node để mở thẻ có nút UPGRADE; đã kiểm tra icon và thẻ cho đủ 10 bóng.
- BB14: thêm BGLGXLQ (`chorus`) và HUNTER (`serpent`) vào Sim, Run, Versus, UI, art placeholder, âm thanh và cây skill.
- BB15: chỉnh WRECKER và BGLGXLQ trong **Run** để đạt gate LV10. Versus giữ thông số cũ; `SimVersion.Rules = 16`.

## Số đo BB15

Greedy RunBot, không tap, 200 seed 3001–3200, 18 build LV10/bóng:

- WRECKER: tốt nhất **68,5%** (trước 76,0%); holdout seed 3201–3400: **66,0%**.
- BGLGXLQ: tốt nhất **69,5%** (trước 73,0%); holdout: **65,5%**.
- Dùng số BB14 của tám bóng không thay đổi, cả 10 hiện có max **70%**, spread **10 điểm**, A/B gap max **4,5 điểm**. CSV và lệnh chạy ở `bb15-balance.md`.
- SimTests **351 đạt, 0 lỗi**; Unity EditMode **550 test: 531 đạt, 0 lỗi, 19 bỏ qua**. XML `bb15-editmode.xml`.

## Việc tiếp theo

1. Thiết kế **gate LV1** rồi đo lại: BGLGXLQ 14,5% ở tập chính, 10,5% ở holdout; SPORE 14%, HUNTER 17,5%; WRECKER khoảng 41%. Không tăng sức mạnh mở màn tùy tiện vì LV10 vừa đạt ngưỡng.
2. Playtest cảm giác cây: nhiều build mạnh nhất bỏ FINALE dù node này tốn 3 điểm; cân nhắc giá trị lựa chọn thay vì chỉ nhìn win rate.
3. Art BGLGXLQ và ảnh bạn của Phú còn placeholder; tên chính thức HUNTER chưa được cung cấp. Xem `bb14.md`.
4. Đánh giá BB13 prediction/deeds trong `docs/design/bb13-breakthrough-research.md` khi Phú chọn hướng tiếp theo. Đây mới là nghiên cứu, chưa implement.

Phú từng yêu cầu tên skill tiếng Việt tự nhiên, có nhịp và sắc thái như bản dịch game Việt; tài liệu `docs/design/bb12-persona-web-v5.md` chứa các tên đã được Phú chấp thuận. UI hiện còn nhiều chữ tiếng Anh. Khi Việt hóa UI, dùng tên đã chốt trong tài liệu, tránh dịch từng chữ.

Đừng áp dụng lại patch BB15: code và CSV đã ở branch. Không reset các thay đổi đang có trong workspace nếu còn file chưa commit.
