# Boss & luật thua — Run mode (Game Designer, 11/10/2026)

## Quyết định đã chốt (Phú, 11/10/2026)

| Mục | Chốt |
|---|---|
| Độ dài run | 8 trận (giữ `RunTuning.Fights = 8`) |
| Boss | **Miniboss SHELL KING ở trận 4**, **boss cuối MIMIC ở trận 8**. **Bỏ WARDEN**; "sân co sớm" chuyển thành một biến thể arena. |
| Mạng | **Bỏ 3 mạng.** Cả run có **đúng 1 lượt đánh lại**, dùng ở bất kỳ trận nào. |
| Đánh lại | Đánh lại **ngay trận vừa thua** với **60% max HP** [chưa đo — chờ sweep 40/50/60%], **giữ nguyên đối thủ và arena**. |
| Thua lần thứ hai | Hết run. Hoà vẫn tính là thua, như code hiện tại. |
| Thắng trận | +3 xu, giữ HP theo tỉ lệ, hồi 35% max HP (`WinHealPct`). **Không có sàn HP** (cập nhật sau sweep 02:27). ~~Sàn 60% max HP~~ chuyển xuống **phương án dự phòng**, xem mục cuối. |
| Thắng run | Thắng trận 8, kể cả sau khi dùng lượt đánh lại ở trận 8. |
| MIMIC | Cần **hook mới `OnDamageTaken(kind, amount)`** và **event "thích nghi"** mang `DamageKind` trong `Variant`. |
| Màu MIMIC | 20 s đầu xám tối **(60,60,72)**. Sau đó lấy màu số sát thương của loại đã thích nghi: độc = xanh lá (lime 96,255,64 như `StatusView`/`FxView`), phản = magenta **(255,90,200)**, vũ khí = xanh thép **(120,150,200)**. |
| Lô thay đổi | Tất cả nằm trong **lô v4**, chỉ merge **sau khi Game Tester xong mục 1 và 4** của playtest trên SimVersion 3. |

**Các phương án A/B/C ở §3 đã bị THAY THẾ** bởi quyết định này. Giữ lại chỉ để tham khảo lý do.

**Ngưỡng sweep của bot (§3 "Sweep cần chạy") VẪN ÁP DỤNG** cho luật mới, kèm các chỉnh sau:
- Bỏ chỉ số "% run thắng đã thua ≥ 1 boss".
- Thêm "% run dùng lượt đánh lại" và "lượt đánh lại được dùng ở trận nào".
- Thêm "tỉ lệ thắng của lượt đánh lại". Mục tiêu 35–60%: thấp hơn thì lượt đánh lại vô nghĩa, cao hơn thì thua lần đầu không đau.
- "Phân bố trận làm hết mạng" đổi thành "phân bố trận kết thúc run". Ngưỡng giữ nguyên: trận 1–2 ≤ 10%, trận boss ≥ 50%, không trận nào > 35%.
- Giữ nguyên: Random 3–12%, Greedy 25–40%, Counter ≥ Greedy + 10 điểm, HP trung vị trước boss 50–80%.

### §PT. Bằng chứng playtest (SimVersion 3, luật cũ: 3 mạng, thua hồi đầy) ủng hộ luật mới

> Nguồn: ghi chú Game Tester, ảnh `/workspace/playtest/`. Số mới trong mục này là **[chưa đo]** bằng bot.

- **Ưu đãi ngược (playtest mục 2):**
  - Thắng sát nút thì vào boss với 65 HP; thua thì được hồi về 100. **Thắng tệ hơn thua.**
  - Luật mới vẫn còn ưu đãi ngược nếu không sửa: thắng ở 10% HP chỉ còn 10% + 35% = 45%, trong khi đánh lại bắt đầu ở 60%.
  - ~~**Sửa:** sau khi thắng, `HP = max(HP + 0.35*MaxHp, 0.60*MaxHp)`.~~ **(Đã chuyển thành phương án dự phòng sau sweep 02:27.)** Sàn bằng `RetryHealPct`, nên thắng không bao giờ tệ hơn đánh lại.
  - Code: `RunState.ApplyResult` khối `if (won)` (~dòng 170–174): `Build.SetHp(Math.Max(frac*MaxHp + MaxHp*WinHealPct, MaxHp*RetryHealPct))`.
  - Sàn và trần đều là static field để sweep. Thuộc lô v4, [Save] cùng đợt.
- **Thua boss không có thêm đau:** luật cũ chỉ −1 mạng như trận thường.
  - Run 2 (Blade): WWWLWLWL. Thua ở trận 4, 6 (boss), 8 (boss). Hạ 1/3 boss mà vẫn đi hết run.
  - Độ khó dồn vào boss: boss Brawler gây 48 HP trong ~12 s.
