---
phase: 2
title: "Deterministic sim core"
status: pending
priority: P1
dependencies: [1]
effort: "1.5 ngày"
---

# Phase 2: Deterministic sim core

## Overview
Vật lý + luật trận viết tay bằng C# thuần, tick 60 Hz, tái lập từ seed. Không dùng Physics2D của Unity (khó tái lập, khó test ngoài Unity).

## Requirements
- Arena: hình chữ nhật 270x480 đơn vị (1 đơn vị = 1 pixel gốc), trừ HUD → vùng chơi ~250x380.
- Bóng: hình tròn r=12, trọng lực nhẹ, va tường đàn hồi (e=1, giữ tốc độ tối thiểu để không "chết" sát đáy), va bóng-bóng đàn hồi.
- Vũ khí: đoạn thẳng (capsule) gắn tâm bóng, xoay góc đều (deg/tick), có độ dài, độ dày.
- Đánh trúng: capsule vũ khí A giao hình tròn B → B mất HP = dmg(A); A nhận `OnHit` để scale; cooldown cùng cặp 0.25 s.
- Parry: capsule A giao capsule B → cả hai đảo chiều xoay, hitstop 6 tick (toàn trận khựng), tách nhẹ để không dính liên tục.
- Bóng không vũ khí (Brawler): thân là hitbox tấn công; dmg theo vận tốc.
- HP 100; ≤0 → chết, trận kết thúc.
- Arena co: từ 90 s, 4 tường dịch vào đều tới vùng tối thiểu ~120x160 trong 30 s; bóng bị tường đẩy vào trong, không xuyên. Trần an toàn 180 s → so % HP, bằng nhau → hòa. <!-- Updated: Validation Session 1 - 150 s timeout -> arena co sau 90 s + trần 180 s -->
- RNG: xorshift32 có seed (vị trí, hướng, vận tốc ban đầu; không dùng `System.Random`).
- Mỗi tick xuất `SimEvent` (Hit, Parry, WallBounce, BallBounce, Death, StatChanged) cho View/âm thanh.
- Snapshot có `ComputeHash()` để test tái lập.

## Architecture
```
Sim/
  MatchSim.cs           Step(): hitstop → tích phân → va chạm → luật → sự kiện
  MatchConfig.cs        hằng số arena, trọng lực, HP, trần giờ, seed
  BallState.cs          pos, vel, hp, weaponAngle, spinDir, cooldowns
  Geometry.cs           circle-circle, segment-circle, segment-segment
  SimRandom.cs          xorshift32
  SimEvent.cs
  Weapons/IWeaponRule.cs   OnHit, OnParry, OnWall, Damage, CanBeParried, Shape
```
Float + dt cố định; tái lập trên cùng build/máy (đồng nhất đa nền tảng: ngoài phạm vi).

## Related Code Files
- Create: các file ở trên trong `BallBattleUnity/Assets/Scripts/Sim/`
- Create: `SimTests/GeometryTests.cs`, `SimTests/MatchSimTests.cs`

## Implementation Steps
1. Geometry + test đơn vị.
2. BallState, MatchSim tích phân + tường + bóng-bóng.
3. Vũ khí dạng đoạn, xoay, hit + cooldown, parry + hitstop.
4. Arena co theo thời gian; kết thúc trận (chết / trần 180 s / hòa).
5. Sự kiện + hash.
6. Vũ khí test giả (dmg cố định) để test luật trước khi có phase 3.

## Success Criteria
- [ ] Test hình học xanh (biên tiếp xúc, song song, trùng).
- [ ] Bóng không lọt tường sau 1.000.000 tick ở vận tốc tối đa.
- [ ] Cùng seed → hash giống nhau sau 9000 tick; khác seed → khác.
- [ ] Parry đảo chiều xoay, không gây sát thương, không kẹt dính > 10 tick.
- [ ] Khi arena co: không bóng/vũ khí nào nằm ngoài tường ở bất kỳ tick nào.

## Risk Assessment
- Xuyên tường ở tốc độ cao → giới hạn vận tốc + substep 2 lần/tick.
- Vũ khí kẹt vào nhau parry liên tục → đẩy tách theo pháp tuyến + cooldown parry cùng cặp.
- Bóng nằm yên ở đáy → tốc độ tối thiểu + kích nhẹ ngẫu nhiên từ RNG có seed.
