# Bot sweep plan — lô v4 (Game Designer, 11/10/2026)

**Phạm vi:** đo trước khi merge lô v4. Lô này gồm:
- retune trait (card-pool-v2 §c + P2);
- luật 1 lượt đánh lại + sàn HP khi thắng;
- SHELL KING / MIMIC (`BossFights {3,7}`), arena `closing`;
- trọng số vũ khí khởi đầu.

**Ràng buộc:**
- Chạy trên nhánh, không merge trước khi Game Tester xong mục 1 và 4 trên SimVersion 3.
- Mọi ngưỡng dưới đây là **mục tiêu tự đặt, [chưa đo]**.
- Seed cố định. Song song như các report trước (khoảng 38 ms/trận, 16 luồng).

## Bot và instrument
- **Random, Greedy:** có sẵn. Greedy thêm ưu tiên Heal khi HP < 60% trước boss.
- **Counter / Lookahead:** card-pool-v2 §d.
- **ComboForcer** (mới): ép build Brawler + Heavy + Vampire + Thorns. Chọn các thẻ đó khi được mời, reroll để tìm chúng. Mô phỏng người chơi "đi theo meta" như Run 1.
- **`RunBot.Result` thêm:** HP% trước mỗi boss, trận dùng lượt đánh lại và kết quả, trận kết thúc run, sát thương nhận theo `DamageKind`, build cuối.

## Gate cứng
| # | Gate | Cách đo | Ngưỡng [chưa đo] |
|---|---|---|---|
| **G1** | **Combo sustain Heavy + Vampire + Thorns** (L1 và L2, trên Brawler + 7 vũ khí khác) | (a) `TraitBalanceReport` combo vs 8 build 3-trait ngẫu nhiên, 40 seed/bên; (b) vs boss r24 300 HP + 2 trait; (c) bot ComboForcer 2000 run | (a) ≤ **75%** (hiện tệ nhất đã biết: Brawler + Vampire2 + SecondWind2 + Thorns2 = 85.7%); (b) ≤ 70%; (c) thắng run ≤ Counter bot + 5 điểm; trận dùng lượt đánh lại ≥ 20% số run |
| **G2** | **Venom @ Spikes** | `ArenaBalanceReport` hàng Venom × spike-walls, 100 seed/bên; tách sát thương theo kind (Status / Hazard / Weapon); Venom vs Blade @ Spikes riêng | TB vs 7 vũ khí ≤ **62%** (hiện 72%); Venom vs Blade @ Spikes ≤ 65%; thời lượng trung vị ≥ 30 s (playtest ~20 s) |
| **G1b** | **Heavy × Thorns** (thêm sau sweep 02:27) | `BrawlerComboReport` (simrunner): Brawler + Heavy2 + Thorns2 và Brawler + Heavy2 + Vampire2 + Thorns2, vs 16 build 3-trait ngẫu nhiên, TB 7 arena, 60 seed/bên | Heavy2 + Thorns2 ≤ **75%** (hiện 61.4% vs build ngẫu nhiên, 91.3% vs vũ khí trơn); combo L2 Heavy2 + Vampire2 + Thorns2 ≤ **70%** (hiện 74.7%); báo cáo kèm cột vs 8 vũ khí trơn (hiện 95.9%) |
| G3 | Ma trận vũ khí | `BalanceThresholdTests` 8×8 | giữ 30–70% (gate hiện tại); báo cáo số cặp ngoài 40–60 (mục tiêu Step 3) |
| G4 | Trait đơn lẻ | `TraitBalanceThresholdTests` trên **8** vũ khí | L1 +5..+13; L2 ∈ [L1+3, +18] (card-pool-v2 §c) |
| G5 | Run | 2000 run/chính sách | Random 3–12%, Greedy 25–40%, Counter ≥ Greedy + 10; HP trung vị trước boss 50–80%; lượt đánh lại thắng 35–60%; trận kết thúc run: trận 1–2 ≤ 10%, trận boss ≥ 50%, không trận nào > 35% |
| G6 | Vũ khí khởi đầu | mô phỏng 30 lần mời liên tiếp/hồ sơ, 1000 hồ sơ | mỗi vũ khí đã mở xuất hiện ≥ 1 lần trong mọi cửa sổ 3 lần mời (100%); vũ khí chưa chơi xuất hiện trong lần mời đầu ≥ 60% |
| G7 | Boss | trận 4 / trận 8 theo vũ khí người chơi (Greedy build) | không vũ khí nào thắng SHELL KING hay MIMIC < 25% hoặc > 80% |
| G8 | Aegis @ Tight | tách: Aegis vs từng vũ khí @ Tight, và Aegis vs boss Venom | xác nhận nguồn gây ra con số 26%; ngưỡng ô 25–75% (gate arena hiện có) |

