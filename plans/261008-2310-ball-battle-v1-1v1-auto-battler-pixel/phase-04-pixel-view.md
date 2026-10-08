---
phase: 4
title: "Pixel view"
status: pending
priority: P1
dependencies: [3]
effort: "1 ngày"
---

# Phase 4: Pixel view

## Overview
Hiển thị trận ở 270x480 pixel, phóng nguyên x4, art placeholder sinh bằng code.

## Requirements
- Pixel Perfect Camera: dùng component của URP `UnityEngine.Rendering.Universal.PixelPerfectCamera` (không dùng `UnityEngine.U2D` của package — dành cho Built-in); kiểm asmdef View resolve đủ reference khi có script đầu tiên. <!-- Updated: Phase 1 review --> Assets PPU 1, reference 270x480, Grid Snapping = Upscale Render Texture, crop X/Y; point filter, không nén.
- Sprite placeholder do Editor tool sinh PNG: bóng r=16 → 32x32 (viền 1px + bóng đổ 1px), arena vuông 230x230 giữa màn, HUD ~125px trên/dưới, <!-- Updated: Phase 2 pacing sweep (reports/phase-02-pacing-sweep.md) --> mỗi vũ khí 1 sprite hướng phải, Pike vẽ thân lặp được (dài ra không méo), nền arena + tường, ô HP.
- Font pixel: atlas chữ số + chữ hoa 3x5 hoặc 4x6 sinh bằng code.
- View nội suy giữa 2 snapshot (60 Hz sim, render theo màn).
- Tường arena vẽ theo vị trí co hiện tại (cảnh báo nhấp nháy 3 s trước khi co, sọc nguy hiểm). <!-- Updated: Validation Session 1 - hiển thị arena co -->
- HUD trên/dưới: tên + màu + thanh HP + số HP + chỉ số scale chính (vd "DMG 7", "SPD 21", "LEN 34", "MAX 5.5"), cập nhật khi `StatChanged`.
- Vũ khí đổi màu dần về đỏ theo mức scale.

## Architecture
```
View/ArenaView.cs        giữ MatchSim, Update() chạy tick theo thời gian thật, nội suy
View/BallView.cs         sprite bóng + vũ khí, xoay, kéo dài Pike
View/HudView.cs          HP, chỉ số
View/PixelText.cs        vẽ chữ từ atlas
Editor/PlaceholderArtGenerator.cs   menu "BallBattle/Generate Placeholder Art" + batch
Editor/SceneBuilder.cs   menu "BallBattle/Build Scenes" + batch (-executeMethod)
```

## Related Code Files
- Create: các file ở trên; `Assets/Art/Generated/*.png`; `Assets/Scenes/Arena.unity`

## Implementation Steps
1. PlaceholderArtGenerator → PNG + import setting (PPU 1, Point, no compression).
2. SceneBuilder dựng Arena: camera pixel perfect, nền, 2 BallView, HUD.
3. ArenaView chạy sim + nội suy.
4. HUD + PixelText.
5. Chụp màn bằng Unity MCP, kiểm không mờ.

## Success Criteria
- [ ] Play Arena với cặp cố định: thấy 2 bóng nảy, vũ khí xoay, HP giảm, chỉ số tăng.
- [ ] Ảnh chụp 1080x1920: mỗi pixel gốc đúng khối 4x4 (không mờ, không lệch lưới).
- [ ] 60 fps trên Editor.

## Risk Assessment
- Upscale RT làm UI uGUI cũng bị pixel hóa không đều → HUD vẽ bằng sprite trong world space cùng camera, không dùng Canvas Overlay cho HUD trận.