- **Giá trị thẻ Heal/MaxHp phụ thuộc số mạng:** còn ≥ 2 mạng thì bị bỏ qua (thua là hồi đầy); chỉ ở **mạng cuối** mới thành thẻ giá trị nhất (`lossrule/f5_cards_heal_maxhp`: LIVES 1, HP 55/100). Luật 1 lượt đánh lại làm HP luôn có giá trị.
- **Không sửa được bằng luật thua:** Run 1 (Brawler + Vampire + Heavy + Thorns) thắng 8/8, không thua trận nào. Combo sustain phải sửa ở card-pool-v2 (P2), gate ở `bot-sweep-v4.md` G1.
- **Hiển thị boss:**
  - HP preview sai: boss Pike có Heavy hiện 220 ở Prepare nhưng vào trận là 297 (`run2/f6_boss_hp_mismatch`).
  - Boss trông "không khổng lồ" khi người chơi có Heavy (`run1/15_fight8_boss_aegis_not_giant`), vì Heavy của người chơi đã làm bóng to ra (L1 r19.2, L2 r21.6) trong khi boss chỉ r24.
  - Đề xuất Art: sprite boss native 48×48 + vương miện/viền vàng WallWarning, không chỉ dựa vào kích thước.
- **Aegis 26% @ Tight:** Versus Aegis vs Blade @ Tight 2–1 (cân). Run 3 thua ở trận 6 là boss **Venom** trên sân nhỏ. Nguyên nhân nhiều khả năng là cặp Venom/boss, không phải Tight. Đo tách trong bot-sweep-v4.

### §6. Trọng số vũ khí khởi đầu (mới, lô v4)

**Hiện trạng code:**
- `RunState.StartChoices(seed, unlocked)` bốc ngẫu nhiên đều 3 vũ khí trong số đã mở. `RunFlow.NewRun` gọi với seed mới mỗi lần.
- Với 8 vũ khí mở, xác suất một vũ khí **không** có mặt trong một lần mời = 5/8.
- Playtest Run 3 phải bắt đầu lại 3–6 lần mới gặp Aegis, trong khi Blade xuất hiện 4/4 và Pike 3/4.

**Đề xuất [chưa đo]:**
1. **Ưu tiên vũ khí đã mở nhưng chưa chơi:** trọng số **3** cho vũ khí chưa từng được chọn bắt đầu run, **1** cho phần còn lại. Bốc có trọng số, không lặp.
2. **Bảo đảm xuất hiện:** mỗi vũ khí đã mở có mặt **ít nhất 1 lần trong mỗi N = 3 lần mời** (8 vũ khí, 3 ô × 3 lần = 9 ≥ 8 nên khả thi).
   - Nếu có vũ khí đã vắng 2 lần mời liên tiếp, ép nó vào ô đầu.
   - Nhiều vũ khí cùng tới hạn thì ưu tiên vắng lâu nhất, rồi theo thứ tự registry (tất định).
3. **Đếm theo lần mời, không theo run đã chơi**, để bấm bắt đầu lại cũng tính.

**Tác động code và save (lô v4):**
- `ProgressData` thêm `WeaponsStarted` (List<string>) và `OffersSinceSeen` (map id → int; lưu dạng 2 list cho JsonUtility). Thuộc **[Save]** `SaveData.CurrentVersion` 1→2, cùng đợt với cờ retry.
- `StartChoices` cần thêm progress làm đầu vào. `RunState.Start` đang **kiểm lại** `StartChoices(seed, weapons).Contains(weaponId)`; nếu progress đổi giữa lúc mời và lúc chọn thì kiểm sai.
  - Đề xuất: lưu bộ 3 lựa chọn đã mời vào `SaveData`, cập nhật bộ đếm ngay khi mời, và `Start` kiểm theo danh sách đã lưu.
- Không đổi MatchSim, nên không [SimVer].
- Metric trong bot-sweep-v4 G6.

**Ngưỡng sweep vẫn áp dụng:** mọi thay đổi ở §PT và §6 được đo bằng các dải ở "Quyết định đã chốt" và `bot-sweep-v4.md`.

### Tác động code (đã kiểm trong `BallBattleUnity/Assets/Scripts/Sim/Run`)

1. **`RunTuning.BossFights` `{2,5,7}` → `{3,7}`.**
   - Đã kiểm: index **0-based**. `FightIndex` bắt đầu 0, `last = FightIndex == Fights - 1`, comment "fights 3, 6, 8" ứng với `{2,5,7}`. Vậy trận 4 = 3, trận 8 = 7. Đúng.
   - Hệ quả phải sửa cùng lô:
     - `UnlockRules.Record` dùng `BossFights[0]`, `[1]`, `[2]`. Với 2 phần tử, `[2]` sẽ **ném IndexOutOfRange**. Cần map lại thành tựu: boss1 = Shell King, boss2 = Mimic. Phần thưởng cũ của `Boss3` (Venom + Parry Master) phải chuyển sang thành tựu khác, ví dụ `win1`. **Cần Phú chốt.**
     - `BossHp` và `BossTraits` đang tra theo **stage** (`StageOf`). Trận 4 (index 3) thuộc stage 1, nên dùng `BossHp[1] = 220` và 1 trait. Trận 8 thuộc stage 2: 300 HP, 2 trait. Cần chốt lại số qua sweep; nên đổi thành mảng tra theo thứ tự boss thay vì stage.
     - `RunBotReport` in histogram 0..3 boss; `RunDifficultyThresholdTests` có ngưỡng "giant boss" cuối. Cả hai cập nhật theo 2 boss.
