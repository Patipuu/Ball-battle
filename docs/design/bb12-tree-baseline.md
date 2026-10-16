# BB-12: baseline cục bộ sau khi bỏ SKILL UP (15/10/2026)

**Trạng thái:** báo cáo baseline và GROWTH sơ bộ. START, KEYSTONE, skill LV2/3 và build LV10 được đo tiếp trong `bb12-tree-final-measurement.md`. Kết luận của sweep là **bộ giá trị thử không cân bằng**.

## Cách đo có thể lặp lại

- Source: checkout `86023c5`, Sim Rules 13. Harness tại `docs/design/bb12/harness/` biên dịch source Sim hiện tại, trừ `CardOffer.cs` được thay bằng bản sao chỉ bỏ dòng thêm thẻ `SkillUp`. Code game trong `Assets/` không đổi.
- Bot: `RunBot.Policy.Greedy`, ép vũ khí khởi đầu, 8 bóng, seed 1–500, cùng bot/seed cho mọi ứng viên. Bot giữ các luật chọn thẻ còn lại. Nó có thể lấy DAMAGE, MAX HP, trait, HEAL, SPEED CAP; card pool được rút lại đúng trọng số sau khi bỏ SKILL UP.
- Đối chứng với luật thẻ cũ trên seed 1–100: kết quả trong `control-skillup-100.csv` gần báo cáo BAL3 500 seed về thứ hạng và mức thắng. Baseline mới 100 seed chạy lặp sau khi thêm callback cây cho kết quả **trùng từng bóng** với lần đầu.
- Bản sao tám `WeaponRule` trong harness cho phép GROWTH chỉ tác động lên `Sim.Balls[0]` (bóng của bot). Khi GROWTH = 0, cả tỷ lệ thắng lẫn số fight thắng trung bình trùng chính xác baseline 100 seed ở cả tám bóng (`baseline-rulecopy-100.csv`). Kết quả `growth2-200.csv` cũ đã thay đổi `WeaponTuning` tĩnh cho **cả hai bên**, nên không hợp lệ và không dùng trong bảng dưới.
- Lệnh: `dotnet run --project docs/design/bb12/harness/TreeLab.csproj -c Release -- 1 500 0 0 docs/design/bb12/baseline-no-skillup-500.csv`. Tham số cuối là đường CSV. Harness biên dịch trong `net10.0`, Optimize=true.

## Baseline không SKILL UP

| Bóng | Run thắng / 500 | Tỷ lệ thắng | Số fight thắng trung bình |
|---|---:|---:|---:|
| EMBER | 112 | 22.4% | 3.906 |
| MANTIS | 137 | 27.4% | 4.118 |
| ANGLER | 111 | 22.2% | 4.026 |
| WRECKER | 280 | 56.0% | 5.524 |
| SPARKLER | 153 | 30.6% | 4.250 |
| SPORE | 78 | 15.6% | 3.304 |
| MIRROR | 104 | 20.8% | 4.204 |
| HIVE | 155 | 31.0% | 4.380 |

Việc bỏ SKILL UP **làm tăng** tỷ lệ thắng trong lượt đo này vì các thẻ khác xuất hiện thường hơn; ví dụ SPORE 9.6% ở BAL3 (có SKILL UP, 500 seed) → 15.6% ở đây. So sánh này có cùng dải seed và bot nhưng hai bản harness khác nhau; cần xem như chỉ dấu, không phải đánh giá sức mạnh người chơi thật. WRECKER hơn SPORE 40.4 điểm phần trăm ở LV1. Sai số lấy mẫu riêng từng tỷ lệ ở n=500 khoảng 3–4.4 điểm cho khoảng 95%; khoảng cách 40 điểm không phải nhiễu cỡ đó.

## Độ nhạy của VITALITY và POWER (seed 1–200, ghép cặp với baseline 200)

| Bóng | Baseline | POWER 2 rank, +12% damage | VITALITY 2 rank, +16% HP | VITALITY 2 rank, +8% HP |
|---|---:|---:|---:|---:|
| EMBER | 25.5% | 37.5% | 42.5% | 34.0% |
| MANTIS | 28.0% | 39.5% | 43.0% | 40.5% |
| ANGLER | 18.0% | 29.5% | 33.5% | 23.5% |
| WRECKER | 61.5% | 72.0% | 80.0% | 72.5% |
| SPARKLER | 33.0% | 41.0% | 44.0% | 37.0% |
| SPORE | 16.0% | 27.0% | 28.0% | 21.0% |
| MIRROR | 19.5% | 27.0% | 33.5% | 26.5% |
| HIVE | 34.5% | 47.0% | 46.5% | 43.5% |

`+16% HP` (8%/rank trong bản nháp đầu) mạnh hơn `+12% damage` ở 7/8 bóng và đẩy WRECKER lên 80%. `+8% HP` (4%/rank) gần hơn nhưng WRECKER vẫn 72.5% khi mới tiêu 2 điểm, vượt mốc LV10 ≤70% của plan cũ. Đây là mẫu 200 seed nên chênh vài điểm chưa đủ để chốt hệ số; xu hướng WRECKER vượt trần cần xử lý trước khi tune phần còn lại. Giữ các CSV thô trong `docs/design/bb12/`.

## GROWTH riêng từng bóng, chỉ buff người chơi (seed 1–200)

