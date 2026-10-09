---
phase: 6
title: "Visual language and run UI"
status: completed
priority: P1
dependencies: [5]
effort: "2 ngày"
---

# Phase 6: Visual language and run UI

## Overview
Giữ "hiểu trong 2 giây" khi có thêm đạn, độc, khiên, turret, trait: ký hiệu thống nhất trên màn 270 px + hoàn thiện UI Run.

## Requirements
- **Ngôn ngữ hình ảnh:** mỗi hiệu ứng 1 màu + 1 hình: độc = chấm xanh lá nhấp nháy quanh bóng + số stack; khiên = vòng trắng; hồi máu = dấu + bay lên; turret = khối vuông màu chủ; đạn = 2–3 px màu chủ. Bảng màu trạng thái không trùng màu vũ khí.
- **Trait badge:** 3 ô icon 7x7 trên HUD mỗi bên (cấp 2 có viền vàng); icon placeholder sinh bằng code.
- **Versus:** chọn arena + đủ 8 vũ khí (ô chọn nhỏ hơn hoặc 2 hàng mỗi bên); không trait. <!-- Updated: Validation Session 1 -  Versus dùng nội dung mới -->
- **HP mang qua trận:** HUD và màn Chuẩn bị hiện HP hiện tại/tối đa; boss khổng lồ có sprite 48x48.
- **UI Run hoàn chỉnh:** bản đồ run 8 nút (màn/boss), màn Chuẩn bị (đối thủ: vũ khí + trait + arena; 3 thẻ có icon + mô tả ngắn ≤ 2 dòng 3x5; xu; ĐỔI 1 / MUA 3), tổng kết run, màn mở khóa (ô bị khóa hiện dấu ? và điều kiện mở), nút ×2 trong trận.
- **FX/âm** cho sự kiện mới (đạn bắn/trúng, độc tick, khiên chặn, hồi máu, vật cản).
- Font: thêm ký tự cần cho mô tả (=, +, %, x đã có/thiếu) — kiểm bằng test như Step 1.

## Related Code Files
- Create: `View/Run/RunMapView.cs`, `CardView.cs`, `TraitBadgeView.cs`, `StatusView.cs`, `ProjectileView.cs`, `ObstacleView.cs`
- Modify: `HudView.cs`, `FxView.cs`, `PixelFontData.cs`, art/sfx generator

## Success Criteria
- [ ] Ảnh chụp 1080x1920 các màn Run: 0 khối lệch; chữ mô tả đọc được.
- [ ] Không GC mỗi frame trong trận có 8 turret + 30 đạn.
- [ ] Mọi chuỗi UI có trong font (test).

## Risk Assessment
- Mô tả trait không vừa 2 dòng ở font 3x5 → viết tắt chuẩn hoá (DMG, HP, SPD, /S).
