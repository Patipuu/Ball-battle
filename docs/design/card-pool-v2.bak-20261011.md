# Card Pool v2 — Run mode (đề xuất Game Designer, 11/10/2026)

Nguồn đối chiếu: `BallBattleUnity/Assets/Scripts` (bản thật; `Scripts/` ở gốc là bản sao giống từng byte). File chính: `Sim/Run/Card.cs`, `CardOffer.cs`, `RunTuning.cs`, `RunBuild.cs`, `Sim/Traits/*`, `Sim/MatchSim.Damage.cs`, `View/Run/PrepareView.cs`, `View/Run/CardText.cs`, `View/PixelFontData.cs`, `SimTests/RunBot.cs`, `TraitBalanceThresholdTests.cs`.
Ký hiệu: **[có sẵn]** = làm được bằng hook/tuning hiện có; **[hook mới]** = cần thêm API vào Sim; **[SimVer]** = đổi kết quả trận đang có → tăng `SimVersion.Rules` (hiện = 3); **[Save]** = đổi dữ liệu RunBuild → tăng `SaveData.CurrentVersion` (hiện = 1; bản cũ giữ tiến trình, bỏ run dở).

---

## 0. Ràng buộc UI thẻ (đo từ code)

- Font 3x5, `Advance = 4` px/ký tự ở scale 1 (`PixelFontData`), độ rộng chuỗi = `len*4-1`.
- Ký tự hợp lệ: `A-Z 0-9 space . - : / % + ! ?`. **Không có** dấu phẩy, ngoặc, `=`, `<`, `>`, `x` thường, dấu nháy. Dùng `X` cho "nhân", `S` cho giây (`3S`).
- Thẻ: `Rect(-125, bottom, 250, 36)`, chữ bắt đầu x = -117, nên dùng được khoảng 234 px.
  - Title scale 2 (8 px/ký tự): tối đa cứng **29 ký tự**.
  - Detail scale 1 (4 px/ký tự): tối đa cứng **58 ký tự**, hiện chỉ có **1 dòng** (PixelText không xuống dòng).
- **Đề xuất chuẩn:**
  - Title ≤ **18 ký tự**, để chừa ~40 px bên phải cho tag scale 1 (`RARE`, `1 FIGHT`, `LV2`).
  - Detail = **2 dòng, mỗi dòng ≤ 44 ký tự** (chuẩn), tối đa 52.
  - Bố cục mới, giữ chiều cao 36: title y+22 (cao 10 px), detail1 y+12, detail2 y+4. Không phải dời nút REROLL/FIGHT. Chỉ đổi View: thêm 1 PixelText cho mỗi thẻ, không đụng Sim.
- Khung ENEMY/YOU: chữ ở x = -74 tới mép 119, khoảng 193 px, tức **≤ 48 ký tự** ở scale 1.

## (a) Sửa text thẻ hiện có cho đúng code

