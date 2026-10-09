---
phase: 5
title: "Arena variants"
status: pending
priority: P2
dependencies: [4]
effort: "1 ngày"
---

# Phase 5: Arena variants

## Overview
6 arena biến thể (+ Classic = 7 cấu hình) làm thay đổi thế khắc chế; hiện ra trước khi chọn thẻ để người chơi tính toán.

## Requirements
| Arena | Đặc điểm | Ai lợi |
|---|---|---|
| Classic | Vuông 230x230 (Step 1) | trung tính |
| Pillar | Cột tròn r=14 giữa sân | parry nhiều, đạn bị chặn |
| Bumpers | 4 bumper tăng tốc ở góc | Brawler, thân |
| Spike Walls | Chạm tường mất 2 HP | vũ khí tầm xa ít chạm tường |
| Low Gravity | Trọng lực 0.02 | đạn, Pike |
| Tight | 170x170, co về 90x90 | cận chiến |
| Wide | 260x230 (rộng hơn, **không cao hơn**) | Volley, Rig | <!-- Updated: Validation Session 1 -  chiều cao arena ≤ 230 để HUD cố định -->
- `ArenaRegistry` + `ArenaTuning`; Run chọn arena theo seed trận; Versus có thể chọn arena.
- View: vẽ vật cản/vùng bằng sprite placeholder; vẫn che lưỡi ngoài arena.

## Related Code Files
- Create: `Sim/Arena/ArenaRegistry.cs`, `ArenaTuning.cs`; `SimTests/ArenaBalanceReport.cs`
- Modify: `ArenaFrameView.cs` (vật cản, vùng, kích thước khác), art generator, `MenuView.cs` (Versus chọn arena)

## Implementation Steps
1. 7 cấu hình (gồm Classic) + test không xuyên vật cản + test mọi arena cao ≤ 230 (HUD/overlay cố định theo mép ±115).
2. Báo cáo: tỉ lệ thắng trung bình mỗi vũ khí trên từng arena → không ai < 25% / > 75%.
3. View vật cản + layout sân khác kích thước (HUD vẫn đúng chỗ).

## Success Criteria
- [ ] Mỗi arena: mọi vũ khí 25–75% trung bình; ít nhất 2 vũ khí đổi thứ hạng so với Classic (arena có ý nghĩa).
- [ ] Pixel-perfect giữ nguyên trên mọi arena.

## Risk Assessment
- Arena làm một vũ khí vô dụng → giảm cường độ hiệu ứng arena thay vì sửa vũ khí.