## Thứ tự chạy
1. G3, G4, G2 (match-level, nhanh).
2. G1.
3. G7, G8.
4. G5, G6 (Run-level, chậm nhất).

Đổi tuning thì chạy lại từ bước 1.

**Đầu ra:** `plans/.../reports/v4-sweep-*.md`, gồm bảng trước/sau và danh sách gate đỏ.

## Cập nhật sau sweep 02:27 (11/10/2026)

**Đã có số mốc trên SimVersion 3** (`/workspace/ball-battle/simrunner/reports/`):

**G1b (mới):**
- Brawler + Heavy2 + Vampire2 + Thorns2: 95.9% vs vũ khí trơn, 74.7% vs build 3-trait ngẫu nhiên.
- Bản L1: 86.9 / 55.9.
- Heavy2 + Thorns2: 91.3 / 61.4.
- Đề xuất đi kèm: Heavy L2 +70 → +50% HP, Thorns L2 22 → 18% **[chưa đo]**. Gate sau đổi: Heavy2 + Thorns2 ≤ 75%, combo L2 ≤ 70% (vs build 3-trait ngẫu nhiên).

**G2 (đổi cách sửa):**
- Không nerf Venom nữa; sửa arena Spikes.
- Mốc: Venom @ Spikes TB 73.3%, Venom vs Blade @ Spikes 71.4%, trận trung vị ~17 s.
- **Ngưỡng mới:** trung vị trận Spikes **≥ 35 s** (thay cho 30 s); Venom @ Spikes TB ≤ 62%; Venom vs Blade ngoài Spikes không được tụt dưới 39% (hiện 39.5–43.7).
- Sweep lưới: `SpikeWallDamage` {2, 1.5, 1} × tốc độ bật tường Spikes {100%, 85%, 70%} [chưa đo]. Report Phase 5 nói chỉ đổi sát thương tường 1/3 không hạ được Venom.

**G5:** sàn HP khi thắng đã bỏ khỏi luật chốt (`boss-and-loss-rules.md`). Thêm chỉ số **retry used rate** và **Heal/run** (mốc luật cũ Greedy 0.61).

## Cập nhật sau sweep 02:46 (11/10/2026)

**Nguồn:** `/workspace/ball-battle/simrunner/reports/v4-proposal-{A-spikes,B-combo,C-runs-traits}.md`. Các gate dưới đây **thay** bản 02:27 nếu khác nhau.

**G1b (sửa):**
- Chỉ dùng cột **vs 16 build 3-trait ngẫu nhiên, TB 7 arena** (Field B). Cột vs vũ khí trơn chỉ báo cáo, không làm gate.
- Gate: Brawler + Heavy2 + Vampire2 + Thorns2 ≤ **70%**, đo **sau khi** có trần Vampire mỗi hit (2/3 HP) **[chưa đo]**. Mốc chưa có trần: 74.7%.
- Huỷ gate "Heavy2 + Thorns2 ≤ 75%" như mục tiêu chỉnh, vì đã 61.4% trên số ship; giữ làm chỉ số theo dõi.
- Không đổi Heavy/Thorns. Bản Heavy +50% làm delta Heavy L2 còn +0.6, vi phạm G4.

