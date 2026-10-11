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
| Thorns | `THORNS` | `RETURN 15% OF MELEE DMG TAKEN` (LV2 `22%`; đề xuất 18% của 02:27 đã HUỶ ở 02:46) | `NOT SHOTS OR POISON` | Chỉ `DamageKind.Weapon`, gồm cả đòn thân Brawler. |
| Second Wind **(TEXT PENDING – xem P2.4)** | `SECOND WIND` | `ONCE: AT 30% HP HEAL 20` (LV2 `28`) | `OR SURVIVE A KO AT 20 HP` | `TryPreventDeath` đặt HP = 20/28, không phải +20. Đã kích ở 30% thì không còn cứu KO. |
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
| ~~Heavy~~ **HOLD (sau playtest)** | r×1.2/1.35, +35/70% HP, push 40/60% (+3.7/+10.3) | ~~r×1.15/1.3, +50/+80% HP, push 50/65%~~ → **giữ nguyên số**, xem "Cập nhật sau playtest" P2 | đo trong combo (bot-sweep-v4 G1) |
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

---

## Cập nhật sau playtest (11/10/2026, SimVersion 3, bản Linux)

> Mọi con số **mới** trong mục này là đề xuất **[chưa đo]**. Số "hiện tại" lấy từ code hoặc report đã có.
> Nguồn: ghi chú Game Tester và ảnh trong `/workspace/playtest/{run1,run2,run3,matchups,lossrule}/`.

### P1. Bằng chứng chính
| Playtest | Kết quả | Ý nghĩa thiết kế |
|---|---|---|
| Run 1: Brawler + Vampire + Heavy + Thorns | thắng 8/8, hạ 3/3 boss, không thua trận nào | Trục **sustain** (Heavy + Vampire + Thorns) áp đảo, dù bot đo Heavy L1 riêng lẻ chỉ +3.7 điểm |
| Run 1, ảnh `07_fight3_boss_volley_hp156_vs_prep115` | Prepare hiện HP 115, vào trận là 154 | Heavy nhân **cả MaxHp của Run** (gồm thẻ +15). Xác nhận lỗi hiển thị HP ở §a. |
| Run 2, ảnh `f6_boss_hp_mismatch` | boss Pike có Heavy: 297 HP trong trận (220 × 1.35) | Preview địch cũng sai HP |
| Run 2: Blade, không có sustain | 5/8, hạ 1/3 boss (WWWLWLWL); boss Brawler gây 48 HP trong ~12 s | Độ khó dồn vào boss |
| Run 3: Aegis | thua ở trận 6/8, hạ 1/3 boss. Phải bắt đầu lại 3–6 lần mới được mời Aegis | Xem boss-and-loss-rules §6 (trọng số vũ khí khởi đầu) |
| Versus: Venom vs Blade @ Spikes | Venom thắng 4/4, mỗi trận ~20 s; số sát thương tường "2" quá nhỏ, không đọc được | Outlier có thật (khớp report: 72%) |
| Versus: Aegis vs Blade @ Tight | 2–1, cân | Aegis 26% ở Tight nhiều khả năng do cặp với Venom và boss, không do Tight. Run 3 trận 6 là boss Venom ở sân nhỏ. |
| Versus: Fang vs Brawler | 2–1, ~45 s | Không cần sửa gấp |

### P2. Thay đổi đề xuất so với §c
1. **HOLD Heavy buff:** rút lại đề xuất Heavy ở §c, giữ số hiện tại (×1.2/1.35, +35/70%, push 40/60%).
   - Δ đo riêng lẻ (+3.7) đánh giá thấp Heavy, vì trong Run Heavy nhân MaxHp đã cộng thẻ và ghép với hồi máu.
   - Chỉ sửa text (bỏ "SLOWER").
