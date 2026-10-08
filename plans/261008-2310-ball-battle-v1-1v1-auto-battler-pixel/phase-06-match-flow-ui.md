---
phase: 6
title: Match flow UI
status: in-progress
priority: P1
dependencies:
  - 4
effort: 0.5 ngày
---

# Phase 6: Match flow UI

## Overview
Vòng chơi đầy đủ: Menu chọn bóng → đếm ngược → best-of-3 → kết quả → đấu lại.

## Requirements
- Menu (pixel): lưới 4 ô vũ khí, chọn bên Đỏ và bên Xanh (trùng được), nút Random, nút Bắt đầu.
- Đếm ngược 3-2-1 trước mỗi ván.
- Best-of-3: hiện tỉ số "1 - 0"; ván hòa đánh lại. Seed ván = hàm(seed trận, số ván).
- Màn kết quả: người thắng, tỉ số, nút Đấu lại (cùng seed → trận y hệt), Trận mới (seed mới), Về menu.
- Hiện seed nhỏ ở góc (để tái lập/quay clip).
- UI dùng sprite pixel cùng camera hoặc Canvas Screen Space - Camera trên RT pixel; chạm/click được.

## Related Code Files
- Create: `View/MenuView.cs`, `View/MatchFlow.cs` (state machine: Menu, Countdown, Playing, RoundEnd, Result), `View/PixelButton.cs`, `Assets/Scenes/Menu.unity`
- Modify: `Editor/SceneBuilder.cs` (dựng Menu, thêm Build Settings)

## Implementation Steps
1. MatchFlow state machine thuần (test được ở EditMode).
2. MenuView + PixelButton.
3. Nối Arena với MatchFlow, best-of-3, kết quả.
4. SceneBuilder thêm Menu làm scene 0.

## Success Criteria
- [ ] Đi hết vòng Menu → 3 ván → Kết quả → Đấu lại không lỗi console.
- [ ] Đấu lại cùng seed cho trận giống hệt (so hash cuối).
- [ ] EditMode test cho MatchFlow xanh.

## Risk Assessment
- Click trên RT phóng to lệch tọa độ → quy đổi tọa độ màn → pixel gốc trong 1 hàm, có test.
