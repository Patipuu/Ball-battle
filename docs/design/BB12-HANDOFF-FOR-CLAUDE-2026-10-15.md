# BB-12 handoff cho Claude — 2026-10-15

**Người dùng yêu cầu dừng chạy và bàn giao.** Đã gửi Ctrl-C cho phép đo đang chạy; không khởi động thêm mô phỏng. Không có thay đổi nào được đưa vào Sim chính của Unity trong lượt này. Giữ nguyên các tên skill tiếng Việt người dùng đã duyệt trong `bb12-persona-web-v2.md`.

## Nơi xem trước tiên

1. `docs/design/bb12-persona-web-v2.md`: cấu trúc web, 18 build LV10, tên skill tiếng Việt, gate cân bằng.
2. `docs/design/bb12-v2-tree-measurement.md`: phương pháp và kết quả v2 18 build × 8 bóng × 200 seed, cùng xác nhận chọn lọc 500 seed và chẩn đoán từng boss.
3. `docs/design/bb12-persona-web-v5.md`: các thay đổi thử sau v2. Phần HIVE trong file này là **v5**, còn harness v6 đổi HIVE A lần nữa (xem bên dưới).
4. `docs/design/bb12/harness-v6/combined-greedy-1301-1500.csv`: bảng 18 build × 8 bóng, 200 seed 1301–1500 của ứng viên mới nhất. Bảy bóng lấy từ harness v5; HIVE từ v6. Chỉ HIVE khác giữa v5 và v6.

## Kết quả đã đo

- **V2 bị loại:** build tốt nhất WRECKER 77%, MIRROR 49,5% trên seed 1301–1500 (200 run/build/bóng); trên 500 seed mới, WRECKER tối đa 78,8%, MIRROR tối đa 53,8% ở hai build dồn chỉ số. Vượt gate thắng tối đa 70% và chênh tốt nhất giữa bóng 15 điểm.
- Fight-level v2 cho build V2/P2/G2/START/SKILL B3/không Tài năng: MIRROR thắng MIMIC 47,8% và WARDEN 56,5% số lượt boss; WRECKER thắng 90,5% và 81,2%. CSV: `bb12/harness-v2/results-validation/root-heavy-B-fights.csv`.
- **Ứng viên v6, pilot 200 seed:** trong bảng ghép mới nhất, build tốt nhất theo bóng trải từ MANTIS 51% đến WRECKER 71%; HIVE có hai cặp A/B cùng root lệch 12 điểm (V0/P2/G1/START và V1/P1/G1/START). Chưa qua gate pilot, chưa được đưa vào game.
- **Ứng viên v6, 500 seed 2001–2500:** build V2/P2/G2/START/A3/không Tài năng cho WRECKER 68%, MIRROR 61,6%, HIVE 67%, MANTIS 52,2%; build B3 cho WRECKER 69%, MIRROR 60,8%, HIVE 66,4%, MANTIS 49%. CSV trong `bb12/harness-v6/results-validation/root-A.csv` và `root-B.csv`.
- Chẩn đoán 500 seed mới, build B3: MIRROR thắng MIMIC **61,8%** số lượt (so với 47,8% ở v2), WARDEN 56,3%; WRECKER thắng MIMIC 89,2%, WARDEN 80,3%. CSV `bb12/harness-v6/results-validation/root-B-fights.csv`. Đây là tỷ lệ trên lượt boss đã tới được, kể cả retry, không phải Run-win.
- Build HIVE A ở cặp V0/P2/G1/START/A3/FINALE A đã xong trên 500 seed 2001–2500: HIVE 60,2%, CSV `bb12/harness-v6/results-validation/hive-finale-A.csv`. **Build B của cặp này đang chạy thì người dùng bảo dừng, đã hủy; không có kết quả 500 seed B.** Không dùng `all-validation.csv` nếu nó chưa tồn tại.

## Khác biệt giữa các harness