| Card | Title (≤18) | Detail 1 | Detail 2 | Hành vi code đã kiểm |
|---|---|---|---|---|
| Heavy L1 | `HEAVY` | `+35% MAX HP  SIZE X1.2  PUSH -40%` | `BIGGER BODY IS EASIER TO HIT` | `HeavySpeedPct = {0,0}` nên **không chậm hơn**; text cũ "SLOWER" sai. Bán kính tối đa 24. Khối lượng va chạm ∝ r². |
| Heavy L2 | `HEAVY LV2` | `+70% MAX HP  SIZE X1.35  PUSH -60%` | `NEVER BIGGER THAN A BOSS` | Như trên. |
| Vampire | `VAMPIRE` / `LV2` | `HEAL 20% OF HIT DAMAGE` (LV2 `28%`) | `MELEE AND SHOTS. NOT POISON` | `OnHitDealt` chỉ chạy cho hit Weapon/Projectile. |
| Spiky | `SPIKY` | `BODY BUMP: 1 DMG. ONCE PER 2S` (LV2 `1.5`) | `IGNORES SHIELDS` | Reflect kind, cooldown 120 tick; Reflect không bị khiên chặn. |
| Thorns | `THORNS` | `RETURN 15% OF MELEE DMG TAKEN` (LV2 `22%`) | `NOT SHOTS OR POISON` | Chỉ `DamageKind.Weapon`, gồm cả đòn thân Brawler. |
| Second Wind | `SECOND WIND` | `ONCE: AT 30% HP HEAL 20` (LV2 `28`) | `OR SURVIVE A KO AT 20 HP` | `TryPreventDeath` đặt HP = 20/28, không phải +20. Đã kích ở 30% thì không còn cứu KO. |
| Glass Cannon | `GLASS CANNON` | `HITS X1.5 DMG  MAX HP -25` (LV2 `X1.9`, `-35`) | `MELEE AND SHOTS. MAX HP AT LEAST 20` | Nhân Weapon + Projectile; không nhân poison/reflect. |
| Parry Master | `PARRY MASTER` | `EVERY 2ND PARRY GROWS YOUR WEAPON` (LV2 `EVERY PARRY`) | `NEEDS A BLADE` | Gọi `Weapon.OnHit`. Với Rig thì parry **đặt turret**, với Volley thì **+1 tên**. |
| Bubble | `BUBBLE` | `SHIELD EVERY 25S. HOLDS 1` (LV2 `18S`) | `BLOCKS 1 HIT OR SHOT. NOT POISON` | Bộ đếm chỉ chạy khi không có khiên, nên khiên đầu tiên đến ở giây 25. |
| Twin Blade | `TWIN BLADE` | `2ND BLADE BEHIND  30% LONG` (LV2 `40%`) | `ALL MELEE HITS X0.45 DMG` | **Giấu ×0.45** (cả lưỡi chính). Chỉ nhân Weapon, **không nhân đạn** của Volley/Rig. |
| Poison Tip | `POISON TIP` | `HITS POISON 0.7/S FOR 3S` (LV2 `0.8/S 4S`) | `SKIPS IF TARGET HAS 1 POISON` (LV2 `2`) | Giới hạn tính stack **từ mọi nguồn**, nên gần như vô dụng nếu đối thủ đã trúng độc Venom. Đạn cũng gây độc. |
| DamageUp | `DAMAGE +15%` | `MELEE BODY AND SHOT HITS +15%` | `NOT POISON SPIKES OR THORNS` | `base*(1+DamagePct)` chỉ trong `ComputeHitDamage`. |
| SpeedUp | `SPEED CAP +10%` | `TOP SPEED +10%. START SPEED SAME` | `BRAWLER: MORE SPEED IS MORE DMG` | Nhân `(MaxSpeed + bonus)` trong Rules. |
| Swap | `SWAP TO <W>` | `NEW WEAPON. KEEPS TRAITS AND STATS` | `ONLY OFFERED IF ALL TRAITS STILL WORK` | `KeepsAllTraits`. |
| Heal | `HEAL 50%` | `HEAL 50% OF MAX HP NOW` | `HP CARRIES TO NEXT FIGHTS` | `SetHp(Hp + MaxHp*0.5)`. |
| MaxHp | `MAX HP +15` | `+15 MAX HP AND +15 HP NOW` | — | |

**Sửa màn Prepare (bug hiển thị HP):**
- `CardText.Stats` in `Build.Hp/MaxHp`, còn `enemyStats` in `enemy.MaxHp`. Cả hai bỏ qua spawn hook (Heavy +35/70%, Glass Cannon −25/−35, kể cả trên boss). Ví dụ Heavy L1 ra trận với 135/135 nhưng màn hình hiện 100/100.
- Thứ tự trait cũng ảnh hưởng: Heavy rồi GC khác GC rồi Heavy.
- Đề xuất: tính bằng một `MatchSim` khô (`ToLoadout()` cho cả hai bên + `ApplySpawnHooks()`, như `RunState.CreateMatch`) rồi đọc `Balls[i].Hp/MaxHp`. Hiện `HP 135/135`, dòng phụ `BASE 100`. Chỉ đổi View, không có [SimVer].
- **Bắt buộc trước thẻ counter:** Prepare phải hiện arena (`RunState.ArenaId` → `ArenaRegistry.Get(id).DisplayName`). Hiện grep `View/Run` không có ArenaId, dù plan Step 2 ghi "xem trước arena".

## (b) Thẻ mới

### Cấu trúc pool v2 và trọng số (thay hằng số trong `CardOffer`)
| Nhóm | Tổng trọng số | Ghi chú |
|---|---|---|
| Trait cốt lõi (10) | 6.0 (cũ 7.5) | chia đều như hiện tại |
| Weapon mod | 2.0 | chỉ mod của vũ khí đang cầm; ô riêng, tối đa 1 |
| Tradeoff | 1.5 | chiếm ô trait (tối đa 3); chỉ 1 cấp |
| Counter (1 FIGHT) | 2.0 | thẻ liên quan đối thủ/arena kế tiếp được ×2 trọng số |
| DMG / SPD | 1.5 / 1.0 (cũ 2 / 2) | thẻ nhạt, giảm tần suất |
| Swap | 1.5 | giữ nguyên |
| Heal / MaxHp | 3 hoặc 1 / 1 | giữ nguyên |