2. **`RunTuning.Lives = 3` → cờ đánh lại.**
   - `RunTuning.Retries = 1`; `RunState.Lives` đổi thành `RetryAvailable` (bool) và `IsRetry` (bool).
   - `RetryHealPct = 0.6f` để là static field cho sweep.
   - View: `PrepareView` đang in `LIVES {n}`; `RunOverlayView` có text "DRAW -1 LIFE". Đổi thành `RETRY: 1` / `RETRY USED` và `LOST - RETRY AT 60% HP`.
3. **`RunState.ApplyResult`, dòng ~176–179** (khối `else { Coins += LossCoins; Lives--; Build.Hp = Build.MaxHp; }`) và điều kiện kết thúc ~189 (`last || Lives <= 0`):
   - **Thua, còn lượt:** `RetryAvailable = false`, `IsRetry = true`, `Build.SetHp(MaxHp * 0.6f)`, **không tăng `FightIndex`**. Ghi `FightRecord` với cờ retry.
   - **Thua, hết lượt:** `Over = true`, `Won = false`.
   - **Thắng:** như cũ, và `IsRetry = false`.
   - Xu khi thua: chưa chốt, đề xuất giữ +1 (`LossCoins`).
4. **Bẫy tất định:** `ArenaId = ArenaRegistry.ForSeed(FightSeed)` và trận dùng chính `FightSeed`. Nếu giữ `FightSeed` khi đánh lại, trận chỉ khác nhau ở HP và thẻ mới, nên rất dễ thua y hệt.
   - Đề xuất: **tách seed arena và seed trận**. `ArenaId` vẫn tính từ `Derive(Seed, 3000+FightIndex, 0)`, để giữ arena như Phú chốt. Trận đánh lại dùng `Derive(Seed, 3000+FightIndex, 1)`.
   - `Enemy` vốn chỉ phụ thuộc `(Seed, FightIndex)` nên tự giữ nguyên.
   - **Cần Phú xác nhận** việc dùng seed trận mới (đối thủ và arena vẫn giữ).
5. **Lượt chọn thẻ trước khi đánh lại:** chưa chốt. `CardOffer.Generate(Seed, FightIndex, RerollCount, …)` không đổi `FightIndex`, nên nếu reset `RerollCount` thì **bộ thẻ sẽ lặp lại y hệt**. Đề xuất: cho 1 lượt chọn miễn phí, offer seed thêm cờ retry. **Cần Phú chốt.**
6. **WARDEN thành biến thể arena:**
   - Thêm `closing` (co từ 30 s) vào `ArenaRegistry`; chỉ đổi `ShrinkStartTick`, không phải luật mới.
   - **Lưu ý:** `ForSeed` = `seed % All.Length`. Thêm arena thứ 8 sẽ đổi arena của mọi run seed, nên phải nằm trong lô có bump save.
7. **`SaveData.CurrentVersion` 1 → 2:** `RunState` đổi field (Lives → cờ retry), nên run dở bị bỏ, tiến trình giữ (luật SaveStore hiện có).
8. **`SimVersion.Rules` 3 → 4:** do hook `OnDamageTaken` + event mới của MIMIC (cùng lô với retune trait của card-pool-v2).
9. Tất cả các mục trên vào **lô v4**, merge sau khi Game Tester xong mục 1 và 4 trên SimVersion 3.

---

**Nguồn code:** `BallBattleUnity/Assets/Scripts/Sim/Run/{RunState,RunTuning,EnemyGenerator,UnlockRules}.cs`, `Sim/MatchConfig.cs`, `Sim/MatchSim.Damage.cs`, `Sim/Traits/*`, `SimTests/{RunBot,RunBotReport,RunDifficultySweep}.cs`.

**Liên quan:** `card-pool-v2.md` (thẻ counter, retune trait, bot Counter/Lookahead), `weapon-bible.md` (tên và màu).

**Ký hiệu:**
- **[có sẵn]** = làm được bằng hook hoặc config hiện có.
- **[hook mới]** = cần thêm API vào Sim.
- **[SimVer]** = đổi kết quả trận với loadout đã có, phải tăng `SimVersion.Rules` (hiện = 3).
- **[Save]** = đổi dữ liệu hoặc luật Run, phải tăng `SaveData.CurrentVersion` (hiện = 1; bản cũ giữ tiến trình, bỏ run dở).

---

## 1. Hiện trạng (số lấy từ code)

