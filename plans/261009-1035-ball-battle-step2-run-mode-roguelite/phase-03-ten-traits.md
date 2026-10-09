---
phase: 3
title: "Ten traits"
status: pending
priority: P1
dependencies: [2]
effort: "1.5 ngày"
---

# Phase 3: Ten traits

## Overview
Đủ 10 trait, mỗi trait có cấp 1/2 và một vai khắc chế rõ. Đo cân bằng bằng "chênh lệch tỉ lệ thắng trên toàn sân".

## Requirements
| Trait | Cấp 1 | Cấp 2 | Vai |
|---|---|---|---|
| Heavy | r+2, bị đẩy −50%, tốc −10% | r+3, bị đẩy −75% | khắc Brawler |
| Spiky | chạm thân: 2 sát thương | 4 | khắc thân-thân |
| Vampire | hồi 15% sát thương gây ra | 25% | khắc độc |
| Thorns | phản 20% sát thương cận chiến | 35% | khắc Blade mạnh |
| Second Wind | 1 lần ≤30% HP: hồi 20 | hồi 35 | lật kèo |
| Glass Cannon | ×1.4 sát thương, −30 HP | ×1.6, −40 HP | rủi ro cao |
| Parry Master | parry: +1 chỉ số vũ khí (qua OnHit giả) | +2 | khắc Fang/parry nhiều |
| Bubble | chặn 1 đòn mỗi 8 s | mỗi 5 s | khắc đòn lẻ tẻ |
| Twin Blade | lưỡi phụ đối diện 60% dài | 80% | phủ rộng, dễ bị parry |
| Poison Tip | đòn gây độc 1/s × 3 s, cộng dồn | 1.5/s × 4 s | khắc hồi máu chậm |
- Số liệu ở `TraitTuning` (static, có fingerprint vào hash như WeaponTuning).
- Twin Blade cần BallState hỗ trợ 2 lưỡi (chuẩn bị luôn cho vũ khí sau).

## Related Code Files
- Create: `Sim/Traits/*.cs` (10 file), `SimTests/TraitTests.cs`, `SimTests/TraitBalanceReport.cs`
- Modify: `TraitRegistry.cs`, `BallState.cs` (nhiều lưỡi), `MatchSim.Collisions.cs`

## Implementation Steps
1. 10 trait + test công thức từng cấp.
2. Báo cáo cân bằng: với mỗi trait T, chạy trận ngẫu nhiên (vũ khí × trait khác × seed) có T vs không T → chênh lệch tỉ lệ thắng; song song.
3. Chỉnh TraitTuning tới khi mọi trait trong +3..+15 điểm (cấp 1) và cấp 2 > cấp 1.
4. Test ngưỡng hồi quy (như BalanceThresholdTests).

## Success Criteria
- [ ] 10 trait đúng công thức, cấp 2 mạnh hơn cấp 1.
- [ ] Mọi trait cấp 1: +3..+15 điểm; không combo 3 trait nào thắng > 85% trên toàn sân (lấy mẫu 500 combo).
- [ ] 4 vũ khí cũ vẫn 30–70% khi không trait.

## Risk Assessment
- Combo phá game (Vampire + Glass Cannon + Thorns…) → test combo lấy mẫu; giới hạn cộng dồn (vd tổng hồi máu tối đa).
- Twin Blade đổi giả định "1 lưỡi" khắp nơi → làm sớm trong phase, test kỹ parry 2 lưỡi.