2. **Nhắm vào combo sustain, không vào Heavy riêng lẻ:**
   - **Vampire: trần hồi mỗi hit** 2 HP (L1) / 3 HP (L2) **[chưa đo]**. Hook có sẵn: `OnHitDealt` → `Sim.Heal(Self, min(d*pct, cap))`. Text: `HEAL 20% OF HIT DMG  MAX 2 PER HIT`.
   - **Vampire vs boss: ×0.5 [chưa đo].** Sim chưa có cờ boss.
     - Dùng `target.Radius > 16` thì bắt nhầm cả địch Heavy.
     - Nên thêm `BallLoadout.IsBoss` (hash vào state) [hook mới nhỏ], lô v4. Không đổi Versus.
   - **Heavy × MaxHp của Run:**
     - Phương án 1: Heavy cộng **phẳng** +35/+70 HP thay vì nhân % **[chưa đo]**.
     - Phương án 2: giữ %, nhưng chỉ nhân trên 100 HP gốc.
     - Text tương ứng `+35 MAX HP`. Chọn bằng sweep (bot-sweep-v4 G1).
   - **Thorns:** giữ số (15/22%). Đo trong combo; Thorns ăn mọi đòn melee, gồm đòn thân Brawler (kind Weapon).
   - **Brawler riêng:**
     - Gate Brawler + Vampire (L1/L2) và Brawler + Heavy + Vampire + Thorns.
     - Nếu vượt ngưỡng: cấm Vampire L2 trên Brawler. Lưu ý `BannedWeapon` hiện chỉ nhận 1 id/trait (Glass Cannon đang dùng), nên phải đổi thành mảng nếu cần [code nhỏ].
     - Hoặc Vampire ×0.7 với đòn thân **[chưa đo]**.
3. **Venom:** ~~`VenomSeconds` 4 → **3** **[chưa đo]**, giữ `VenomDps` 1.~~ **ĐÃ HUỶ sau sweep 02:27: giữ Venom nguyên số, sửa arena Spikes** (xem mục cuối). Kiểm cộng dồn với tường gai:
   - Spike-walls gây 2 Hazard mỗi lần bật tường cho cả hai bên.
   - Cần tách sát thương theo nguồn trong trận Venom@Spikes (Status / Hazard / Weapon) để biết Venom thắng nhờ độc hay nhờ đối thủ đập tường nhiều hơn (Venom spin 4, chậm).
   - Gate: bot-sweep-v4 G2.
   - Đọc được: popup Hazard to hơn, có màu riêng; tường gai nháy khi gây sát thương (View).
4. **Second Wind — text PENDING:**
   - Code có **hai cửa kích hoạt**, dùng chung một cờ `used` (1 lần mỗi trận):
     1. `OnTick`: HP ≤ 30% max HP thì **hồi +20/+28**.
     2. `TryPreventDeath`: nhận đòn chí mạng khi chưa dùng thì **đặt HP = 20/28** (không phải cộng thêm).
   - Nghi bug từ playtest: không kích ở 9/135; hồi 28 dù là LV1. Đang kiểm bằng test.
   - **Giữ text cũ cho tới khi có kết quả.** Text dự kiến nếu code đúng: `ONCE: AT 30% HP HEAL 20` / `OR SURVIVE A KO AT 20 HP`.
5. **Reroll:** `RunState.CanReroll` yêu cầu `PicksTaken == 0` (thiết kế đúng), nhưng UI chỉ làm mờ nút `REROLL 1`.
   - Thêm dòng scale 1 dưới nút: `ONLY BEFORE FIRST PICK` (22 ký tự ≈ 87 px, vừa nút rộng 110).
   - Khi đã chọn thì nút hiện `REROLL LOCKED`.