| Mục | Giá trị | Nơi |
|---|---|---|
| Số trận | 8 (`Fights`), chia stage 0/1/2 = trận 1–3 / 4–6 / 7–8 | `RunTuning.StageOf` |
| Boss | trận 3/6/8 (`BossFights = {2,5,7}`), bán kính 24 (thường 16), HP **160/220/300**, trait **0/1/2** | `RunTuning`, `EnemyGenerator` |
| Địch thường | trait 0 / 0–1 / 1–2; HP +0/+0/+10%; xác suất trait cấp 2: 0/25/50% | `RunTuning` |
| Vũ khí địch và boss | ngẫu nhiên đều trong cả 8 vũ khí (kể cả chưa mở khoá). **Boss không có cơ chế riêng.** | `EnemyGenerator.Generate` |
| Mạng | 3 | `Lives` |
| Thắng | +3 xu; giữ HP theo tỉ lệ cuối trận rồi + **35%** max HP (`WinHealPct`) | `RunState.ApplyResult` |
| Thua hoặc hoà | −1 mạng, +1 xu, **hồi đầy HP**, run **vẫn đi tiếp** sang trận sau (không đánh lại) | như trên |
| Kết thúc run | sau trận 8, hoặc khi hết mạng. **Thắng run = thắng trận 8**, dù có thua boss 1 hoặc 2. | như trên |
| Thẻ hồi máu | Heal 50% max HP (trọng số 3 nếu HP < 60%, ngược lại 1, chỉ khi đang mất máu); MaxHp +15 (trọng số 1) | `CardOffer` |
| Bot (2000 run, phase 7) | Random **7.1%**, Greedy **35.8%** | report phase 7 |
| Số boss hạ được 0/1/2/3 | Random 36.9 / 41.2 / 18.7 / 3.3%; Greedy 14.8 / 31 / 32.8 / 21.5% | report phase 7 |

**Suy ra từ số trên:** Greedy thắng run 35.8% nhưng chỉ 21.5% số run hạ đủ 3 boss. Vậy khoảng **14 điểm %** run thắng (khoảng 40% số run thắng) đã **thua ít nhất 1 boss** mà vẫn về đích.

## 2. Vấn đề

1. **Thua được hồi đầy HP.**
   - Sau một trận thua, HP luôn đầy. Thẻ Heal và MaxHp chỉ có giá trị trong chuỗi thắng, và giá trị giảm ngay sau mỗi trận thua.
   - Greedy bot ưu tiên Heal (điểm 10) chỉ khi HP < 50%, nên không đo được việc này.
   - Thua thậm chí "tốt" hơn thắng sát nút với 5% HP: sau khi thắng chỉ còn 5% + 35% = 40% HP.
2. **Thua boss không đau.**
   - Thua boss tốn đúng 1 mạng, bằng thua trận thường, rồi đi tiếp.
   - Boss không phải "cửa ải". Unlock (boss1/boss2/boss3) là phần thưởng duy nhất phân biệt.
3. **Boss chỉ là bóng to.**
   - Bán kính 24 + HP + trait ngẫu nhiên. Không dạy hay kiểm tra kỹ năng gì.
   - Bán kính to còn là bất lợi kép: dễ trúng hơn và kích thước sprite không nguyên lần (report phase 7 ghi lỗi lệch pixel).
4. **Bot chưa đo được hậu quả.** `RunBot.Result` chỉ có `Won / FightsWon / BossesDefeated`. Chưa có HP trước boss, trận gây kết thúc run, hay chỉ số "chết dần" (yêu cầu của plan Phase 7).

## 3. Phương án luật thua — ĐÃ THAY THẾ (xem "Quyết định đã chốt")

> A/B/C dưới đây không còn hiệu lực; giữ để tham khảo. Ngưỡng sweep ở cuối mục này **vẫn áp dụng**, kèm chỉnh sửa ở phần chốt.

Mọi phương án chỉ đổi `RunState.ApplyResult` (code Run) nên **không [SimVer]** (MatchSim không đổi), nhưng có **[Save]** và đổi `RunDifficultyThresholdTests`. Các mức tác động bên dưới là **giả thuyết cần sweep**, chưa đo.

### A. Hồi một phần khi thua *(đã thay thế)*
- **Luật:** thua hoặc hoà thì −1 mạng, +1 xu, HP = **50%** max HP (thay vì 100%).
- **Kỳ vọng độ khó:** khó hơn vừa phải; trận ngay sau một trận thua nguy hiểm hơn.
- **Giá trị thẻ:** Heal (50%) và MaxHp tăng giá rõ. WinHealPct có thể giữ 0.35.
- **Rủi ro:** vòng xoáy "chết dần" (thua → HP thấp → thua tiếp). Phải đo chỉ số này.
- **Tham số sweep:** `LossHealPct` ∈ {0.4, 0.5, 0.6, 0.75}.

### B. Boss là cửa ải *(đã thay thế)*
- **Luật:** thua trận thường vẫn như cũ (hoặc theo A). **Thua boss thì không qua cửa:** −1 mạng, HP = 60%, **đánh lại boss đó**.
  - Lần đánh lại dùng seed mới `Derive(seed, 3000+fight, retry)`; arena có thể khác, enemy giữ nguyên để preview không đổi.
  - Được 1 lượt chọn thẻ mới trước khi đánh lại.
- **Kỳ vọng độ khó:** run thắng phải hạ đủ 3 boss. Tỉ lệ thắng Greedy có thể rơi gần mức "hạ đủ 3 boss" hiện tại (21.5%) trừ khi giảm HP boss. Cần bù bằng sweep `BossHp`.
- **Giá trị thẻ:** thẻ counter (card-pool-v2 B3) đắt giá trước boss; reroll và mua thẻ thứ 2 có lý do.
- **Rủi ro:** kẹt ở một boss khắc chế cứng (ví dụ Aegis ở tight 26%) → cần arena/seed mới mỗi lần đánh lại (đã có ở trên).
- **Tham số:** `BossRetryHealPct` ∈ {0.5, 0.6, 1.0}, `BossHp` scale ∈ {0.8, 0.9, 1.0}.

