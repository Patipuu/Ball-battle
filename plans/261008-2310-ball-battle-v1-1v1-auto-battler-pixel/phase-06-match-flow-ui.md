---
phase: 6
title: Match flow UI
status: completed
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
- [x] Đi hết vòng Menu → 3 ván → Kết quả → Đấu lại không lỗi console.
- [x] Đấu lại cùng seed cho trận giống hệt (so hash cuối).
- [x] EditMode test cho MatchFlow xanh.

## Risk Assessment
- Click trên RT phóng to lệch tọa độ → quy đổi tọa độ màn → pixel gốc trong 1 hàm, có test.

## Kết quả (2026-10-09)
- `Sim/Series.cs`: best-of-3, hòa đánh lại bằng seed mới, seed ván = SeedFor(seed trận, chỉ số ván). Test dotnet: phát lại cả loạt với cùng seed → hash từng ván giống hệt.
- `View/MatchFlow.cs` (C# thuần): Menu → Countdown → Playing → RoundOver → Result; lệnh sai trạng thái bị bỏ qua. EditMode test.
- `PixelInput` (đổi toạ độ màn → pixel gốc theo Windowbox, có test 1080x1920 / 1080x2400 / 1920x1080), `PixelButton` (bấm-thả trong nút), `MenuView`, `OverlayView`, `GameController`; `ArenaView.Paused`; Esc/Back về menu.
- Chạy thật qua MCP (tốc x6): PIKE vs BRAWLER 2-0 → REMATCH: 2 ván cùng seed/tick/hash/kết quả → NEW MATCH seed mới → MENU. Log: `reports/phase-06-playthrough.log`.
- EditMode 17/17; SimTests 51/51; console sạch.

## Khác plan
- Menu là lớp phủ trong cùng scene `Arena.unity` thay vì `Menu.unity` riêng: một AudioListener, chuyển màn tức thì, không phải nạp scene.

## Còn mở
- Bấm nút bằng chuột/chạm thật mới kiểm bằng unit test toạ độ; cần người chơi thử (Phase 7 playtest).
- Đếm ngược "3" có thể ngắn 1 khung hình (chấp nhận).
