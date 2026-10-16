# BB-12 — đo nguyên cây v2

## Phạm vi và cách tái lập

- Prototype ở `bb12/harness-v2` sao chép Sim và thay đúng các node trong `bb12-persona-web-v2.md`. Không sửa Sim dùng trong Unity. Build bằng `dotnet build docs/design/bb12/harness-v2/TreeLabV2.csproj -c Release`.
- `measure.ps1` liệt kê mọi build chi đúng 10 điểm, kiểm tra điều kiện mở tầng và cùng nhánh Kỹ năng/Tài năng, rồi chạy mỗi build cho tám bóng. Có **18 build**, gồm 16 build có Tài năng và hai build không có Tài năng.
- Vòng sàng lọc: Greedy bot, seed **1301–1500**, 200 run/bóng/build, tổng **28.800 run**. Mọi bóng trong một build dùng đúng cùng dải seed. CSV gốc: `bb12/harness-v2/results-greedy-1301-1500/all-lv10.csv`.
- Kiểm tra nền: tắt toàn bộ node v2 và chạy 20 seed 1301–1320. Cả tám bóng khớp harness v1 ở số Run thắng và trung bình fight thắng. CSV: `bb12/harness-v2/v1-baseline-smoke.csv`, `bb12/harness-v2/v2-baseline-smoke.csv`.

## Kết quả sàng lọc LV10

Tên bóng ở bảng dùng tên trong UI; thứ tự dữ liệu tương ứng mã vũ khí blade/fang/pike/brawler/volley/venom/aegis/rig. Tỷ lệ là phần trăm Run thắng; **mỗi ô min/max lấy trên 18 build**, không phải khoảng tin cậy thống kê.

| Bóng | Trung bình 18 build | Kém nhất | Tốt nhất | Khoảng chênh |
|---|---:|---:|---:|---:|
| EMBER | 46,2% | 40,0% | 58,0% | 18,0 điểm |
| MANTIS | 43,8% | 39,0% | 51,0% | 12,0 điểm |
| ANGLER | 48,6% | 43,0% | 61,5% | 18,5 điểm |
| WRECKER | 73,7% | 70,0% | 77,0% | 7,0 điểm |
| SPARKLER | 48,9% | 36,0% | 64,5% | 28,5 điểm |
| SPORE | 53,1% | 44,5% | 59,5% | 15,0 điểm |
| MIRROR | 37,2% | 25,0% | 49,5% | 24,5 điểm |
| HIVE | 60,2% | 53,5% | 68,5% | 15,0 điểm |

Build tốt nhất của WRECKER là V2/P2/G2/START/SKILL A3/không FINALE, 77%. Build tốt nhất của MIRROR là cùng root nhưng SKILL B3, 49,5%: lệch **27,5 điểm** khi mỗi bóng được chọn build tốt nhất. Mức này vượt gate thiết kế 15 điểm và ngưỡng ≤70% của build mạnh nhất. Các cặp A/B cùng root lớn nhất: SPARKLER 11,5 điểm ở V1/P2/G2/không START; SPORE 11 điểm ở V1/P0/G2/START; HIVE 10 điểm ở V0/P2/G1/START. Đây là tín hiệu sàng lọc trên 200 seed, chưa đủ để kết luận chính xác ngưỡng 8 điểm.

Hai build không FINALE đạt đỉnh của **bảy trong tám bóng** (trừ HIVE). Điều đó cho thấy Tài năng giá 3 điểm chưa cạnh tranh được với cách dồn điểm vào chỉ số gốc/Mở màn trong v2. Đây là so sánh build tổng thể, chưa phải hiệu ứng nhân quả riêng của từng node.

## Diễn giải và quyết định

V2 **chưa qua gate cân bằng**. WRECKER mạnh ở toàn bộ 18 build (70–77%), MIRROR vẫn yếu (25–49,5%). Chỉ chỉnh hai Tài năng cuối sẽ không khép khoảng cách nền. Cần cân lại Run nền, nhất là boss/đối thủ của MIRROR, và xem lại cơ chế tăng trưởng của WRECKER. SPARKLER nhạy mạnh với cách phân điểm, cần kiểm đạn và skill branch trong từng arena. Trước khi đưa vào Sim chính, đo node riêng và fight theo boss/arena để biết nguyên nhân, sau đó lặp lại sweep trên seed tune và holdout mới.