6. **Đọc được (chuyển Art/View):**
   - Sát thương phản và parry đang hiện số xám mờ → magenta (255,90,200) như quy ước. Grep cho thấy `FxView` không có case cho `SimEventType.Damage`; dev cần kiểm số xám đến từ đâu.
   - Aegis lớn lên chỉ thấy qua số WIDTH → weapon-bible (MIRROR) cần hình lớn dần rõ.
   - Các lỗi tester ghi qua tên ảnh (chưa xác minh): `13b_fight7_started_without_card_pick_BUG`, `16_end_run_won_unlock_text_clipped`, `05_fight2_numbers_outside_arena`, `10_fight5_mirror_match_cant_tell_who_is_who`. Chuyển PM/dev.

### P3. Spec màn kết quả sau trận (post-fight result screen)

**Mục tiêu:** người chơi hiểu **vì sao thắng/thua** (SDT – năng lực; research Step 3 GĐ0) và nối sang thẻ counter.

**Phạm vi:** **chỉ View**. Gom từ `SimEvent` hiện có, không đổi Sim, không [SimVer]. Dòng gợi ý thẻ counter chỉ bật khi thẻ counter tồn tại (lô v4).

**Bố cục 270×480, font 3x5:**
```
        WIN  /  LOSE  /  DRAW          (scale 3)
        BY KO | BY TIME | BY ARENA     (scale 1)
                 YOU        FOE        (cột ~115 px ≈ 28 ký tự scale 1)
WEAPON DMG       41.0       22.5
SHOT DMG          0.0       12.0
POISON DMG        6.3        0.0
REFLECT DMG       4.1        0.0
ARENA DMG TAKEN   2.0        8.0
HEALED           18.4        0.0
HP LEFT        64/135        0/220
MAX DMG / LEN…      9         34      (StatLabel + đỉnh StatValue)
MOST DMG TAKEN: SHOTS 12     TRY: DEFLECTOR
         [ CONTINUE ]
```
(Số minh hoạ, không phải dữ liệu thật.)

**Nguồn dữ liệu (View tích luỹ trong mảng cố định, không GC mỗi frame; chỉ dựng chuỗi khi mở màn):**
| Dòng | Event |
|---|---|
| WEAPON DMG | `Hit` (A = attacker, Value) |
| SHOT DMG | `ProjectileHit` (A = owner, Value; 0 nếu khiên chặn) |
| POISON DMG | `StatusTick` (B = nguồn chính, Value) |
| REFLECT DMG | `Damage` với `Variant = Reflect` (A = nguồn: Thorns, Spiky, Aegis) |
| ARENA DMG TAKEN | `WallBounce.Value` + `ObstacleHit.Value` > 0 (Hazard, không có nguồn bóng) |
| HEALED | `Heal.Value` |
| HP LEFT | `Balls[i].Hp/MaxHp` lúc `MatchEnd` |
| MAX STAT | đỉnh `Weapon.StatValue` (đọc khi có `StatChanged`) |

**Lý do kết thúc:**
- `MatchEndReason.TimeCap` → `BY TIME`.
- `Knockout` → `BY KO`. Nhưng nếu sự kiện sát thương cuối cùng trước `Death` của bóng thua là Hazard (WallBounce/ObstacleHit Value > 0) thì **suy ra `BY ARENA` trong View**. Sim không có lý do này, và `LastAttacker` không đổi khi trúng Hazard.
- Hoà → `DRAW` (Run tính là thua).

**Giới hạn:** `Damage` event chỉ mang `DamageKind`, không mang id trait, nên chưa tách được Thorns / Spiky / Aegis. Muốn tách thì thêm id nguồn vào event (lô v4, [SimVer] chỉ nếu đổi hash; thêm field event thì không đổi kết quả).

**Dòng `MOST DMG TAKEN: <SOURCE>` (loại sát thương người chơi nhận nhiều nhất) và thẻ gợi ý:**
| Nguồn | Thẻ gợi ý |
|---|---|
| POISON | `ANTIDOTE` |
| SHOTS | `DEFLECTOR` |
| REFLECT | `DAMPENER` |
| ARENA | `SPIKE BOOTS` |
| WEAPON từ bóng không lưỡi | `ANCHOR` |
| WEAPON từ boss/Heavy | `GIANT SLAYER` |

