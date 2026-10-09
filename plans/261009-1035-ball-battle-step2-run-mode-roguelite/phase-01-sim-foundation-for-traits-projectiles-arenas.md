---
phase: 1
title: "Sim foundation for traits projectiles arenas"
status: completed
priority: P1
dependencies: []
effort: "2 ngày"
---

# Phase 1: Sim foundation for traits projectiles arenas

## Overview
Mở rộng MatchSim để chứa được trait, hiệu ứng trạng thái, đạn và vật cản trong arena — vẫn tái lập, vẫn không cấp phát mỗi tick. Chưa thêm nội dung (chỉ 1–2 mẫu để test).

## Requirements
- **Trait layer:** `TraitRule` (instance riêng mỗi bóng, có cấp 1/2, HashState bắt buộc) với móc: `OnSpawn`, `ModifyOutgoingDamage`, `ModifyIncomingDamage`, `OnHitDealt`, `OnHitTaken`, `OnParry`, `OnWall`, `OnTick`. Thứ tự gọi cố định (theo chỉ số trait) → tái lập.
- **Chỉ số khởi đầu:** `BallLoadout` = weapon id + list trait (id, level) + stat bonus (dmg%, speed%) + **MaxHp, HP hiện tại (mang qua trận) và bán kính** (boss khổng lồ r=24). MatchSim nhận loadout thay vì chỉ WeaponRule (giữ constructor cũ cho Versus). <!-- Updated: Validation Session 1 -  HP mang qua trận + bán kính boss -->
- **Trạng thái:** `StatusEffects` trên bóng: độc (stack, sát thương/giây, thời gian), khiên (chặn N đòn), hồi máu theo thời gian. Mảng cố định, không List mới mỗi tick.
- **Đạn:** pool cố định trong MatchSim (vd 128): vị trí, vận tốc, bán kính, chủ, sát thương, số lần nảy còn lại, đâm xuyên; va bóng / lưỡi (bị parry đánh bật) / tường. Sự kiện `ProjectileFired`, `ProjectileHit`, `ProjectileDeflected`.
- **Vật cản arena:** hình tròn và đoạn thẳng tĩnh (cột, tường gai); vùng (bumper, trọng lực). Nảy như tường; có thể gây sát thương (gai). `ArenaLayout` thay cho ArenaRect thuần (vẫn co dần).
- **Sự kiện mới** cho View: StatusApplied, StatusTick, ShieldBlocked, ObstacleHit, Heal.
- Determinism: hash gồm trạng thái, đạn, vật cản động, trait state.

## Architecture
```
Sim/
  Loadout/BallLoadout.cs, StatBonus.cs
  Traits/TraitRule.cs, TraitRegistry.cs, TraitTuning.cs
  Status/StatusEffects.cs
  Projectiles/ProjectilePool.cs, MatchSim.Projectiles.cs (partial)
  Arena/ArenaLayout.cs, Obstacle.cs, MatchSim.Obstacles.cs (partial)
```
Damage pipeline (một chỗ duy nhất): base weapon dmg → trait outgoing (attacker, theo thứ tự) → khiên → trait incoming (target) → áp dụng → OnHitDealt/OnHitTaken.

## Related Code Files
- Modify: `MatchSim.cs`, `MatchSim.Collisions.cs` (dùng damage pipeline chung), `BallState.cs`, `WeaponRule.cs` (móc bắn đạn), `MatchConfig.cs`, `SimVersion.cs`
- Create: các file ở trên; `SimTests/TraitHookTests.cs`, `ProjectileTests.cs`, `ObstacleTests.cs`, `StatusTests.cs`

## Implementation Steps
1. BallLoadout + MatchSim constructor mới; Versus dùng loadout không trait (hash cũ có thể đổi → cập nhật test).
2. Damage pipeline chung; chuyển TryHit/ApplyHit sang dùng nó.
3. TraitRule + 2 trait mẫu (test double) → test thứ tự móc + hash.
4. StatusEffects (độc, khiên, hồi) + test.
5. ProjectilePool + va chạm (bóng, lưỡi = parry đánh bật, tường) + test tái lập.
6. ArenaLayout + vật cản tròn/đoạn + vùng; test không xuyên vật cản 1.000.000 tick.
7. Chạy lại toàn bộ test Step 1 + bảng cân bằng 4 vũ khí: phải giữ nguyên ngưỡng.

## Success Criteria
- [x] Toàn bộ test cũ xanh; bảng cân bằng 4 vũ khí vẫn trong 30–70% (kết quả Versus giống hệt Step 1 — `VersusGoldenTests`).
- [x] Test mới: thứ tự móc trait tái lập, độc cộng dồn đúng, khiên chặn đúng số đòn, đạn bị parry đánh bật, bóng không xuyên vật cản.
- [x] Bóng bán kính 24 và HP khởi đầu < MaxHp chạy đúng (va chạm, HUD %, hash).
- [x] Không cấp phát trong `Step()` (test đếm GC.GetAllocatedBytesForCurrentThread trước/sau 10.000 tick = 0 sau khởi tạo).

## Risk Assessment
- Damage pipeline đổi thứ tự → đổi kết quả Step 1: chấp nhận, tăng SimVersion, chạy lại cân bằng.
- Đạn nhanh xuyên bóng → substep theo tốc đạn (đã có cơ chế substep).
- Phình MatchSim → tách partial theo chủ đề, mỗi file < 200 dòng.

## Kết quả (2026-10-09)
- SimTests 96/96 (51 cũ + 45 mới), Unity EditMode 17/17, compile netstandard2.1 sạch.
- Kết quả Versus 4 vũ khí **giống hệt bit** Step 1 (digest 640 trận + `VersusGoldenTests`) → không cần cân bằng lại. `SimVersion.Rules` = 3 (hash có thêm trường mới).
- Code review 2 vòng: sửa trade phụ thuộc thứ tự khi có hook, độc lệch pha, bóng "chờ chết" vẫn hành động, đạn của bóng đã chết, API vũ khí thiếu ngữ cảnh (thêm `Sim`/`Self`, `OnHitDealt`, `OnParry(other)`, `OnDeflect`).

## Lệch so với plan (có chủ đích)
- **Không có "vùng"**: trọng lực thấp = `MatchConfig.Gravity` toàn sân; bumper = vật cản tròn có `Boost`. Đủ cho 7 arena ở Phase 5.
- **`TraitRegistry`/`TraitTuning`** để Phase 3 (chưa có nội dung).
- **Va chạm bóng-bóng theo khối lượng** (∝ r²) để boss r=24 nặng hơn; bằng nhau thì giống hệt cũ.
- Thêm `TraitRule.TryPreventDeath` (cho Second Wind), `BallState.BladeShift` (lưỡi boss không bị lún vào thân; View đã dùng).
- Thời lượng độc/hồi làm tròn lên giây chẵn (mỗi giây 1 nhịp).

## Để lại cho phase sau
- Phase 2: loadout dùng 1 lần/trận → run layer dựng lại từ id + cấp mỗi trận; trạng thái "1 lần mỗi run" lưu ở run.
- Phase 3: Twin Blade cần lưỡi thứ 2 (hitbox), Parry Master cần chỉnh tham số parry; giới hạn số khiên (Bubble).
- Phase 4: Rig cần vật cản động theo trận + chủ đạn không phải bóng (turret).
- Phase 6: View vẽ đạn, vật cản, trạng thái; boss sprite.
