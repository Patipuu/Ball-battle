# BB-12: kết quả đo cuối của bộ cây thử nghiệm (15/10/2026)

**Kết luận: KHÔNG ĐẠT cân bằng, không đưa bộ hệ số này vào game.** Đây là kết quả cuối của **bộ ứng viên đã thử**, không phải xác nhận rằng tám cây đã cân bằng. Phần lõi Run hiện cũng lệch giữa các bóng; chỉnh vài phần trăm ở từng node không đủ giải quyết đồng thời LV1, LV10 và hai nhánh KEYSTONE.

## Phạm vi và cách lặp lại

- Source: checkout `86023c5`, Sim Rules 13. Harness tại `docs/design/bb12/harness/` biên dịch bản sao `MatchSim`, `CardOffer` và tám weapon rules để thử node; không sửa gameplay trong `BallBattleUnity/Assets/`.
- Bỏ SKILL UP khỏi card pool; ép vũ khí khởi đầu; `RunBot.Policy.Greedy`; cùng seed cho tất cả build. START và KEYSTONE chỉ áp lên bóng ở slot 0. Skill LV2/3 dùng nguyên `SkillState` hiện có. Matchups, boss và arena thay đổi theo seed của Run.
- Tắt node mới, harness khớp chính xác baseline 100 seed ở cả tám bóng, kể cả số fight thắng (`baseline-keystone-hook-100.csv`). GROWTH, START, KEYSTONE là prototype trong harness; chưa đo người chơi thật, Random bot, PvP hoặc hiệu năng Unity.
- Bộ holdout dùng seed **701–900**; không dùng dải này để chỉnh hệ số. Mỗi ô dưới là **200 full runs**. Mẫu 200 có biên sai số 95% khoảng 4–7 điểm phần trăm cho một tỷ lệ đơn lẻ; chênh rất lớn giữa nhánh không thể giải thích bằng sai số cỡ đó. Các kết quả sát nhau chưa đủ để xếp hạng.

## Sáu build LV10 hợp lệ trên holdout

Mọi build đều có VITALITY 2, POWER 2, GROWTH 1 (5 điểm). Bốn cột đầu mua skill A3/B3 (2 điểm) và KEYSTONE A/B (3 điểm). Hai cột cuối mua START (2 điểm), giữ skill L1, cộng KEYSTONE A/B (3 điểm). Tổng đều **10 điểm**, qua cổng tầng 2 và 3. VITALITY dùng +4% HP/rank, POWER +6 điểm phần trăm damage/rank. Baseline là LV1, không cây, không SKILL UP, cùng seed.

| Bóng | LV1 | A3+KA | A3+KB | B3+KA | B3+KB | START+KA | START+KB | Tốt nhất trong 6 | Dao động 6 build |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| EMBER | 21.5% | 69.5% | 22.0% | 80.0% | 35.5% | 74.0% | 38.5% | 80.0% | 58.0 điểm |
| MANTIS | 21.5% | 30.5% | 65.5% | 34.5% | 58.0% | 37.0% | 66.5% | 66.5% | 36.0 điểm |
| ANGLER | 20.5% | 48.5% | 63.0% | 51.5% | 57.5% | 59.5% | 65.0% | 65.0% | 16.5 điểm |
| WRECKER | 54.0% | 87.0% | 86.0% | 91.5% | 83.5% | 85.0% | 80.0% | 91.5% | 11.5 điểm |
| SPARKLER | 26.5% | 74.0% | 74.0% | 74.0% | 61.0% | 78.5% | 70.0% | 78.5% | 17.5 điểm |
| SPORE | 14.0% | 81.0% | 66.5% | 73.0% | 60.0% | 83.0% | 67.0% | 83.0% | 23.0 điểm |
| MIRROR | 19.0% | 62.5% | 35.5% | 64.0% | 39.5% | 69.0% | 40.0% | 69.0% | 33.5 điểm |
| HIVE | 36.5% | 83.0% | 61.5% | 84.0% | 68.5% | 84.5% | 66.5% | 84.5% | 23.0 điểm |

Theo gate đề xuất trước đây (LV1 ≥25%, LV10 ≤70%): **5/8 bóng dưới mốc LV1**; **5/8 bóng có ít nhất một build đã đo vượt mốc LV10**. WRECKER đạt 80–91.5% trên cả sáu build, trong khi tốt nhất của ANGLER là 65%. Chênh giữa hai bóng ở giá trị tốt nhất quan sát được là 26.5 điểm. Đây chỉ là sáu trong 76 build dùng đủ 10 điểm; vì đã tìm thấy nhiều build vượt gate và nhánh chênh mạnh, không cần quét hết 76 để bác bỏ bộ hệ số hiện tại.

