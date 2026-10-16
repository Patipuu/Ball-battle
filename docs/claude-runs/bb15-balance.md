# BB-15: cân bằng Run sau khi thêm hai bóng

Trạng thái: **đã chỉnh và đo, chưa commit**. Rules 16.

## Cách đo

- `SimTests/Bb15TreeBalanceSweep.cs` chạy Greedy RunBot, không tap, với cùng seed cho từng build: LV1 cây trống và đủ 18 cây LV10 tiêu hết 10 điểm. File CSV ghi số thắng của từng build.
- Tập chọn thông số: seed **3001–3200**, 200 Run mỗi build. Tập kiểm tra độc lập: seed **3201–3400**, cũng 200 Run mỗi build cho hai bóng được chỉnh.
- Lệnh: đặt `BB_BAL_BALLS=brawler,chorus`, `BB_BAL_SEEDS=200`, `BB_BAL_START=3001` (hoặc `3201`), `BB_BAL_REPORT=<đường dẫn CSV>` rồi chạy `dotnet test BallBattleUnity/SimTests/SimTests.csproj --filter FullyQualifiedName~Bb15TreeBalanceSweep`.

## Thay đổi

- WRECKER: hệ số sát thương lúc đầu của riêng Run ×0,90 → **×0,85**; tăng trưởng mỗi đòn của riêng Run ×0,95 → **×0,90**. Versus và WRECKER làm địch không mang cây vẫn dùng số cũ.
- BGLGXLQ: nhánh CHORUS hồi máu mỗi UNITY trong Run giảm từ 1,5%/2,25% xuống **1%/1,5%** máu tối đa ở skill LV2/LV3. Versus giữ số BB14 cũ. RADIANCE không đổi.
- `SimVersion.Rules` 15 → **16**; hash vàng của tám bóng cũ đã được cập nhật cho Rules mới. Hash che Rules của các trận đó không đổi.

## Kết quả 200 seed

| Bóng | LV1 | LV10 tốt nhất trước BB15 | LV10 tốt nhất sau BB15 | Nhánh A | Nhánh B |
|---|---:|---:|---:|---:|---:|
| EMBER | 28,0% | 60,0% | 60,0% | 59,5% | 60,0% |
| MANTIS | 25,0% | 60,0% | 60,0% | 60,0% | 59,5% |
| ANGLER | 26,5% | 67,5% | 67,5% | 67,5% | 67,0% |
| WRECKER | **41,5%** | 76,0% | **68,5%** | 66,0% | 68,5% |
| SPARKLER | 37,0% | 68,0% | 68,0% | 67,0% | 68,0% |
| SPORE | 14,0% | 68,5% | 68,5% | 66,5% | 68,5% |
| MIRROR | 19,5% | 60,0% | 60,0% | 59,5% | 60,0% |
| HIVE | 37,0% | 70,0% | 70,0% | 70,0% | 66,5% |
| BGLGXLQ | **14,5%** | 73,0% | **69,5%** | 69,5% | 65,0% |
| HUNTER | 17,5% | 61,0% | 61,0% | 61,0% | 59,0% |

WRECKER và BGLGXLQ sau chỉnh được đo từ `bb15-final-200.csv`. Tám bóng còn lại lấy từ `bb14.md`: thay đổi BB15 chỉ tác động khi chính WRECKER/BGLGXLQ là người chơi Run; địch không có cây và trị số Versus của BGLGXLQ được giữ nguyên. Vì thế kết quả trận của tám bóng đó không đổi, dù hash có Rules mới.

Các gate LV10 hiện đạt: build mạnh nhất **70,0%** (ngưỡng ≤70), khoảng cách bóng mạnh nhất/yếu nhất **10 điểm** (ngưỡng ≤15), chênh A/B lớn nhất **4,5 điểm** (ngưỡng ≤8).

## Seed độc lập và test

- WRECKER: LV10 tốt nhất **66,0%**, A/B **66,0% / 64,0%**, LV1 **41,0%**.
- BGLGXLQ: LV10 tốt nhất **65,5%**, A/B **65,5% / 65,0%**, LV1 **10,5%**.
- CSV: `bb15-final-200.csv`, `bb15-holdout-200.csv`. SimTests: **351 đạt, 0 lỗi**. Unity EditMode: **550 test, 531 đạt, 0 lỗi, 19 bỏ qua** (test chụp ảnh/sweep chỉ chạy khi bật tùy chọn); XML ở `bb15-editmode.xml`.

## Điểm còn mở

- LV1 vẫn lệch đáng kể, nhất là BGLGXLQ, SPORE và HUNTER so với WRECKER. Gate BB12g/BB14 chỉ quy định LV10; cần thiết kế mục tiêu LV1 riêng trước khi thay đổi sức mạnh mở màn, vì tăng LV1 có thể đẩy LV10 vượt ngưỡng.