**G2 (sửa — Spikes leo thang):**
- Sweep N (giây đầu tường thu răng) = **6 / 10 / 15 s**. Sau N: 1/chạm, +1 mỗi 20 s, trần 3.
- Đo: 28 cặp không-gương × 500 seed/bên trên spike-walls, giống `v4-proposal-A`.
- **Ngưỡng [chưa đo]:**
  - Venom @ Spikes TB ≤ **62%**;
  - **mọi vũ khí ở Spikes trong [38, 62]%**;
  - trung vị trận ≥ **35 s**;
  - kèm báo P10/P90 và % chạm trần thời gian.
- Mốc thất bại cần ghi trong báo cáo: 2×1 = 73.3%/16.8 s; 1×1 = 77.3%/28.0 s; mọi ô độ nảy < 1 làm Brawler ≤ 18%.
- **Không đổi `VenomSeconds`:** giữ 4 s. Probe 3 s làm Venom còn 30.3–44.0% ở 6 arena khác.

**G4 (ghi mốc):** Parry Master L2 +24.0 và Poison Tip L2 +21.4 đang vượt trần +18 trên số ship. Ghi nhận, chưa chặn lô v4.

**G5 (mốc luật cũ, `v4-proposal-C`):**
- Gộp thay đổi (tường 1, Heavy2 +50%, Thorns2 18%): Greedy **36.5 → 33.7%**, Random **6.4%**. Vẫn trong dải 25–40 / 3–12.
- Bản tường 1 × độ nảy 0.85: Greedy 33.9, Random 5.9.
- Hai bản này đã bị huỷ, chỉ là tham chiếu độ nhạy: thay đổi arena/trait cỡ này dịch Greedy khoảng 3 điểm.
- Chạy lại G5 với gói v4 thật (Spikes leo thang + trần Vampire + luật 1 lượt đánh lại).

## Cập nhật 03:05 (11/10/2026)

Nguồn: `simrunner/reports/v4-spike-ramp.md`, `v4-spike-ramp-runs.md`, `v4-spike-ramp-combo.md`, `v4-n15-vampire.md`.

**G2 (sửa, chờ Phú duyệt):**
- Tường leo thang **N = 15 s**, +1/20 s, trần 3.
- **Gate:**
  - trung vị trận Spikes ≥ 35 s (đo 36.1 ✓);
  - mọi vũ khí **trừ Venom** trong 38–62% (đo 41.4–54.5 ✓);
  - Venom @ Spikes **không gate trần**, chỉ báo cáo (đo 72.8%), và không được vượt **75%** [chưa đo, chặn hồi quy].
- Nếu Phú không duyệt: quay về N = 40 s (đạt hết, nhưng Run Greedy 45.7%), hoặc tìm phương án khác.
- **Gate phụ (Run):**
  - % trận boss có địch Venom trên Spikes = **0**;
  - % lượt mời trước trận Spikes + địch Venom/Poison Tip có ANTIDOTE = **100%**.

**G1b:**
- Combo L2 ≤ 70% vs build ngẫu nhiên. Vampire2 22/24% trượt (71.2 / 71.9).
- Đo lại **sau khi có trần hồi mỗi hit trong code**, và chạy lưới Vampire × Thorns.
- Ràng buộc: Vampire2 delta phải > Vampire1 delta (+11.0, 4 vũ khí).

**G4:** Parry Master L2 +24.0, Poison Tip L2 +21.4, Twin Blade L2 +18.6, Second Wind L2 +18.3 > +18. Đổi từ "ghi nhận" thành **phải xanh trước khi merge v4**.

**G5:**
- Mốc Run với N = 15 (luật cũ): Greedy **40.8%** (vượt trần 40 khoảng 0.8 điểm), boss3 26.2%, Random 6.7%.
- Đo lại với gói v4 đầy đủ (luật 1 lượt đánh lại, trần Vampire, chỉnh G4). Dải giữ 25–40%.

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
- **G2:** N = 15 s đã duyệt; Venom @ Spikes chỉ báo cáo, chặn hồi quy ở 75%. **G5:** thay mốc bằng số của (A) ở trên.
