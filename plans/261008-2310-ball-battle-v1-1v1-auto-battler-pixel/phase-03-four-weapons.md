---
phase: 3
title: "Four weapons"
status: pending
priority: P1
dependencies: [2]
effort: "1 ngày"
---

# Phase 3: Four weapons

## Overview
4 vũ khí tên/thiết kế riêng (không dùng tên của Earclacks), mỗi cái một quy tắc scale, tạo vòng khắc chế. Số liệu dưới là khởi điểm, phase 7 chỉnh.

## Requirements
| Tên tạm | Hình | Bắt đầu | Mỗi lần trúng | Khắc / bị khắc |
|---|---|---|---|---|
| Blade (kiếm) | đoạn dài 24 (từ r-4=12) | dmg 1, xoay 6°/tick | dmg +1 | ổn định; thua parry dày (Fang) |
| Fang (dao găm) | đoạn dài 12 | dmg 1, xoay 8°/tick | xoay +k, k giảm 2%/lần; dmg giữ 1 | parry giỏi, khắc Pike; thua Brawler |
| Pike (giáo) | đoạn dài 26 | dmg 1, xoay 4°/tick | dài +0.5, dmg +0.5 | tầm xa, khắc Brawler; dài → dễ bị parry |
| Brawler (tay không) | không vũ khí | tốc tối đa 3 | dmg = hệ số × |v|; tốc tối đa +0.5 khi trúng hoặc chạm tường | không bị parry, khắc Fang; không đỡ được → thua Pike/Blade |

- Inner mặc định 12 (bóng r=16). Thử nghiệm Phase 2: lưỡi dài hơn → trận nhanh hơn (24→30: trung vị 54→47 s). <!-- Updated: Phase 2 pacing sweep (reports/phase-02-pacing-sweep.md) -->
- Trần: Pike dài ≤ 90 (arena tối thiểu 110x110); Fang xoay ≤ 40°/tick; Brawler tốc ≤ 12 (giữ ổn định vật lý).
- Màu nhận diện mỗi bóng; chỉ số hiện trên HUD (phase 4).
- Thêm vũ khí mới = 1 class `IWeaponRule` + 1 dòng đăng ký.

## Related Code Files
- Create: `Sim/Weapons/BladeRule.cs`, `FangRule.cs`, `PikeRule.cs`, `BrawlerRule.cs`, `WeaponRegistry.cs`, `WeaponStats.cs`
- Create: `SimTests/WeaponRuleTests.cs`

## Implementation Steps
1. `WeaponStats` (dmg, length, spin, maxSpeed, hitCount) hiển thị được.
2. Viết 4 rule + registry.
3. Test từng rule: sau N lần trúng chỉ số đúng công thức, tôn trọng trần.
4. Chạy thử 100 trận mỗi cặp, ghi tỉ lệ thắng thô vào report (chưa chỉnh).

## Success Criteria
- [ ] 4 rule đúng công thức, test xanh.
- [ ] Brawler không bị parry; Fang parry được Blade/Pike.
- [ ] Báo cáo tỉ lệ thắng thô ở `plans/.../reports/`.

## Risk Assessment
- Fang xoay nhanh xuyên qua (tunneling) → kiểm va chạm theo cung quét giữa 2 tick, không chỉ vị trí cuối.
- Brawler quá mạnh/yếu do phụ thuộc vật lý → để phase 7 chỉnh hệ số.
