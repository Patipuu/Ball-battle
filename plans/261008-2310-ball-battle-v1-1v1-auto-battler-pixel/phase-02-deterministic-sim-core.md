---
phase: 2
title: Deterministic sim core
status: completed
priority: P1
dependencies:
  - 1
effort: 1.5 ngày
---

# Phase 2: Deterministic sim core

## Overview
Vật lý + luật trận viết tay bằng C# thuần, tick 60 Hz, tái lập từ seed. Không dùng Physics2D của Unity (khó tái lập, khó test ngoài Unity).

## Requirements
- Arena: vuông 230x230 đơn vị (1 đơn vị = 1 pixel gốc) giữa màn 270x480, HUD trên/dưới. <!-- Updated: Phase 2 pacing sweep (reports/phase-02-pacing-sweep.md) -->
- Bóng: hình tròn r=16, tốc xuất phát 5–7, tối đa 10 đơn vị/tick, trọng lực 0.05, va tường đàn hồi (e=1; nảy sàn tối thiểu 5; tốc ngang tối thiểu 1.5), va bóng-bóng đàn hồi.
- Vũ khí: đoạn thẳng (capsule) gắn tâm bóng, xoay góc đều (deg/tick), có độ dài, độ dày.
- Đánh trúng: capsule vũ khí A giao hình tròn B → B mất HP = dmg(A); A nhận `OnHit` để scale; cooldown cùng cặp 0.25 s.
- Parry: capsule A giao capsule B → cả hai đảo chiều xoay, hitstop 6 tick (toàn trận khựng), tách nhẹ để không dính liên tục. Khi 2 lưỡi còn chạm nhau, không lưỡi nào gây sát thương cho cặp đó.
- Bóng không vũ khí (Brawler): thân là hitbox tấn công; dmg theo vận tốc.
- HP 100; ≤0 → chết, trận kết thúc.
- Arena co: từ 90 s, 4 tường dịch vào đều tới vùng tối thiểu 110x110 trong 30 s; bóng bị tường đẩy vào trong, không xuyên. Trần an toàn 180 s → so % HP, bằng nhau → hòa. <!-- Updated: Validation Session 1 - 150 s timeout -> arena co sau 90 s + trần 180 s -->
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
- [x] Test hình học xanh (biên tiếp xúc, song song, trùng).
- [x] Bóng không lọt tường sau 1.000.000 tick ở vận tốc tối đa.
- [x] Cùng seed → hash giống nhau sau 9000 tick; khác seed → khác.
- [x] Parry đảo chiều xoay, không gây sát thương, không kẹt dính > 10 tick.
- [x] Khi arena co: không bóng nào nằm ngoài tường ở bất kỳ tick nào (lưỡi vũ khí được phép chồm qua tường, View cắt theo khung arena).

## Risk Assessment
- Xuyên tường ở tốc độ cao → giới hạn vận tốc + substep 2 lần/tick.
- Vũ khí kẹt vào nhau parry liên tục → đẩy tách theo pháp tuyến + cooldown parry cùng cặp.
- Bóng nằm yên ở đáy → tốc độ tối thiểu + kích nhẹ ngẫu nhiên từ RNG có seed.

## Kết quả (2026-10-08)
- 31 SimTests xanh; Sim compile netstandard2.1 + C# 9 sạch (warnings = errors); Unity compile OK.
- Pacing (blade +1/hit, 300 seed): trung vị 58 s, p90 77 s, 0 trận chạm trần. Chi tiết: `reports/phase-02-pacing-sweep.md`.
- Đổi so với plan gốc: arena 230x230 vuông, r=16, tốc 5–7/max 10 (lý do: plan gốc trung vị 119 s).
- Sửa theo code review: weapon instance riêng mỗi bóng (bắt buộc), hash trạng thái weapon con bắt buộc, substep theo tốc đầu lưỡi (MaxSubsteps 32), trade đối xứng + cùng hạ gục = hòa, giới hạn tốc độ đúng thứ tự, đẩy lùi theo điểm chạm.

## Để sau (ghi nhận từ review)
- Parry lật cả 2 chiều xoay có thể làm 2 lưỡi kẹt tối đa 10 tick → đo ở Phase 3/7 (số parry/phút, thời gian kẹt) với Fang/Pike thật.
- Chưa có test riêng cho đầu lưỡi lướt qua nhau (tip-to-tip) với substep theo tốc đầu lưỡi.
- Spawn 3+ bóng có thể chồng nhau (chưa có chế độ này).
- Đồng nhất đa nền tảng: ngoài phạm vi v1 (đã chốt validate).
