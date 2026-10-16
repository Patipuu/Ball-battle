# BB-12: cây skill 8 bóng — bản dựng lại để đo

**Trạng thái: bản thử đã bị loại qua phép đo LV10; chưa triển khai vào game.** Bản này nối tiếp yêu cầu của Phú trong [phiên Claude](https://claude.ai/share/99d04e2e-573c-4a91-a806-58974bbdd45a). File và kết quả `TreeLab` của cloud không xuất được; mọi con số bên dưới là **giá trị thử nghiệm cục bộ**, không phải số Claude đã chốt. Repo hiện tại ở `86023c5`, Rules 13. Kế hoạch rương/shard/cấp bóng vẫn ở `bb12-ball-progression.md`. Baseline và các sweep node nằm ở `bb12-tree-baseline.md`; kết luận LV10 nằm ở `bb12-tree-final-measurement.md`.

## 1. Luật cây chung

- Mỗi bóng LV1–10 có đúng số điểm bằng cấp bóng: LV1 có 1 điểm, LV10 có 10. Điểm thuộc riêng từng bóng. Người chơi tự cộng, hoàn điểm và đổi nhánh miễn phí ở màn BALLS khi không ở trong run. Khi vào run, lưu bản build và khóa nó tới hết run; save cũ phải được chuyển đổi rõ ràng.
- Điểm chưa tiêu được giữ lại. Vì START tốn 2 và KEYSTONE tốn 3, màn BALLS phải hiển thị cả điểm còn lại và giá node; không tự tiêu điểm khi lên cấp.
- Tầng 1: **VITALITY** 0–2 rank, **POWER** 0–2 rank, **GROWTH** 0–2 rank. Mỗi rank tốn 1 điểm. VITALITY đang thử quanh +4–5% HP gốc/rank ở đầu run; POWER đang thử +6 điểm phần trăm sát thương/rank (cùng đường `DamagePct` của thẻ DAMAGE). Các hệ số chung có thể phải tách theo bóng sau holdout; WRECKER đã vượt 70% với riêng hai rank. GROWTH có tác dụng riêng cho từng bóng ở bảng dưới. Không cộng thêm +2% HP/+1% damage tự động mỗi cấp; điều đó sẽ cộng sức mạnh hai lần và làm lựa chọn điểm kém ý nghĩa.
- Tầng 2 mở khi đã đầu tư **2 điểm ở tầng 1**: skill đặc trưng LV2 nhánh A **hoặc** B tốn 1 điểm; **START** tốn 2 điểm, cấp một lợi thế đầu trận riêng cho bóng. START có thể lấy trước skill LV2.
- Tầng 3 mở khi đã đầu tư **5 điểm ở tầng 1+2**: skill LV3 tốn thêm 1 điểm và giữ nhánh A/B đã chọn; KEYSTONE chọn đúng một trong A/B, tốn **3 điểm**. KEYSTONE có cả lợi và hại, mở lối chơi khác nhau. Việc lấy KEYSTONE không bắt buộc phải lấy skill LV3.
- Tổng chi phí tối đa của một nhánh là `6 tầng 1 + 2 skill + 2 START + 3 KEYSTONE = 13`; LV10 có 10 điểm nên phải bỏ ít nhất 3. Chi phí KEYSTONE 3 là điều chỉnh của bản dựng lại để khớp mục tiêu 13 điểm. Claude chưa đo mô hình này. Số 76 build ở Claude thuộc luật cũ, **không dùng**.
- Đếm độc lập với luật ứng viên này cho 3/26/37/76 build dùng đủ lần lượt 1/4/7/10 điểm. Con số 76 ở LV10 tình cờ bằng con số Claude từng in với một bộ chi phí khác; nó không xác nhận mô hình hay độ cân bằng của Claude.
- Build hợp lệ phải có thứ tự mua hợp lệ; khi hoàn điểm, tự hoàn cả node phụ thuộc đã mất điều kiện. Bản xem trước hiện rõ HP, damage, chỉ số tăng trưởng, skill level/nhánh và các mặt trái của KEYSTONE trước khi lưu.
- Trong Run thường, bỏ thẻ SKILL UP khi cây được triển khai; SWAP lấy build của bóng mới. Versus/PvP không nhận tiến trình vĩnh viễn. Đường debug `-bbForceSkill` và run đang lưu dở cần được xử lý riêng theo `bb12-ball-progression.md`.

## 2. Node riêng cho từng bóng

Giá trị trong cột là **giá trị khởi tạo cho lần đo đầu**. Mọi tỉ lệ áp trên tham số gốc, không cộng dồn lũy tiến theo từng hit trừ khi nói rõ; các giới hạn hiện có vẫn giữ, trừ khi KEYSTONE nêu giới hạn mới. Tên UI là đề xuất.

| Bóng | GROWTH (mỗi rank) | START (2 điểm) | KEYSTONE A (3 điểm) | KEYSTONE B (3 điểm) |
|---|---|---|---|---|
| **EMBER** `blade` | **Rèn nóng:** `BladeDamagePerHit` +5% gốc/rank; mức gốc 1 mỗi hit. | **Lưỡi nung:** `CurrentDamage` khởi đầu +0.5. | **Lửa bền:** tăng trưởng mỗi hit +35%, sát thương khởi đầu −20%. | **Chớp lửa:** sát thương khởi đầu +25%, tăng trưởng mỗi hit −25%. |
| **MANTIS** `fang` | **Giữ nhịp:** hệ số giữ `SpinGain` sau hit 0.92 → 0.94/0.96 theo rank; trần spin không đổi. | **Lấy đà:** spin khởi đầu +2 độ/tick. | **Cối xay:** trần spin +20%, damage của hit −20%. | **Sát thủ:** damage mỗi hit +20%, trần spin −15%. |
| **ANGLER** `pike` | **Kéo dài:** `PikeGrowthPerHit` +4% gốc/rank; ảnh hưởng cả length và damage như code hiện tại. | **Mũi dài:** length khởi đầu +3. | **Tầm xa:** chiều dài +20%, damage −20%. | **Xuyên giáp:** damage +25%, chiều dài −15%. |
| **WRECKER** `brawler` | **Quán tính:** `BrawlerDamagePerSpeedPerHit` +5% gốc/rank; speed cap giữ nguyên. | **Bứt tốc:** hệ số damage theo tốc độ khởi đầu +0.005. | **Cú húc:** hệ số damage theo tốc độ +20%, wall speed boost −20%. | **Nảy tường:** wall speed boost +25%, hệ số damage theo tốc độ −15%. |
| **SPARKLER** `volley` | **Nạp đạn:** `VolleyArrowDamagePerHit` 0.10 → 0.125/0.15 theo rank. | **Nòng đôi:** bắt đầu với 2 arrows thay vì 1. | **Đạn nặng:** arrows bắn ra −1 (tối thiểu 1), sát thương mỗi arrow +40%. | **Mưa đạn:** arrows bắn ra +2 (tối đa 10), sát thương mỗi arrow −25%. |
| **SPORE** `venom` | **Độc đậm:** `VenomDps` +8% gốc/rank cho độc do SPORE gây; thẻ DAMAGE tiếp tục nhân lên sau đó. | **Mầm độc:** cú đánh đầu gây thêm một stack độc nếu chưa chạm giới hạn. | **Độc cấp:** DPS độc +25%, thời gian độc −20%. | **Âm ỉ:** thời gian độc +25%, DPS độc −15%. |
| **MIRROR** `aegis` | **Khiên rộng:** `AegisWidthPerHit` +20% gốc/rank, giữ trần width 14. | **Khiên sẵn:** width khởi đầu +0.5. | **Pháo đài:** width khởi đầu +15%, reflect −20%. | **Phản kích:** reflect +25%, width khởi đầu −15%. |
| **HIVE** `rig` | **Đồng bộ:** chu kỳ bắn tháp giảm 2 tick/rank từ 68 (tối thiểu 64). | **Triển khai sẵn:** đặt 1 turret tại vị trí xuất phát. | **Pin lớn:** shot damage +30%, chu kỳ bắn +15%. | **Bầy tháp:** chu kỳ bắn −15%, shot damage −20%. |

### Các tương tác phải đo riêng

- **SPORE/MIRROR:** BAL3 đưa POWER/DAMAGE tới poison và reflect trong Run; không được nhân hai lần. Trong Versus, `DamagePct` từ tiến trình luôn bằng 0. Đo riêng boss MIMIC và WARDEN vì đây là chỗ yếu theo `docs/claude-runs/bbbal3.md`.
- **MANTIS:** GROWTH tác động vào hệ số suy giảm SpinGain, không trực tiếp cộng tốc độ vô hạn. KEYSTONE A cần kiểm tra trần spin với FRENZY cấp 3.
- **SPARKLER:** số arrows bắn thực tế từ KEYSTONE B có thể hơn trần `Arrows` tích lũy; harness giới hạn 10 projectile/volley. Cần đo hiệu năng trước khi chốt.
- **HIVE:** bản thử KEYSTONE dùng nhịp bắn và shot damage để tránh thay kích thước mảng turret trước khi chứng minh giá trị của hai nhánh.
- **EMBER/ANGLER/WRECKER:** sức mạnh tăng theo số hit hoặc thời gian di chuyển. Đo cả fight 1 và boss dài để tránh node chỉ tốt ở một phía.
- **KEYSTONE:** nhánh A/B phải được so trong cùng seed, cùng cấp, cùng tổng điểm; nếu một nhánh thắng đều trên mọi chỉ số thì chỉnh mặt trái hoặc làm lại tác dụng.

## 3. Kế hoạch chứng minh cân bằng

1. Dựng harness cục bộ từ đúng Sim Rules 13. Lập seed cố định trước khi chỉnh số. Đo baseline không cây và sau khi bỏ SKILL UP cho 8 bóng, cả Greedy và bot đơn giản; không lấy số BAL3 làm baseline mới vì BAL3 vẫn có SKILL UP.
2. Đo riêng mỗi node và từng rank ở LV1/4/7/10 bằng **cùng seed** để giảm nhiễu. Sau đó quét build hợp lệ, lưu số run, seed, bot policy, commit/source hash, win %, fights won, boss win %, arena, opponent, thời gian, CSV thô. Nêu khoảng tin cậy; vài trăm run chỉ đủ sàng lọc, không đủ khẳng định chênh 1–2 điểm.
3. Gate ban đầu: không node nào là lựa chọn bắt buộc ở mọi build tối ưu; không KEYSTONE nào thắng nhánh còn lại ở mọi boss/arena; LV10 phải mạnh hơn LV1 cùng bóng; không bóng nào bị tụt khi nâng cấp đúng cách. So sánh chênh lệch **giữa 8 bóng** và riêng MIMIC/WARDEN. Mốc tuyệt đối LV1 ≥25% trong plan cũ phải được đánh giá lại: BAL3 hiện có nhiều bóng dưới 25% ngay khi vẫn được nhặt SKILL UP.
4. Tối đa hai vòng chỉnh giá trị theo ràng buộc cũ của dự án; vòng cuối chạy seed holdout chưa dùng khi tune. Nếu chưa đạt, báo rõ bóng/node nào còn lệch thay vì tuyên bố cân bằng. Chỉ sau đó mới đưa hiệu ứng vào Sim chính, tăng Rules, cập nhật golden và save migration.

## 4. Điều cần quyết trước khi triển khai

- KEYSTONE 3 điểm là đề xuất để giữ tổng 13. Nếu muốn giữ chi phí KEYSTONE 2 theo bản Claude cuối, tổng cây là 12; phải cập nhật mô tả và đếm lại build.
- Giá trị bảng trên là đầu vào để đo. Chốt số cuối bằng kết quả harness, không dựa vào cảm giác hoặc ảnh mockup.