## Node riêng lẻ giải thích vì sao bị loại

| Bóng | Baseline 200 seed | START bản đầu | START đã giảm/chỉnh |
|---|---:|---:|---:|
| EMBER | 25.5% | 42.5% | 33.5% |
| MANTIS | 28.0% | 42.0% | 25.5% |
| ANGLER | 18.0% | 60.5% | 37.0% |
| WRECKER | 61.5% | 52.5% | 64.5% |
| SPARKLER | 33.0% | 43.5% | 43.5% |
| SPORE | 16.0% | 16.0% | 18.5% |
| MIRROR | 19.5% | 54.5% | 27.0% |
| HIVE | 34.5% | 47.0% | 47.0% |

START là node 2 điểm nhưng bản chỉnh vẫn dao động từ −2.5 đến +19 điểm so với baseline. Phép thử tách START không đi qua gate tầng 2; nó chỉ đo hiệu ứng node, còn hai build START+KEYSTONE ở trên là build hợp lệ.

Skill LV2/3 hiện có cũng không tăng đều. Trên seed 1–100, với không node khác: EMBER từ LV1 23% lên A2 22% hoặc B2 32%, A3 22% hoặc B3 40%; MANTIS từ 26% lên A2 34% nhưng B3 25%; MIRROR từ 19% xuống A2/B2 17%. Mua node skill không đảm bảo có lợi trong bot này. Raw CSV: `skill-A2-100.csv`, `skill-B2-100.csv`, `skill-A3-100.csv`, `skill-B3-100.csv`.

KEYSTONE đo riêng trên core 6 điểm (V2/P2/G1 + skill A2) ở seed 1–100 cho EMBER: không KEYSTONE 49%, KA 74%, KB 25%; MANTIS 49/34/68%; SPORE 55/80/58%; MIRROR 48/53/37%. Dải LV10 holdout xác nhận phần lớn lệch nhánh, nên không phải chỉ một lần chạy seed xấu. Các thay đổi cơ chế/cost của KEYSTONE cần một vòng thiết kế mới.

## Quyết định và giới hạn

1. **Bác bỏ các hệ số ở `bb12-skill-trees-candidate.md`.** Chúng được giữ trong tài liệu như bản thử đã đo, không dùng để lập trình progression hoặc quảng bá là “balanced”.
2. **Cân bằng Run nền trước:** LV1 đã chênh 14% (SPORE) đến 54% (WRECKER) trên holdout; START/GROWTH chỉ làm khoảng cách thay đổi thêm. Nên đo riêng boss và arena, sửa Run-only tuning để không ảnh hưởng Versus, rồi xác nhận lại với 500+ seed/bóng. Đặt lại gate LV1/LV10 sau khi có baseline mới.
3. **Thiết kế lại START và KEYSTONE**, ưu tiên MANTIS START, SPORE START, EMBER/MIRROR KEYSTONE B và các nhánh chỉ số đã cho lợi thế một chiều. Kiểm tra skill LV2/3 vì đó là node thật của cây, không phải phần trang trí.
4. **Giới hạn phép đo:** sáu build LV10 trên 76 build exact-spend, Greedy bot, 200 seed/ô; chưa có phân tích boss/arena chi tiết, không có thử nghiệm người chơi. Dù vậy mức lệch quan sát được đã đủ bác bỏ bộ hệ số; quét thêm cùng bộ này không biến nó thành cân bằng.

## CSV để kiểm toán

- LV10 holdout: `lv10-holdout-baseline-200.csv`, `lv10-A3-A-holdout-200.csv`, `lv10-A3-B-holdout-200.csv`, `lv10-B3-A-holdout-200.csv`, `lv10-B3-B-holdout-200.csv`, `lv10-start-A-holdout-200.csv`, `lv10-start-B-holdout-200.csv`.
- START: `start-200.csv`, `start-revised-200.csv`.
- KEYSTONE riêng: `keystone-none-core-100.csv`, `keystone-A-core-100.csv`, `keystone-B-core-100.csv`.
- Baseline/GROWTH và các lỗi phương pháp đã loại: xem `bb12-tree-baseline.md`.
