---
phase: 4
title: "Four new weapons"
status: pending
priority: P1
dependencies: [3]
effort: "2 ngày"
---

# Phase 4: Four new weapons

## Overview
4 vũ khí mở 4 trục khắc chế mới, dùng nền đạn/trạng thái/vật thể của Phase 1. Tên tạm; cơ chế lấy cảm hứng từ thể loại, tên/art tự thiết kế.

## Requirements
| Tên tạm | Cơ chế | Mỗi lần trúng | Khắc / bị khắc |
|---|---|---|---|
| Volley (bắn xa) | Lưỡi ngắn; mỗi 1.2 s bắn loạt mũi tên theo hướng lưỡi | +1 mũi tên mỗi loạt (tối đa 8) | mạnh sân to; yếu sân nhỏ, bị parry đánh bật |
| Venom (độc) | Lưỡi cong rộng, xoay chậm; đòn gây độc cộng dồn | +1 stack độc tối đa | khắc hồi máu/Second Wind; thua dồn sát thương nhanh |
| Aegis (phản đòn) | Khiên rộng, không gây sát thương; parry phản lại sát thương của đòn đối thủ | khiên rộng +0.1 khi đập trúng thân | đối thủ càng mạnh càng mạnh; thua độc/đạn |
| Rig (turret) | Mỗi lần trúng đặt 1 turret (vật cản tròn bắn đạn nhỏ) | +1 turret (tối đa 6) | kiểm soát không gian, khắc tầm xa; thua dồn sát thương sớm |
- Số liệu ở WeaponTuning; HUD stat (ARROWS, VENOM, WIDTH, RIGS).
- Cả 4 vũ khí mới bị khóa lúc đầu trong Run (mở theo UnlockRules); Versus dùng được ngay.
- Turret = vật cản động (có chủ, HP? → không, tồn tại tới hết trận) → hash.

## Related Code Files
- Create: `Sim/Weapons/VolleyRule.cs`, `VenomRule.cs`, `AegisRule.cs`, `RigRule.cs`; test từng cái
- Modify: `WeaponRegistry.cs`, `WeaponTuning.cs`, `Palette.cs`, art generator (ball + blade/khiên/turret sprite)

## Implementation Steps
1. 4 rule + test công thức/giới hạn.
2. Bảng cân bằng 8×8 (500 seed/bên, song song) → chỉnh tới 30–70% mọi cặp.
3. Kiểm thời lượng trận (trung vị 25–60 s) — Aegis gương/Rig gương có thể kéo dài → luật riêng nếu cần.
4. Cập nhật BalanceThresholdTests cho 8 vũ khí.

## Success Criteria
- [ ] 28 cặp không gương 30–70%; gương hòa < 5%; trần 180 s < 1%.
- [ ] Đạn/turret không làm GC mỗi frame; không xuyên tường.

## Risk Assessment
- Aegis vs Aegis không bao giờ kết thúc (giống Shield vs Grimoire của clip gốc) → arena co + luật: hai bên không gây sát thương thì sát thương tường co tăng dần.
- Rig nhốt đối thủ vĩnh viễn → turret biến mất khi arena co chạm tới.