Nếu đối thủ hồi > 20% max HP của nó **[chưa đo]**: gợi ý `WOUND`.

**Phạm vi chế độ:** Run trước (sau mỗi trận, trước Prepare). Versus sau mỗi ván Bo3 là bước sau.

### P4. Kế hoạch sweep
Xem `bot-sweep-v4.md`: gate cứng cho combo Heavy + Vampire + Thorns và Venom@Spikes.

## Cập nhật sau sweep 02:27 (11/10/2026)

**Nguồn:** `/workspace/ball-battle/simrunner/reports/` (`venom-arenas.md`, `arena-balance-250.md`, `brawler-combo.md`), sim .NET trên SimVersion 3.

### Venom — HUỶ nerf độc 4 s → 3 s; sửa arena Spikes
- **Ngoài Spikes, Venom không mạnh:**
  - vs Blade chỉ **39.5–43.7%** ở 6 arena còn lại (500 seed/bên);
  - TB vs 7 vũ khí: classic 45.2, pillar 45.4, low-gravity 49.3, wide 43.9.
  - Nerf độc sẽ làm Venom yếu ở mọi arena khác.
- **Outlier là bản thân Spikes:**
  - TB **73.3%**; vs từng vũ khí 70.1–78.6%.
  - Mỗi bóng mất **~80 HP vào tường** (đối thủ 77.5–83.4, Venom 73.8–82.9).
  - Trận trung vị **~17 s** (Venom vs Blade: 16.7 s, so với 61.7 s ở classic).
  - Đối thủ chỉ nhận ~6–7 sát thương vũ khí + ~11–13 độc. Trận được **quyết ở ~20 HP cuối**, nơi độc (sát thương theo thời gian, không cần chạm) chiếm ưu thế.
  - Không thấy cộng dồn đặc biệt giữa độc và tường (độc ở Spikes 11.6 so với 46.9 ở classic, chỉ vì trận ngắn).
- ~~**Đề xuất [chưa đo]:**~~ (**THẤT BẠI**, xem 02:46)
  - `ArenaTuning.SpikeWallDamage` 2 → **1** mỗi lần chạm, và/hoặc **giảm tốc độ bật tường** ở Spikes (giảm số lần chạm, hiện ~40 lần/bóng/trận).
  - **Mục tiêu:** trung vị trận Spikes **≥ 35 s**; Venom @ Spikes TB **≤ 62%**.
- **⚠ Mâu thuẫn cần kiểm:** report Phase 5 (`phase-05-arena-balance-report.md`) ghi sát thương tường 1 hoặc 3 **không** hạ được 72%. Nên sweep cả hai cần gạt (sát thương tường × tốc độ bật), không chỉ đổi 2 → 1.
- Thay đổi arena đổi kết quả sim, nên thuộc **[SimVer]** lô v4.

### Combo Heavy × Thorns — lõi thật của combo sustain
Brawler, 60 seed/bên mỗi (đối thủ, arena), TB 7 arena:

| Build | vs 8 vũ khí trơn | vs 16 build 3-trait ngẫu nhiên |
|---|---|---|
| Heavy2 + Vampire2 + Thorns2 | **95.9%** | **74.7%** |
| Heavy1 + Vampire1 + Thorns1 | 86.9% | 55.9% |
| Heavy2 + Thorns2 (không Vampire) | 91.3% | 61.4% |
| Heavy2 + Vampire2 | 85.1% | 62.5% |
| Vampire2 + Thorns2 | 81.1% | 47.5% |