## Xác nhận trên seed mới

Chạy Greedy seed **1501–2000**, 500 run/bóng/build cho hai build không FINALE (V2/P2/G2/START/SKILL A3 hoặc B3) và một cặp FINALE A/B (V1/P2/G2/không START). Đây là dải seed chưa dùng ở vòng sàng lọc. CSV: `bb12/harness-v2/results-validation/all-validation.csv`.

| Bóng | Không FINALE A3 | Không FINALE B3 |
|---|---:|---:|
| EMBER | 47,2% | 60,8% |
| MANTIS | 56,6% | 55,8% |
| ANGLER | 56,8% | 62,4% |
| WRECKER | 75,4% | **78,8%** |
| SPARKLER | 57,6% | 58,8% |
| SPORE | 67,6% | 65,8% |
| MIRROR | **53,8%** | 53,4% |
| HIVE | 60,8% | 69,4% |

Với hai build này, WRECKER tốt nhất 78,8% và MIRROR tốt nhất 53,8%: chênh **25 điểm** trên 500 seed. Đây là đủ để bác bỏ v2 theo cả gate 70% và gate chênh giữa bóng 15 điểm. Chênh A3/B3 riêng EMBER là 13,6 điểm, HIVE 8,6 điểm; hai build này không có Tài năng nên chênh xuất phát từ nhánh Kỹ năng cấp 3 và cách nó tương tác với vũ khí.

SPARKLER ở cặp V1/P2/G2/không START: FINALE A 58,4%, B 50,0%, chênh 8,4 điểm trên cùng 500 seed. Mức này sát ngưỡng 8 điểm và chỉ là một cặp, nên cần đo bằng cặp kết quả theo từng seed trước khi kết luận nhánh cuối mất cân bằng.

Một kiểm tra phụ dùng Random bot trên 200 seed 1501–1700 với build không FINALE A3. Tỷ lệ thắng từ 7,5% (EMBER) đến 29% (WRECKER); policy này chọn card yếu thường xuyên nên **không so trực tiếp** với Greedy 500 seed. Nó vẫn cho thấy thứ tự sức mạnh nền không đảo chiều. Gate đầy đủ cho phương án đã chỉnh vẫn cần 500 seed × cả 18 build × tám bóng và cả hai policy.

## Fight nào tạo khoảng cách?

Chạy lại build không FINALE B3 trên đúng 500 seed 1501–2000, xuất từng fight với boss, arena, vũ khí địch, retry và kết quả (`bb12/harness-v2/results-validation/root-heavy-B-fights.csv`). Tỷ lệ dưới đây tính theo **lượt đánh boss đã tới được**, kể cả retry, nên không phải xác suất thắng toàn Run.

| Bóng | SHELL KING | MAGNET | MIMIC | WARDEN |
|---|---:|---:|---:|---:|
| MIRROR | 78,9% (275 lượt) | 75,7% (268) | **47,8%** (249) | 56,5% (262) |
| WRECKER | 88,5% (244) | 84,0% (257) | **90,5%** (221) | 81,2% (239) |

MIRROR thua chủ yếu ở boss cuối: fight 8 thắng 52,3% số lượt, trong khi fight 5–7 đều trên 96%. Riêng MIMIC sao chép MIRROR khiến kết quả gần 50/50; WRECKER gặp MIMIC vẫn thắng 90,5%. Điều này phù hợp với giả thuyết rằng sức mạnh nhận từ card/node của MIRROR không chuyển thành lợi thế rõ trong kèo gương. Cần kiểm cơ chế reflect/rage của hai Aegis và dữ liệu card tại fight 8 trước khi chỉnh; bảng này chỉ xác định chỗ lệch, chưa chứng minh nguyên nhân mã nguồn.