### C. Vết sẹo *(đã thay thế)*
- **Luật:** thua thì −1 mạng, +1 xu, hồi đầy nhưng **MaxHp −10 vĩnh viễn** (sàn 60). Thua boss thì −15.
- **Kỳ vọng độ khó:** khó tăng nhẹ và tích luỹ. Thua sớm ảnh hưởng cả run nhưng không gây vòng xoáy ngay.
- **Giá trị thẻ:** MaxHp +15 thành "chữa sẹo"; Heal giá trị như cũ (vẫn thấp).
- **Rủi ro:** đọc khó hơn A trên UI 3x5. Cần hiện `MAX HP 90/100` ở Prepare.
- **Tham số:** sẹo ∈ {5, 10, 15}.

~~**Khuyến nghị thử trước:** **B + A**~~ (đã thay thế) (thua thường 50–60%, boss là cửa ải). Combo này xử lý cả 2 vấn đề. C là dự phòng nếu A gây "chết dần" quá cao.

### Sweep cần chạy (mở rộng `RunDifficultySweep`, `Explicit`)

**Bot:**
- Random, Greedy (hiện có).
- **Counter** (card-pool-v2 §d). Cần có trước khi chốt, vì luật thua mạnh tay hơn sẽ thưởng việc đọc đối thủ.
- Greedy cần thêm logic: Heal khi HP < 60% trước boss.

**Instrument `RunBot.Result`:** HP% trước mỗi boss, index trận làm hết mạng, số lần đánh lại boss (B), MaxHp cuối run (C).

**Quy mô:** 2000 run mỗi chính sách mỗi cấu hình, seed cố định như phase 7.

| Chỉ số | Mục tiêu |
|---|---|
| Thắng run: Random / Greedy / Counter | 3–12% / 25–40% / ≥ Greedy + 10 điểm |
| % run thắng mà đã thua ít nhất 1 boss | ≤ 10% với A/C; 0% theo định nghĩa với B (thay bằng: số lần đánh lại trung vị ≤ 1) |
| HP trung vị trước boss (Greedy) | 50–80% (thấp hơn thì boss là xổ số; cao hơn thì Heal vô nghĩa) |
| Phân bố trận làm hết mạng | trận 1–2 ≤ 10%; các trận boss chiếm ≥ 50%; không trận nào > 35% |
| "Chết dần" (thua ≥ 2 trận liên tiếp sau boss) | ≤ 15% số run (Greedy) |
| Pick rate Heal/MaxHp (Counter bot) | tăng so với hiện trạng; mỗi thẻ ≤ 25% khi được mời (đồng bộ card-pool-v2) |

**Chọn phương án:** cấu hình đạt nhiều ngưỡng nhất, ưu tiên khoảng Greedy/Counter và phân bố trận làm hết mạng. Ra quyết định cuối sau playtest người thật.

## 4. Ba boss có cơ chế riêng

**Nguyên tắc:**
- Boss vẫn r24 với HP theo `BossHp`, vũ khí vẫn random theo seed (preview công bằng).
- Thêm **1 trait boss cố định theo vị trí** (không nằm trong pool, không hiện ở thẻ). Preview ghi tên boss + 1 dòng gợi ý.
- Cài bằng `TraitRule` mới, gắn trong `EnemyGenerator` khi `IsBoss`.
- Vì chỉ áp cho trận boss trong Run, Versus và mọi loadout cũ không đổi, nên **không [SimVer]**; fingerprint/hash phải gồm tuning mới. Có **[Save]** (run dở sẽ gặp boss khác), và phải đo lại `RunDifficultyThresholdTests`.

### Miniboss (trận 4, index 3): **SHELL KING / Vua Vỏ Sò** — GIỮ
- **Dạy:** sát thương có nhiều loại; khiên chặn trọn một đòn.
- **Cơ chế:**
  - Bắt đầu với **3 khiên** (`Sim.AddShield(Self, 3)` trong `OnSpawn`).
  - Hồi 1 khiên mỗi **12 s** khi đang không có khiên, chỉ khi HP > 50% (đếm trong `OnTick`).
  - Khiên chặn Weapon/Projectile và **không cho vũ khí người chơi lớn lên** (pipeline hiện tại). Độc (Status), Reflect và Hazard xuyên khiên.
- **Lời giải người chơi học:** đánh nhiều hit nhỏ (Fang/MANTIS, Volley/SPARKLER), Poison Tip, Spiky, Thorns. Cẩn thận với Blade/Pike vì chậm lớn.
- **Hook:** [có sẵn] (`AddShield`, `OnTick`, `OnSpawn`, `Self.Status.ShieldCharges`).
- **Tín hiệu hình ảnh (Art):**
  - Vòng trắng dày quanh boss; `StatusView` đã vẽ vòng thứ 2 khi có ≥ 2 khiên, cần thêm vòng 3.
  - Vỏ sò sáng lên 1 s trước khi khiên hồi.
  - Khi khiên vỡ: mảnh trắng bắn ra + âm "clink".
  - Preview: `BOSS: SHELL KING  SHIELDS 3`.