- Bỏ Vampire vẫn còn 91%. Lõi là **Heavy × Thorns**: HP lớn × phản theo % sát thương nhận. Bóng càng nhiều máu càng ăn nhiều đòn, nên càng phản nhiều.
- **Đề xuất [chưa đo]:**
  - ~~**Heavy L2: +70% → +50% max HP.**~~ **HUỶ 02:46.** Bỏ trạng thái HOLD ở §c cho riêng con số HP L2; size/push giữ. Text `HEAVY LV2`: `+50% MAX HP  SIZE X1.35  PUSH -60%`.
  - ~~**Thorns L2: 22% → 18%.**~~ **HUỶ 02:46.** Text `RETURN 15% OF MELEE DMG TAKEN` (LV2 `18%`).
  - **Giữ** Vampire trần hồi mỗi hit (2/3 HP) và ×0.5 vs boss (mục "Cập nhật sau playtest").
- **Gate:** xem `bot-sweep-v4.md` G1b (Heavy2 + Thorns2 ≤ 75% vs build 3-trait ngẫu nhiên; combo L2 ≤ 70%).

## Cập nhật sau sweep 02:46 (11/10/2026)

**Nguồn:** `/workspace/ball-battle/simrunner/reports/v4-proposal-{A-spikes,B-combo,C-runs-traits}.md`.

### Spikes — tường gai "thu răng" rồi leo thang [chưa đo]
- **Đã thử và THẤT BẠI:**
  - Tường 2 → 1: Venom @ Spikes **tăng** lên 77.3% (từ 73.3), trung vị 28 s.
  - Lưới sát thương × độ nảy {2, 1} × {1, 0.85, 0.7}: không ô nào có sát thương tường ≥ 1 đạt mục tiêu. Venom 72.8–77.3%. Khi độ nảy < 1, Brawler sụp (3–18%) và Aegis/Rig > 62%.
  - Khớp report Phase 5 ("1 hoặc 3 không hạ được").
- **Nguyên nhân:** Venom gây sát thương **dồn đầu trận**: độc chạy ngay, không cần tích. Tường bào mòn đều hai bên, nên trận ngắn lại và cắt mất thời gian của vũ khí "lớn dần" (Blade, Brawler, Volley…).
- **Quyết định mới (Phú):**
  - N giây đầu tường **thu răng** (không gây sát thương).
  - Sau đó **1 sát thương mỗi lần chạm**, **+1 mỗi 20 s**, **tối đa 3**.
- **Sweep N = 6 / 10 / 15 s. Mục tiêu:**
  - Venom @ Spikes ≤ **62%**;
  - không vũ khí nào < **38%** hoặc > **62%** ở Spikes;
  - trung vị trận ≥ **35 s**.
- **Mốc tham chiếu (không phải đề xuất):** sát thương 0.25 cố định, độ nảy 1 cho Venom 60.0%, trung vị 46.4 s, các vũ khí 44.3–60.0%. Tức là tường gần như vô hại thì đạt, nhưng Spikes mất bản sắc. Thiết kế leo thang nhắm giữ được bản sắc ấy.
- **Cần code [SimVer]:** `ArenaLayout` có sát thương tường theo thời gian (N, bước 20 s, trần 3), đọc tick trận. Sim tất định nên không vấn đề.
- **Tín hiệu cho Art:**
  - Giai đoạn thu: răng gai thụt vào, màu tối/mờ.
  - Trước khi bật ~1 s: **nháy cảnh báo**.
  - Bật ra: răng nhô, sáng. Mỗi lần +1 sát thương thì nháy lại và răng dài thêm một nấc.
  - Popup Hazard theo card-pool-v2 (to hơn, màu riêng).
- **Giữ độc Venom 4 s.** Probe 3 s: Venom còn **30.3–44.0%** ở 6 arena khác (TB/arena; classic 31.6, wide 30.3) mà Spikes vẫn 68.9%.

### Combo — HUỶ đề xuất Heavy2 / Thorns2; gate đo vs build ngẫu nhiên
- **Đo vs build 3-trait ngẫu nhiên mới có nghĩa:** người chơi PvP và Run đều có trait. Trên số đang ship, Heavy2 + Thorns2 vs build ngẫu nhiên đã chỉ **61.4%**; con số 91.3% là vs vũ khí trơn.
- **Huỷ Heavy L2 +70% → +50%:**
  - Delta Heavy L2 tụt còn **+0.6 điểm** (từ +10.5), vì L1 = +4.9 nên L2 kém hơn L1.
  - Thành thẻ bẫy, vi phạm G4.