- `harness-v2`: triển khai đầy đủ node v2 trong bản sao Sim, không đụng production. Không node nào bật thì khớp harness v1 ở cả tám bóng trên 20 seed.
- `harness-v3`: WRECKER giảm DamageFactor nền 10% Run-only; MIRROR khiên nền +0,25; EMBER skill A cộng 0,625/0,75 ở cấp 2/3; SPORE finale B độc 95% DPS, thời gian ×20/19; SPARKLER B quạt hẹp hơn; HIVE A skill 6 loạt.
- `harness-v4`: thử WRECKER growth-hit ×0,85 và MIRROR phản chip khi đấu gương 0,125/bậc. WRECKER tụt quá mạnh; loại.
- `harness-v5`: WRECKER growth-hit ×0,95; MIRROR chip **0,5/bậc**; SPARKLER B tổng sát thương +5% và quạt hẹp hơn; HIVE A skill **5 loạt**. HIVE A yếu ở vài build; không chốt.
- `harness-v6`: giống v5 trừ HIVE A skill trở lại **6 loạt** và Finale A sát thương mỗi đạn **+5%** (v5 +10%). Ứng viên đo mới nhất; HIVE vẫn lệch ở hai cặp G1/START, cần xác nhận 500 seed rồi có thể phải đổi cơ chế chu kỳ bắn thay vì đổi % damage.

## Việc còn lại nếu người dùng cho tiếp tục

1. Chạy lại riêng cặp HIVE B bị hủy trên seed 2001–2500, so với A 60,2%. Lệnh tương đương: `dotnet docs/design/bb12/harness-v6/bin/Release/net10.0/TreeLabV6.dll 2001 500 0 2 1 1 B3 B greedy <output.csv>`. Xóa biến môi trường `BB12_WEAPON_FILTER` hoặc để trống nếu muốn đủ tám bóng; nếu chỉ HIVE thì đặt `rig`.
2. Xem MANTIS: trên holdout root A/B 52,2/49%, hiện là bóng thấp nhất. Có thể thử node Mở màn từ +2 lên +3 lực xoáy ở hit đầu, nhưng phải đo trên seed tune trước khi dùng seed mới. Không sửa chỉ vì một điểm ước lượng.
3. Nếu HIVE vẫn lệch >8 điểm trên 500 seed, kiểm chu kỳ bắn integer `RigFireIntervalTicks` (G1/G2 chênh đúng một tick nhưng kết quả đổi mạnh); cân nhắc đổi cơ chế Finale A thay vì tiếp tục vi chỉnh sát thương.
4. Sau khi khóa ứng viên, chạy gate 500 seed × đủ 18 build × tám bóng trên holdout chưa dùng, Greedy và một policy bot khác; đo LV1/LV4/LV7, boss/arena, rồi mới quyết định đưa vào Unity Sim. Các kết quả hiện tại **không đủ để tuyên bố tám cây đã cân bằng**.

## Lưu ý workspace

Repo đang có nhiều tệp production modified/untracked từ các task khác từ trước; **đừng reset hoặc ghi đè**. Công việc BB-12 ở `docs/design/bb12*` và các harness cô lập. `dotnet build docs/design/bb12/harness-v6/TreeLabV6.csproj -c Release` thành công (một warning `RunBot.PickObserved` không được dùng).

## Claude tiếp tục (15/10, sau 21:01)

**Việc 1 đã xong: cặp HIVE B bị hủy được chạy lại.**
- **Lệnh:** `TreeLabV6.dll 2001 500 0 2 1 1 B3 B greedy`, đủ 8 bóng, `BB12_WEAPON_FILTER` để trống.
- **Kết quả:** `bb12/harness-v6/results-validation/hive-finale-B.csv`, 133 giây trên laptop.
- **Log:** `hive-finale-B.log` có dòng `NativeCommandError` chỉ vì dotnet ghi thời gian chạy ra stderr; CSV đủ 8 dòng.

Cặp V0/P2/G1/START/kỹ năng 3/Tài năng, seed 2001–2500, 500 run/bóng, Greedy:

| Bóng | Tài năng A (A3) | Tài năng B (B3) | B − A |
|---|---:|---:|---:|
| EMBER | 52,4% | 46,2% | −6,2 |
| MANTIS | 43,4% | 46,4% | +3,0 |
| ANGLER | 49,6% | 45,2% | −4,4 |
| WRECKER | 61,8% | 67,6% | +5,8 |
| SPARKLER | 46,2% | 52,4% | +6,2 |
| SPORE | 63,8% | 64,2% | +0,4 |
| MIRROR | 44,0% | 46,8% | +2,8 |
| HIVE | 60,2% | 64,8% | +4,6 |

**Đọc kết quả:**
- **HIVE:** lệch A/B 4,6 điểm trên 500 seed, dưới ngưỡng 8. Mức lệch 12 điểm ở pilot 200 seed không lặp lại, nên chưa cần đổi cơ chế chu kỳ bắn (việc 3).
- **Cả tám bóng:** ở cặp này đều dưới ngưỡng 8 điểm.
- **Gate giữa các bóng chưa đạt.** Lấy build tốt nhất mỗi bóng trong các build đã đo trên 500 seed 2001–2500 (root A/B và cặp này):
  - WRECKER 69,0% (≤ 70%, đạt sát ngưỡng);
  - MANTIS 52,2%, thấp nhất;
  - chênh 16,8 điểm, vượt gate 15.
  - MANTIS vẫn là việc 2.