### ~~Boss 2 (trận 6): WARDEN / Cai Ngục~~ — BỎ

> Đã bỏ theo quyết định. Cơ chế "sân co từ 30 s" chuyển thành biến thể arena `closing` (mục 6 phần chốt). Nội dung dưới chỉ để tham khảo.

- **Dạy:** đọc arena; sân co làm đổi thế trận.
- **Cơ chế:** sân bắt đầu co từ **30 s** thay vì 90 s (`MatchConfig.ShrinkStartTick = 30*60`), co trong 30 s như cũ về 110x110 (Tight: 90x90).
  - Chỉ đổi config cho trận boss trong `RunState.CreateMatch`.
  - Các vũ khí sẵn yếu ở sân nhỏ bị phạt: Aegis 26.2% ở tight, Volley 37.4% (ArenaBalanceReport). Turret Rig bị sân nuốt sớm.
  - Thẻ `CLOSE QUARTERS` (card-pool-v2) và thẻ Swap có giá trị.
- **Hook:** [có sẵn]. Đây là config mutable, không phải luật mới; trận không phải boss giữ nguyên.
- **Ràng buộc:** boss r24 + Heavy vẫn vừa sân 110 (đã có cap `HeavyMaxRadius = 24`). Cần test trường hợp tight 90x90 với boss r24 + người chơi Heavy r24.
- **Tín hiệu hình ảnh:**
  - Tường mang màu WallWarning ngay từ countdown (dùng lại hiệu ứng cảnh báo co của `ArenaFrameView`).
  - Boss đeo "chùm chìa khoá"; mỗi giây co sân có tiếng xích.
  - Preview: `BOSS: WARDEN  WALLS CLOSE AT 30S` + tên arena (yêu cầu Prepare hiện arena, card-pool-v2 §a).

### Boss cuối (trận 8, index 7): **MIMIC / Kẻ Bắt Chước** — GIỮ, cập nhật theo chốt
- **Dạy:** build một nguồn sát thương sẽ bị trừng phạt; build lai thì thắng.
- **Cơ chế:** 20 s đầu, boss ghi lại sát thương nhận theo `DamageKind` (Weapon, Projectile, Status, Reflect; bỏ Hazard). Tại giây 20, boss "thích nghi": loại chiếm nhiều nhất bị giảm **40%** cho tới hết trận.
  - Nếu người chơi đã gây ≥ 2 loại, mỗi loại ≥ 30%, thì chỉ giảm 20%.
  - Không phụ thuộc lựa chọn ngoài trận, nên nguyên tắc "địch chỉ sinh từ seed" vẫn giữ.
- **Hook (đã chốt):** **[hook mới]** `TraitRule.OnDamageTaken(DamageKind kind, float amount)`.
  - Gọi sau khi HP đã trừ, cho mọi loại sát thương: Weapon/Projectile trong `ApplyHitHp`/`RunHitHooks`, và Status/Reflect/Hazard trong `DealDamage`.
  - Đếm bằng sát thương thực nhận, không đếm đòn bị khiên chặn.
  - Giảm sát thương sau khi thích nghi vẫn dùng `ModifyIncomingDamage` (có sẵn).
- **Event mới:** `SimEventType.Adapted` (A = boss, `Variant = (byte)DamageKind` đã thích nghi). Phát một lần ở giây 20. View dùng để đổi màu và hiện icon.
- **[SimVer] 3 → 4** do thêm hook + event vào pipeline. Hash phải gồm bộ đếm và trạng thái thích nghi.
- **Lưu ý:** Brawler (Weapon thuần) và Blade không trait bị phạt nặng nhất. Cần sweep riêng tỉ lệ thắng trận 8 theo vũ khí người chơi, mục tiêu không vũ khí nào < 25%.
- **Tín hiệu hình ảnh:**
  - **(Đã chốt)** 20 s đầu thân boss **xám tối (60,60,72)**, mắt quét qua lại.
  - Tại giây 20 (event `Adapted`), thân đổi sang màu số sát thương của loại đã thích nghi:
    - **độc** = xanh lá (lime 96,255,64);
    - **phản** = magenta **(255,90,200)**;
    - **vũ khí** = xanh thép **(120,150,200)**.
  - Màu cho **Projectile** chưa chốt; đề xuất vàng nhạt (255,224,128) theo weapon-bible (SPARKLER). **Cần Phú/Art chốt.** Hazard không tính.
  - Kèm icon 2 chữ trên HUD (`AD:WP`, `AD:SH`, `AD:PS`, `AD:RF`).
  - Popup sát thương của loại bị giảm hiện màu xám.
  - Preview: `BOSS: MIMIC  ADAPTS AT 20S`.