- Độ hiếm trong nhóm: Common ×1.0, Uncommon ×0.6, Rare ×0.3.
- Thẻ counter tăng trọng số dựa trên `RunState.Enemy` và `ArenaId`. Cả hai sinh từ seed, độc lập với lựa chọn người chơi, nên vẫn tất định và công bằng. Thẻ không gợi ý "tốt vs đối thủ"; việc đọc đối thủ là phần của người chơi.
- Cách cài đặt chung:
  - Mod/tradeoff/counter đều là `TraitRule` mới, nên dùng được mọi hook sẵn có.
  - `TraitRegistry.Entry` thêm `RequiredWeapon` (đối xứng `BannedWeapon`) và `MaxLevel`.
  - `RunBuild` thêm `WeaponMod` (string) và `FightOnly` (List<TraitSlot>, xoá trong `ApplyResult`). `ToLoadout()` gộp cả hai. **[Save]**
  - Thêm content không đổi trận cũ (Versus không trait vẫn khớp `VersusGoldenTests`), nên không cần [SimVer]. Nhưng hash phải gồm trait mới (TraitRule đã tự hash, và fingerprint phải gồm tuning mới).

### B1. Weapon mod (1/vũ khí; chiếm ô MOD riêng; mất khi Swap, thẻ Swap phải ghi `LOSES MOD`)
| Weapon | Card | Text (title / d1 / d2) | Hiệu ứng chính xác | Độ hiếm | Hook |
|---|---|---|---|---|---|
| blade | `KEEN EDGE` | `+0.25 DMG PER HIT YOU LAND` / `STACKS ALL FIGHT` | Trait đếm hit Weapon trong `OnHitDealt`; `ModifyOutgoingDamage` cộng `0.25*n` sau `DamagePct` | Uncommon | [có sẵn] |
| fang | `WOUND UP` | `START SPIN +6` / `PARRIES MORE OFTEN` | `OnSpawn: Self.Weapon.SpinDegPerTick += 6` (8→14; trần 40 nằm trong OnHit) | Common | [có sẵn] (field public) |
| pike | `LONG HAFT` | `START LENGTH +8` / `EASIER TO PARRY` | `BladeLength += 8` (26→34; trần 90 trong OnHit) | Common | [có sẵn] |
| brawler | `BULL RUSH` | `START TOP SPEED +1.5` / `WALL BOUNCE SPEED +50%` | `MaxSpeedBonus += 1.5` (−3→−1.5, trần +4 vẫn áp khi tăng); `WallSpeedBoost` 0.08→0.12 | Uncommon | [có sẵn]; cần kiểm combo Brawler (85.7%) |
| volley | `QUIVER` | `START WITH 3 ARROWS` / `+0.2 ARROW DMG` | `OnSpawn`: gọi `Weapon.OnHit` 2 lần (mỗi lần +1 tên, +0.1 dmg) | Common | [có sẵn] (OnHit là public virtual, Parry Master đã dùng) |
| venom | `VIRULENT` | `START POISON CAP 3` / `+0.5 MELEE DMG` | `OnHit` ×2 lúc spawn (MaxStacks 1→3, dmg 1.5→2.0) | Uncommon | [có sẵn] |
| aegis | `THICK PLATE` | `START WIDTH +2` / `+0.5 CHIP DMG` | `BladeThickness += 2` (7→9; chip = 0.7+(w−7)*0.25 = 1.2; trần 14) | Common | [có sẵn] |
| rig | `FIELD KIT` | `START WITH 2 TURRETS` / `PLACED WHERE YOU SPAWN` | `OnHit` ×2 lúc spawn: 2 turret ở vị trí spawn, melee 1.5→1.9 | Rare | [có sẵn]; 2 turret chồng một điểm (chấp nhận, ghi rõ) |

Mod thứ hai để sau, cần field theo instance: Volley `QUICK DRAW` (interval 90→70), Aegis `POLISH` (reflect 40→55%), Rig `OVERCLOCK` (68→54 tick). Hiện các số này là static trong `WeaponTuning`, nên phải chuyển sang field instance (mặc định = tuning, kết quả cũ không đổi). [hook mới nhỏ], không [SimVer].