| Bóng | Baseline | GROWTH rank 1 | GROWTH rank 2 | Chênh rank 2 với baseline |
|---|---:|---:|---:|---:|
| EMBER | 25.5% | 36.5% | 48.5% | +23.0 điểm |
| MANTIS | 28.0% | 31.0% | 34.5% | +6.5 điểm |
| ANGLER | 18.0% | 33.0% | 45.5% | +27.5 điểm |
| WRECKER | 61.5% | 50.5% | 60.5% | −1.0 điểm |
| SPARKLER | 33.0% | 39.5% | 45.5% | +12.5 điểm |
| SPORE | 16.0% | 19.0% | 25.5% | +9.5 điểm |
| MIRROR | 19.5% | 24.0% | 33.5% | +14.0 điểm |
| HIVE | 34.5% | 42.0% | 50.5% | +16.0 điểm |

Các node cùng tốn hai điểm nhưng hiệu quả trải từ −1 đến +27.5 điểm; bản giá trị đầu **không cân bằng**. EMBER/ANGLER GROWTH mạnh hơn POWER 2 ở cùng seed, còn WRECKER GROWTH gần như vô ích và rank 1 có dấu hiệu hại. Có thể do sớm chạm trần speed bonus và quỹ đạo va chạm thay đổi; cần trace trận cụ thể trước khi đổi cơ chế. Đây vẫn là Greedy bot, 200 seed; không suy diễn thành hiệu quả đối với người chơi.

### Vòng chỉnh GROWTH và holdout

Đã đổi WRECKER GROWTH sang tăng `BrawlerDamagePerSpeedPerHit`. Mức +15%/rank cho WRECKER ra 78% thắng ở rank 2 trên seed 1–200 (`growth2-brawler-alt-200.csv`), nên giảm còn +5%/rank. Đồng thời EMBER giảm +12→+5%/rank, ANGLER +12→+4%/rank, HIVE giảm chu kỳ bắn 4→2 tick/rank. Các bóng khác giữ nguyên. **Chưa đưa thay đổi vào game.**

| Bóng | Seed 1–200: baseline → G2 mới | Seed 501–700: baseline → G2 mới |
|---|---:|---:|
| EMBER | 25.5 → 34.5% (+9.0) | 26.5 → 33.5% (+7.0) |
| MANTIS | 28.0 → 34.5% (+6.5) | 30.0 → 38.0% (+8.0) |
| ANGLER | 18.0 → 33.0% (+15.0) | 27.5 → 30.5% (+3.0) |
| WRECKER | 61.5 → 68.5% (+7.0) | 56.0 → 71.0% (+15.0) |
| SPARKLER | 33.0 → 45.5% (+12.5) | 26.0 → 44.0% (+18.0) |
| SPORE | 16.0 → 25.5% (+9.5) | 12.0 → 26.5% (+14.5) |
| MIRROR | 19.5 → 33.5% (+14.0) | 16.0 → 35.0% (+19.0) |
| HIVE | 34.5 → 42.0% (+7.5) | 31.0 → 41.0% (+10.0) |

Các tác dụng vẫn đổi nhiều giữa tập chỉnh và holdout; ANGLER là ví dụ rõ nhất (+15 so với +3 điểm). 200 seed/tập chưa đủ chốt node khi một thay đổi nhỏ làm trận đấu lệch quỹ đạo. WRECKER đã 71% ở holdout chỉ với hai điểm GROWTH, còn các node khác chưa cộng. Vì thế chưa đạt gate LV10 ≤70%; cần một pass cân bằng Run cơ bản và sweep lớn hơn trước khi gắn nhãn “balanced”.

## Quyết định kỹ thuật cho vòng kế

1. Không dùng VITALITY 8%/rank chung cho 8 bóng. Thử quanh 4–5%/rank, hoặc dùng bảng theo bóng nếu số liệu holdout chứng minh cần; POWER 6%/rank cũng cần so với VITALITY ở cùng chi phí.
2. Chênh LV1 giữa WRECKER và SPORE/MIRROR đã có trước cây. Cây vĩnh viễn không giải quyết chênh này ở LV1. Cần thiết kế một pass cân bằng **Run** riêng hoặc điều chỉnh mục tiêu tuyệt đối LV1 của plan cũ; mọi thay đổi phải đo cả Versus để tránh phá đối kháng.
3. Kết quả đo tiếp nằm trong `bb12-tree-final-measurement.md`; các build LV10 không qua gate, nên không triển khai bộ hệ số thử nghiệm này. Cần chỉnh Run cơ bản trước khi có thể cân bằng tám cây.

## File dữ liệu

- `baseline-no-skillup-500.csv`: baseline chính.
- `baseline-no-skillup-100.csv`, `baseline-no-skillup-repeat-100.csv`: kiểm tra lặp.
- `control-skillup-100.csv`: đối chứng luật thẻ cũ.
- `baseline-no-skillup-200.csv`, `power2-200.csv`, `vitality2-200.csv`, `vitality2-hp4-200.csv`: độ nhạy cùng seed.
- `baseline-rulecopy-100.csv`: đối chứng bản sao weapon rules với GROWTH = 0.
- `growth1-player-200.csv`, `growth2-player-200.csv`: GROWTH riêng cho bóng người chơi, cùng 200 seed.
- `growth2-brawler-alt-200.csv`: thử WRECKER damage-growth +15%/rank, quá mạnh.
- `growth2-revised-200.csv`, `holdout-baseline-200.csv`, `holdout-growth2-revised-200.csv`: bản giảm hệ số và kiểm tra seed mới 501–700.
- `growth2-200.csv`: **không hợp lệ**, để lại chỉ để kiểm toán lỗi phương pháp buff cả hai phe.