### Ghi chú chung cho boss
- Cân lại HP và trait của boss cho **2 boss** (trận 4 và 8; hiện tra theo stage sẽ ra 220 HP/1 trait và 300 HP/2 trait) sau khi có cơ chế. Có thể giảm số trait ngẫu nhiên để cơ chế boss là bài toán chính (đề xuất sweep: trait 0/0/1).
- Unlock giữ nguyên bảng `UnlockRules`: boss1 → Glass Cannon, v.v.
- Có thể thêm achievement theo boss ("hạ Mimic không bị thích nghi") ở Step 3 GĐ1.
- Hiển thị boss: sprite boss nên vẽ native 48x48 thay vì phóng 1.5 lần, để sửa lỗi pixel không nguyên lần.

## 5. Thứ tự triển khai (tôn trọng thoả thuận)

> Cập nhật theo chốt: bước 2 chỉ còn cài **luật đánh lại 1 lần** (không sweep A/B/C). Bước 3 chỉ còn SHELL KING + MIMIC + arena `closing`. Bước 5 gồm `BossFights {3,7}`, bỏ Lives/thêm cờ retry, `SaveData` 1→2, `SimVersion` 3→4, hook `OnDamageTaken` + event `Adapted`, và sửa `UnlockRules`.


**Thoả thuận:** không merge gì làm đổi Sim/Run cho tới khi Game Tester xong **mục 1 và mục 4** của checklist playtest trên **SimVersion 3**. Mình không có nội dung chi tiết hai mục này trong source; PM/Game Tester xác nhận.

| Bước | Việc | Đụng Sim? | Merge khi |
|---|---|---|---|
| 0 | Game Tester chơi mục 1 và 4 trên SimVersion 3. GD ghi riêng cảm nhận về boss và cảm giác khi thua. | không | — |
| 1 | Instrument `RunBot.Result` (HP trước boss, trận làm hết mạng, chết dần) + bot Counter, chỉ trong `SimTests/` | không (chỉ test) | được merge sớm nếu PM đồng ý, vì không đổi `Assets/Scripts/Sim` |
| 2 | Nhánh `gd/loss-rules`: cài A/B/C sau cờ tuning (static field như `RunTuning.WinHealPct`), chạy sweep §3 | Run | **không merge**; chỉ nộp report |
| 3 | Nhánh `gd/bosses`: Warden (config) → Shell King → Mimic; sweep độ khó + theo vũ khí | Run + trait mới | không merge; report |
| 4 | Sau bước 0: user chốt luật thua + boss dựa trên report bước 2–3 và cảm nhận playtest | — | — |
| 5 | Gộp **một lần** với retune trait của card-pool-v2 (đó mới là [SimVer] 3→4): bump `SimVersion.Rules`, `SaveData.CurrentVersion` 1→2, cập nhật `RunDifficultyThresholdTests` và golden/threshold tests | có | sau bước 0 + review |
| 6 | View: preview boss, vòng khiên 3, tường cảnh báo, màu Mimic, sprite boss 48x48 | View | cùng hoặc sau bước 5 |
| 7 | Playtest vòng 2 trên SimVersion 4 (≥ 3 run), so sánh với bot | — | — |

**Câu hỏi mở còn lại cho Phú:**
1. Chuyển phần thưởng `Boss3` (Venom + Parry Master) sang thành tựu nào?
2. Đánh lại có dùng seed trận mới (giữ đối thủ và arena) không? Có được chọn 1 thẻ trước khi đánh lại không?
3. Màu MIMIC khi thích nghi loại Projectile?
4. Thua có còn +1 xu không?

## Cập nhật sau sweep 02:27 (11/10/2026) — sàn HP khi thắng

**Nguồn:** `/workspace/ball-battle/simrunner/reports/floor-sweep.md`.
- 2000 run mỗi chính sách mỗi mức sàn, seed 1..2000 giống nhau.
- **Luật cũ:** 3 mạng, thua hồi đầy. Chưa phải luật 1 lượt đánh lại.

| Sàn | Greedy thắng run | Greedy Heal/run | Heal được chọn khi có mời | Lần thắng được sàn nâng HP |
|---|---|---|---|---|
| không | 36.5% | 0.61 | 38.8% | 0% |
| 40% | 36.6% | 0.61 | 38.8% | 4.0% |
| 50% | 36.0% | 0.30 | 19.3% | 15.0% |
| 60% | 36.5% | 0.27 | 18.5% | 27.5% |

**Đọc số:**
- Sàn 40% gần như không bao giờ kích.
- Sàn 50% và 60% **giảm một nửa lượt chọn Heal** của Greedy, còn tỉ lệ thắng run gần như giữ nguyên (~36–37%). Random cũng vậy: 6.4 → 7.6%.
- Tức là sàn chủ yếu làm thẻ Heal mất giá, không làm run dễ hơn.

**Quyết định thiết kế:**
- **Bỏ sàn 60% khỏi luật đã chốt; giữ hồi +35% thuần.**
- Lý do: với luật 1 lượt đánh lại, thua là **đốt lượt đánh lại duy nhất**, và thua lần hai thì hết run. Ưu đãi ngược "thua có lợi hơn thắng sát nút" yếu hơn nhiều so với luật 3 mạng mà playtest đã quan sát.
- Sàn còn có hại phụ: làm Heal mất giá, đi ngược mục tiêu "HP luôn có giá trị" ở §PT.