### B2. Tradeoff (chiếm ô trait, 1 cấp)
| Card | Text | Hiệu ứng | Độ hiếm | Hook |
|---|---|---|---|---|
| `IRON SKIN` | `HITS AND SHOTS TAKEN -20%` / `TOP SPEED -15%` | `ModifyIncomingDamage` (Weapon/Projectile) ×0.8; `OnSpawn Bonus.SpeedPct -= 0.15` (giống cách Heavy dùng) | Common | [có sẵn] |
| `OVERDRIVE` | `TOP SPEED +25%` / `MAX HP -15%` | `SpeedPct += 0.25`; MaxHp và Hp ×0.85 (giữ tỉ lệ như GC) | Uncommon | [có sẵn]; có thể cấm Brawler nếu đo > ngưỡng |
| `BERSERK` | `BELOW 50% HP: HITS +30%` / `ABOVE 50% HP: HITS -15%` | `ModifyOutgoingDamage` đọc `Self.HpFraction` | Uncommon | [có sẵn] |
| `SLOW BURN` | `FIRST 20S: HITS -30%` / `AFTER 20S: HITS +30%` | đếm tick trong `OnTick` | Common | [có sẵn] |
| `GREED` | `+3 COINS NOW` / `NEXT 2 ENEMIES +15% HP` | Thẻ Run, không chiếm ô. `RunState.GreedFights = 2`; `CreateMatch` nhân MaxHp địch. Preview phải hiện HP đã nhân. | Rare | Run-only, [Save]. Nới nhẹ nguyên tắc "địch chỉ từ seed": hiển thị minh bạch. |

### B3. Counter (`1 FIGHT`, không chiếm ô, xoá sau trận)
| Card | Text | Hiệu ứng | Tín hiệu (×2 trọng số khi…) | Hook |
|---|---|---|---|---|
| `ANTIDOTE` | `POISON DMG TAKEN -60%` | `ModifyIncomingDamage` kind Status ×0.4 | địch Venom hoặc Poison Tip | [có sẵn] |
| `DEFLECTOR` | `SHOT DMG TAKEN -40%` | kind Projectile ×0.6 | địch Volley hoặc Rig | [có sẵn] |
| `ANCHOR` | `BODY HITS TAKEN -30%` / `PUSH -50%` | kind Weapon và `!attacker.Weapon.HasBlade` ×0.7; `KnockbackResist = max(cur, 0.5)` | địch Brawler hoặc Spiky | [có sẵn] |
| `DAMPENER` | `REFLECT DMG TAKEN -50%` | kind Reflect ×0.5 (gồm Aegis, Thorns, Spiky) | địch Aegis, Thorns hoặc Spiky | [có sẵn] |
| `GIANT SLAYER` | `HITS +25% VS BIG BALLS` | `ModifyOutgoingDamage` nếu `target.Radius > 16` | boss hoặc địch Heavy | [có sẵn] |
| `SPIKE BOOTS` | `ARENA DMG TAKEN -75%` | kind Hazard ×0.25 | arena spike-walls | [có sẵn] |
| `CLOSE QUARTERS` | `HITS +20% WHILE ARENA IS SMALL` | `Sim.Arena.Width ≤ 170` (Tight từ đầu, arena khác sau khi co) | arena tight | [có sẵn] |
| `WOUND` | `ENEMY HEALING -50%` | cần `ModifyIncomingHeal` trong `MatchSim.Heal` | địch Vampire, Second Wind hoặc Aegis (regen) | **[hook mới]**; không [SimVer] nếu mặc định trả nguyên |

Đánh đổi: counter chỉ mạnh 1 trận, còn lượt chọn miễn phí thì có thể lấy trait vĩnh viễn. Đó chính là quyết định "đọc đối thủ" cần có.

## (c) Sửa trait bẫy và trait quá mạnh

Số hiện tại lấy từ báo cáo Phase 3, đo trên 4 vũ khí cũ, 24 seed/bên. Số đề xuất **phải đo lại**; mục tiêu là khoảng chứ không phải dự đoán.

| Trait | Hiện tại (Δ L1/L2) | Đề xuất | Mục tiêu Δ |
|---|---|---|---|
| Heavy | r×1.2/1.35, +35/70% HP, push 40/60% (+3.7/+10.3) | r×**1.15/1.3**, **+50/+80%** HP, push **50/65%** | L1 +7..+11, L2 +13..+17 |
| Glass Cannon | ×1.5/1.9, −25/−35 HP (+3.7/+7.9) | **×1.6/2.0, −20/−30** | L1 +7..+10, L2 +12..+16 |
| Parry Master | mỗi 2/1 parry (+13.5/+22.6) | L1 giữ; L2 mỗi parry nhưng **cooldown 60 tick** (`ParryMasterCooldownTicks = {0, 60}`) | L2 +15..+18 |
| Poison Tip | 0.7/0.8 dps, 3/4 s, 1/2 stack (+10.9/+21.6) | L2 **0.75 dps × 3 s**, 2 stack (tổng/stack 3.2→2.25) | L2 +15..+18 |
| Twin Blade | dài 30/40%, ×0.45 (+9.1/+20.6) | L2 dài **35%**, giữ ×0.45 | L2 +15..+18 |