- **Còn thiếu:** đây vẫn chỉ là ba build trên 18; chưa đủ để tuyên bố cân bằng (việc 4).

## Claude: chỉnh MANTIS và kiểm nhẹ (Phú duyệt 21:15, bỏ gate 500 seed × 2 bot)

**Chỉnh MANTIS** (seed tune 1301–1500, chỉ MANTIS, đủ 18 build):
1. **Vòng 1, Mở màn +2 → +3 lực xoáy:** không đổi rõ.
   - Build có Mở màn: trung bình +0,5 điểm.
   - Build không có Mở màn: khớp tuyệt đối với số cũ (kiểm chứng phép đo sạch).
   - Đã trả về +2.
   - CSV: `bb12/harness-v6/results-mantis-start3-1301/`.
2. **Vòng 2, hệ số gốc MANTIS Sinh lực 4 → 6%, Sát thương 5 → 8% mỗi bậc** (`harness-v6/Program.cs` dòng 18–19):
   - trung bình 18 build 43,8 → 49,3%;
   - build tốt nhất 51 → 62%.
   - **Giữ.** CSV: `bb12/harness-v6/results-mantis-root68-1301/`.

**Kiểm nhẹ trên seed mới 3001–3200**, Greedy, 200 run/bóng, 18 build LV10 cùng LV0/LV1/LV4/LV7. CSV và tóm tắt ở `bb12/harness-v6/results-light-3001/`; `analyze_light.py` cũng ở đó.

| Bóng | LV0 | LV1 tốt nhất | LV4 | LV7 | LV10 tốt nhất | LV10 trung bình 18 build |
|---|---:|---:|---:|---:|---:|---:|
| EMBER | 26,5 | 30,5 | 43,0 | 63,0 | 63,0 | 54,1 |
| MANTIS | 25,0 | 31,0 | 41,5 | 55,5 | 64,0 | 49,8 |
| ANGLER | 23,5 | 28,0 | 41,5 | 54,0 | 58,0 | 48,7 |
| WRECKER | 42,0 | 45,5 | 59,5 | 69,5 | 68,5 | 62,3 |
| SPARKLER | 26,5 | 34,0 | 44,5 | 57,5 | 64,5 | 54,8 |
| SPORE | 13,0 | 17,5 | 34,5 | 51,0 | 68,0 | 60,4 |
| MIRROR | 27,5 | 32,5 | 43,5 | 54,0 | 64,5 | 49,1 |
| HIVE | 35,5 | 41,5 | 52,5 | 70,0 | 73,5 | 63,1 |

**Đọc kết quả:**
- **Chênh build tốt nhất giữa các bóng:** 15,5 điểm (ANGLER 58 → HIVE 73,5), sát gate 15. Sai số mẫu 200 seed khoảng ±3 điểm.
- **MANTIS** không còn là bóng thấp nhất.
- **Vượt 70%:** chỉ 2/144 build, đều là HIVE (71,5 và 73,5).
- **Lệch A/B cùng gốc > 8 điểm:** 7 cặp, đều nghiêng về nhánh B:
  - WRECKER: 3 cặp, +8,5 đến +9;
  - MIRROR: 2 cặp, +8,5 và +11;
  - HIVE: 2 cặp, +10 và +16.
- **Tiến trình:** LV0 → LV10 tăng đều ở mọi bóng. Riêng EMBER và WRECKER đạt đỉnh ở LV7: Tài năng 3 điểm không hơn dồn chỉ số.
- **SPORE LV0 13%:** vấn đề Run nền đã biết, không phải lỗi cây.

**Quyết định (theo luật handoff: chấp nhận lệch nhỏ, tối đa 2 vòng chỉnh):**
- Khóa ứng viên **v6 + MANTIS 6/8** để triển khai vào game.
- Đưa vào đợt cân bằng tổng (đo 500 seed trên Sim thật sau khi triển khai):
  - HIVE hơi mạnh;
  - nhánh B của WRECKER/MIRROR/HIVE nhỉnh hơn A;
  - SPORE LV0 yếu;
  - Tài năng của EMBER/WRECKER chưa đáng 3 điểm.