**Phương án dự phòng (không bật):**
- `HP = max(HP + 0.35*MaxHp, Floor*MaxHp)`, `Floor` ∈ {0.5, 0.6}.
- Vẫn nên để `Floor` là static field (mặc định 0) cho sweep v4, nhưng code này không bắt buộc trong lô v4.

**Đánh giá lại sau sweep v4 (luật 1 lượt đánh lại) [chưa đo]:**
- **Retry used rate:** % run dùng lượt đánh lại, và dùng ở trận nào. Nếu lượt đánh lại chủ yếu dùng ngay sau một trận thắng sát nút (vào trận kế tiếp với HP thấp), xem lại phương án dự phòng.
- **Tỉ lệ chọn Heal:** Heal/run và "Heal được chọn khi có mời", so với mốc luật cũ 0.61 / 38.8% (Greedy).
- **HP lúc vào trận sau một trận thắng:** phân bố, đặc biệt % trận bắt đầu dưới 40%.

## Cập nhật sau sweep 02:46 (11/10/2026) — độ khó Run

- **Nguồn:** `simrunner/reports/v4-proposal-C-runs-traits.md`. 2000 run/bot, seed 1..2000; luật thua cũ (mạng, hồi đầy), không sàn HP.
- **Kết quả:** gói thay đổi thử (tường gai 1, Heavy2 +50%, Thorns2 18%) đưa Greedy **36.5% → 33.7%**, Random **6.4%** (giữ nguyên). Vẫn nằm trong dải độ khó (Greedy 25–40%, Random 3–12%).
- **Lưu ý:** Heavy/Thorns và tường 1 đã bị huỷ sau sweep này. Gói v4 thật (Spikes leo thang, trần Vampire, luật 1 lượt đánh lại, bỏ sàn HP) phải đo lại G5 trong `bot-sweep-v4.md`, cùng retry used rate và Heal/run như mục 02:27.

## Cập nhật 03:05 (11/10/2026)

- **Spikes leo thang N = 15 s (chờ Phú duyệt "sân nhà Venom"):** Run luật cũ Greedy 36.5 → **40.8%**, boss3 21.7 → **26.2%**, Random **6.7%** (`v4-spike-ramp.md`, 2000 run/bot). Greedy nhích quá trần 40% khoảng 0.8 điểm. Đo lại với luật 1 lượt đánh lại.
- **Luật sinh boss:** trận boss (SHELL KING trận 4, MIMIC trận 8) **không bao giờ** là địch Venom trên Spikes.
  - Nếu seed ra Spikes và boss có Venom: đổi arena theo bước seed kế tiếp (tất định).
  - Ảnh hưởng đến cách tách seed/arena cho lượt đánh lại (§ code impacts).
- **ANTIDOTE bảo đảm** khi trận kế là Spikes + địch Venom/Poison Tip. Không chiếm ô trait, dùng được làm lượt miễn phí; chi tiết `card-pool-v2.md` mục 03:05.
- Tương tác với lượt đánh lại: đánh lại cùng địch/arena thì **lượt mời được giữ nguyên** (theo đề xuất ở § code impacts; cần giữ đúng như vậy), nên ANTIDOTE vẫn có mặt.

## Đã chốt 11/10 (Phú duyệt 14:51 ICT)

- **Độ khó Run: phương án (A) FINAL.**
  - 8 trận. **1 lượt đánh lại mỗi run** với **60% HP**: cùng địch, cùng arena, seed trận mới, có **lượt chọn miễn phí** trước khi đánh lại.
  - **Miniboss SHELL KING (trận 4):** 160 HP, **0 trait** (không dùng chỉ số boss tầng 2).
  - **Boss cuối MIMIC (trận 8).** Chỉ còn 2 boss.
  - **Đầu run chọn 1 trong 3 thẻ:** dùng luật mời thẻ bình thường (Heal bị lọc vì đầy máu), **không reroll**, **không mời ANTIDOTE** ở lượt này.
  - **Số đo** (`simrunner/reports/v4-early-easier.md`, 2000 run/bot, ±1 điểm): Greedy **28.7%**, Random **2.4%**.
- **Ngưỡng test `RunDifficultyThresholdTests`:** Random **1–6%**, Greedy **25–40%**. **Thay test "boss thứ 3"** (chỉ còn 2 boss).
- **ANTIDOTE bảo đảm** ở lượt mời trước trận Venom @ Spikes **và** trước lượt đánh lại trận đó.
- **Spikes** được chấp nhận là sân nhà của Venom (~73%). Tường leo thang **N = 15 s**.
- **Cân bằng:** Vampire2 **24%**, Thorns2 **18%**, Heavy giữ nguyên. Combo mạnh nhất **69.2%** (gate ≤ 70%).
- Các phương án đã loại: B (giảm 15% HP địch trận 1–3, 26.1%), C (A+B, 37.4%), mức mạnh hơn; "2 lượt đánh lại" (32–33%) không dùng.