Lý do:
- Heavy và GC ở L1 nằm sát sàn 3 điểm. Người chơi chọn sẽ thấy "vô hình", tức thẻ bẫy.
- Heavy to ra là thành bia, nên phần thưởng phải nằm ở HP.
- L2 của 3 trait cần lưỡi vượt +20, trong khi trần L1 là +15.

Gate mới (`TraitBalanceThresholdTests`):
- L1 ∈ [+5, +13].
- L2 ∈ [L1+3, +18].
- Đo trên **cả 8 vũ khí** (hiện `BaseWeapons` chỉ có 4).
- Seeds 24→40 (mặc định của report).
- Combo 3 trait tệ nhất ≤ 80% (hiện 85.7%: Brawler + Vampire2 + SecondWind2 + Thorns2).
- Sửa TraitTuning là **[SimVer]** 3→4. Trận Versus không trait không đổi.
- Text cập nhật theo số mới, ví dụ Heavy L1: `+50% MAX HP  SIZE X1.15  PUSH -50%`.

## (d) Kế hoạch đo

**Bot mới trong `SimTests/RunBot.cs`:**
1. **Counter** (heuristic). Có bảng khắc chế đo trước:
   - ma trận vũ khí × vũ khí (`MatchupReport`);
   - vũ khí × arena (`ArenaBalanceReport`);
   - Δ trait theo vũ khí địch (mở rộng `TraitBalanceReport`).

   Điểm của thẻ = lợi ích ước lượng trước enemy + arena kế tiếp. Có thêm luật mua thẻ thứ 2 và reroll như Greedy.
2. **Lookahead** (trần kỹ năng). Với mỗi thẻ được mời, mô phỏng 16 trận build+thẻ vs `run.Enemy` trên `ArenaId`, dùng **seed khác FightSeed** (để không biết trước kết quả thật), rồi chọn thẻ thắng cao nhất.
   - Chi phí ước tính: khoảng 38 ms/trận (report Phase 3), tức ~2 s/lượt chọn, ~16 s/run. 800 run trên 16 luồng khoảng 15 phút. Đặt `Explicit`, không gate.

**Ngưỡng (800–2000 run/chính sách, seed cố định):**
| Chính sách | Hiện tại | Mục tiêu v2 |
|---|---|---|
| Random | 7.1% | 5–15% |
| Greedy | 35.8% | 30–45% |
| Counter | — | **≥ Greedy + 10 điểm** (chứng minh đọc đối thủ có giá trị) |
| Lookahead | — | ≤ 75% (nếu cao hơn thì game quá dễ "giải") |

**Chỉ số thẻ (log mỗi lượt chọn):**
- Pick rate theo thẻ: không thẻ nào < 2% hoặc > 25% khi được mời (với bot Counter).
- Win-rate-added: fight win% khi chọn so với khi không chọn. Không thẻ nào > +15 hoặc < 0.
- Độ nhạy quyết định: chênh win% giữa thẻ tốt nhất và tệ nhất trong cùng offer (Lookahead). Trung vị ≥ 8 điểm; nếu ~0 thì lựa chọn vô nghĩa.
- Counter khi đúng tín hiệu so với khi lệch tín hiệu: chênh ≥ 10 điểm.
- Bổ sung việc còn thiếu của Phase 7: HP trung bình trước mỗi boss, tỉ lệ "chết dần" (thua ≥ 2 trận liên tiếp sau boss).

**Thứ tự triển khai:**
1. Sửa text, layout 2 dòng, HP thật và arena ở Prepare (chỉ View).
2. Retune trait theo (c) + gate 8 vũ khí [SimVer].
3. Weapon mod + counter + tradeoff [Save].
4. Bot Counter/Lookahead, rồi chỉnh trọng số. Cuối cùng là playtest người thật (≥ 3 run).

Câu hỏi mở:
- Ô MOD riêng hay chung 3 ô trait?
- Có chấp nhận GREED làm địch phụ thuộc lựa chọn người chơi không?
