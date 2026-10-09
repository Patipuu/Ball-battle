---
phase: 2
title: "Run core and vertical slice"
status: pending
priority: P1
dependencies: [1]
effort: "2 ngày"
---

# Phase 2: Run core and vertical slice

## Overview
Luật Run (C# thuần, test được) + giao diện tối thiểu để **chơi trọn một run sớm** với 4 vũ khí cũ, 3 trait, 1 arena. Mục tiêu: biết vòng lặp có vui không trước khi đổ công làm nội dung.

## Requirements
- `RunState` (Sim, thuần): seed run, trận hiện tại (1..8), mạng (3), xu, loadout người chơi **(gồm HP hiện tại, MaxHp)**, lịch sử; `RunRules` (bảng màn/boss/thưởng xu/hồi máu từ `RunTuning`).
- **HP mang qua trận:** thắng → hồi 20% MaxHp; thua (HP 0) → mất 1 mạng và hồi đầy. <!-- Updated: Validation Session 1 -  HP mang qua trận -->
- `EnemyGenerator`: sinh đối thủ trận n từ seed run + n (không phụ thuộc lựa chọn người chơi → xem trước công bằng): vũ khí, trait theo màn, HP bonus; **boss = bóng khổng lồ r=24, 250/350/500 HP, 1/2/3 trait**. <!-- Updated: Validation Session 1 -  boss khổng lồ thay bản Super -->
- `CardOffer`: 3 thẻ seeded (trait mới/lên cấp, +chỉ số, đổi vũ khí, **hồi 50% HP, +15 HP tối đa**); chỉ lấy từ nội dung đã mở khóa; đổi bộ (1 xu), mua thêm (3 xu); luật 3 ô trait, cấp tối đa 2.
- Kết quả trận → cập nhật mạng/xu/trận; thắng trận 8 = thắng run; hết mạng = thua.
- `SaveData` JSON (persistentDataPath): mở khóa, kỷ lục, run đang dở (đủ để dựng lại RunState, gồm HP).
- **Mở khóa:** bắt đầu 4 vũ khí cũ + 5 trait; `UnlockRules` (hạ boss 1/2/3, thắng run…) mở vũ khí/trait mới. <!-- Updated: Validation Session 1 -  mở khóa theo thành tích -->
- UI tối thiểu (pixel, tái dùng PixelButton/PixelText): Run menu → chọn 1/3 vũ khí → màn Chuẩn bị (xem đối thủ + 3 thẻ + xu + đổi bộ) → trận (ArenaView, nút ×2) → kết quả trận → … → tổng kết run.
- Menu chính: thêm "RUN" bên cạnh "VERSUS" (Step 1).

## Architecture
```
Sim/Run/RunState.cs, RunRules.cs, RunTuning.cs, EnemyGenerator.cs, CardOffer.cs, Card.cs
View/Run/RunController.cs, PrepareView.cs, RunSummaryView.cs, SaveStore.cs
```
RunController giống GameController: một máy trạng thái thuần (`RunFlow`) + view.

## Related Code Files
- Create: các file trên; `SimTests/RunTests.cs` (tái lập run, kinh tế, luật ô trait, boss), `Tests/EditMode/RunFlowTests.cs`, `SaveStoreTests.cs`
- Modify: `MenuView.cs` (nút RUN/VERSUS), `SceneBuilder.cs`, `ArenaView.cs` (tốc ×2; nhận loadout)

## Implementation Steps
1. RunTuning + RunState + EnemyGenerator + CardOffer (seeded) + test.
2. Bot mô phỏng run (ngẫu nhiên / tham lam) chạy headless → đo tỉ lệ thắng run (dùng lại ở Phase 7).
3. SaveStore JSON + test vòng đọc/ghi + run dở.
4. RunFlow + RunController + PrepareView + RunSummaryView (art placeholder).
5. Thêm 3 trait tạm (Heavy, Vampire, Spiky) để thẻ có nghĩa.
6. Chơi thử qua MCP + user playtest → ghi cảm nhận vào report.

## Success Criteria
- [ ] Chơi trọn 1 run 8 trận trong Editor; thoát giữa chừng rồi tiếp tục được.
- [ ] Cùng seed run + cùng lựa chọn → cùng hash từng trận và cùng HP sau mỗi trận (dotnet).
- [ ] Luật HP: thắng +20% (không quá MaxHp), thua = mất mạng + hồi đầy (test).
- [ ] Bot 2.000 run chạy < 2 phút (song song).
- [ ] Báo cáo playtest lát cắt dọc ở `reports/`.

## Risk Assessment
- Vòng lặp không vui ở lát cắt dọc → dừng, brainstorm lại trước Phase 3 (đây là lý do làm lát cắt sớm).
- Lựa chọn thẻ không đủ ý nghĩa với 3 trait → chấp nhận ở phase này, đánh giá lại sau Phase 3.