- **Huỷ Thorns L2 22% → 18%:** các mức 16/18/20% chỉ xê dịch combo vài điểm (H2 + V2 + T2 vs ngẫu nhiên: 62.9 / 63.9 / 65.1 khi Heavy +50%).
- **Giữ cho Run:** Vampire trần hồi mỗi hit (2/3 HP) và ×0.5 vs boss **[chưa đo]**.
- **PvP:** xử lý bằng ngân sách loadout và **tối đa 1 trait sustain** (`pvp-research.md` §3).
- **Gate:** combo L2 đầy đủ Brawler + Heavy2 + Vampire2 + Thorns2 ≤ **70%** vs 16 build 3-trait ngẫu nhiên (TB 7 arena), **sau khi** có trần Vampire. Hiện 74.7% chưa có trần.
- **Việc sau (follow-up):**
  - **Heavy: phạt tốc độ so với giá trị HP.** Code hiện có `TraitTuning.HeavySpeedPct = {0, 0}` (không chậm) và `HeavyKnockbackResist = {0.4, 0.6}`.
    - Nếu muốn Heavy có "giá" thật, cân một phạt tốc độ nhỏ so với lượng HP (ví dụ đổi một phần HP L2 lấy tốc độ −X%) [chưa đo].
    - L1 hiện chỉ +4.9 (dưới sàn G4 +5), L2 +10.5.
    - Đo riêng, không gộp vào lô combo.
- **Ghi chú G4 (số mốc, trait đơn lẻ, 4 vũ khí Step 1):**
  - Parry Master L2 **+24.0**, Poison Tip L2 **+21.4** vượt trần +18.
  - Twin Blade L2 +18.6, Second Wind L2 +18.3 sát trần.
  - Đưa vào lượt cân sau.

## Cập nhật 03:05 (11/10/2026)

### Spikes — tường leo thang N = 15 s (chờ duyệt)
Nguồn: `simrunner/reports/v4-spike-ramp.md`, `v4-spike-ramp-runs.md`, `v4-spike-ramp-combo.md`, `v4-n15-vampire.md`. Đo trên 28 cặp × 500 seed/bên; leo thang +1 mỗi 20 s, trần 3.

| N | Trung vị trận | Venom TB | Vũ khí khác | Ghi chú |
|---|---|---|---|---|
| 6 s | 29.6 s | 76.3% | 40.2–54.3 | trượt |
| 10 s | 32.5 s | 75.4% | 40.8–54.3 | trượt |
| **15 s** | **36.1 s** | **72.8%** | **41.4–54.5** | **chọn** |
| 40 s | 49.0 s | 59.6% | 45.2–55.9 | đạt mọi mục tiêu nhưng **bị loại**: tường vô hại gần hết trận; Run Greedy 45.7% |

> ⚠ **CẦN PHÚ DUYỆT:** chấp nhận Spikes là "sân nhà" của Venom (~72.8%) là **nới một mục tiêu cân bằng** (Venom @ Spikes ≤ 62% → không áp dụng). Chưa duyệt thì quyết định này chỉ là đề xuất.

- **Quyết định:** N = 15 s. Trung vị 36.1 s đạt ≥ 35 s; các vũ khí khác trong 38–62%; Venom ~72.8% được chấp nhận như **sân nhà của Venom**.
- **Run với N = 15** (luật thua cũ): Greedy 36.5 → **40.8%**, boss3 21.7 → **26.2%**, Random **6.7%**.
  - ⚠ Greedy 40.8% **vượt trần dải 25–40% khoảng 0.8 điểm**. Dev báo "trong dải"; cần Phú xác nhận chấp nhận, hoặc chờ gói v4 đầy đủ (trần Vampire, luật 1 lượt đánh lại) đo lại.
  - Các probe Vampire2 thấp hơn cho Greedy 35.7–39.9%.
- **Bù cho người chơi:**
  1. Preview arena (Prepare hiện arena).
  2. PvP: mỗi bên ban 1 arena.
  3. Run: **trận boss không bao giờ có địch Venom trên Spikes** [code: `EnemyGenerator`/seed arena].
  4. Đảm bảo có ANTIDOTE (dưới).

### ANTIDOTE — luật bảo đảm (chốt)
- **Bảo đảm:** trận kế là **Spikes** VÀ địch có **Venom hoặc Poison Tip** thì 3 thẻ mời **luôn có ANTIDOTE**. Các trận gặp Venom khác: giữ trọng số ×2 (counter).
- **Hiệu ứng:** `POISON DMG TAKEN -60%` (`ModifyIncomingDamage` kind Status ×0.4), **chỉ trận này**.
- **Là thẻ counter 1 trận, KHÔNG phải trait:**
  - không tính vào `RunTuning.MaxTraits` (3);
  - `StillApplies` luôn true (khác nhánh trait trong `RunState.StillApplies`, ~dòng 100);
  - chọn được làm lượt miễn phí; nếu là lượt thứ 2 thì tốn 3 xu như thẻ thường;
  - xoá sau trận.
- **Khoá:** bấm FIGHT thì mọi thẻ khoá, **không ngoại lệ** (kể cả ANTIDOTE).
- **Hình (Art 4 v3, `/workspace/art/art4_counter_v3.png`):**
  - viền **đứt nét xanh ngọc** (110,200,190), nhãn `1 FIGHT`;
  - dòng 2 `THIS FIGHT ONLY. NOT A TRAIT SLOT` (33 ký tự, trong giới hạn 44), chân thẻ `COUNTER` / `FREE PICK OK`;
  - trường hợp Spikes + Venom: **viền nhấp nháy**.
- **Code:** `CardOffer` thêm bước "chèn bắt buộc" sau khi rút. Thay một thẻ không phải Heal để vẫn tất định theo seed [lô v4, Save nếu lưu thẻ counter].

### Tín hiệu hình Spikes (Art, chỉ View)
- **5 trạng thái tường:** thu răng (0–15 s) → bật mức 1 → mức 2 (35 s) → mức 3 (55 s, trần).
- **Răng đang nạp đổi màu theo nấc:** (70,60,66) → (120,50,45) → (180,70,50).
- **Nhịp:**
  - xung 0.1 s mỗi 5 s;
  - **nháy vàng + tiếng "clink" 1 s trước mỗi lần tăng mức**.
- Mọi mốc tính theo **`Sim.ActiveTick`**, không theo thời gian thực, nên replay, tua và tốc độ ×2 đều khớp.

### Vampire và trần G4
- **Vampire2 28% → 24% / 22%:** combo L2 (Brawler + H2 + V2 + T2 vs 16 build ngẫu nhiên, 7 arena) còn **71.9% / 71.2%**, **trượt** gate ≤ 70%.
  - Chỉ ≤ 18% mới đạt (69.7%), nhưng khi đó Vampire2 delta (+10.3) thấp hơn Vampire1 (+11.0): L2 tệ hơn L1, không chấp nhận.
- **Trần hồi mỗi hit CHƯA có trong code.** `VampireTrait` hồi damage×pct; `Sim.Heal` chỉ kẹp MaxHp. Đây là **việc code lô v4**; gate combo đo lại sau khi có trần.
- **Còn chờ:** lưới Vampire × Thorns.
- **Vượt trần G4 (+18), cân lại trong v4 theo §c pool v2:**
  - Parry Master L2 +24.0
  - Poison Tip L2 +21.4
  - Twin Blade L2 +18.6
  - Second Wind L2 +18.3
