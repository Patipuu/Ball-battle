# Brainstorm — Step 2: chiều sâu chiến thuật & đa dạng (2026-10-09)

## Vấn đề
Bản 1: quyết định duy nhất = chọn 2 vũ khí → kết quả ≈ tung đồng xu có lệch khắc chế. Xem vui, không lý do chơi lại.
Game auto-battler (không input trong trận) → chiều sâu phải đến từ **quyết định trước/giữa trận** (mô hình Super Auto Pets, Backpack Battles, TFT).
Lợi thế riêng: sim tái lập + công cụ chạy 10.000 trận/30 s → mọi cơ chế mới đo được cân bằng ngay.

## Các hướng đã cân nhắc
| # | Hướng | Chiều sâu | Đa dạng | Chi phí | Rủi ro |
|---|---|---|---|---|---|
| A | Trait gắn bóng (1–2/bóng) | Cao | Cao | TB | Bùng nổ tổ hợp |
| B | Vũ khí trục mới (bắn xa, độc, phản đòn, turret) | TB | Cao | TB | Cần đạn + trạng thái |
| C | Arena biến thể | TB | Cao | Thấp | Ít |
| D | Run mode roguelite | Rất cao | Rất cao | Cao | Cân bằng độ khó, lưu tiến trình |
| E | Đội 2v2/3v3 | Cao | Cao | Cao | Khó đọc trên 270 px |
| F | Dự đoán/cược | Thấp | Thấp | Thấp | Không thêm chiều sâu |
| G | "Tính cách AI" (tự lao tới) | TB | Thấp | Thấp | Mất chất vật lý thuần |

## Quyết định (user chốt 2026-10-09)
- **Khung: D — Run mode roguelite.**
- **Nội dung: A (≈10 trait) + B (4 vũ khí mới) + C (arena biến thể).**
- **Trong trận: auto thuần** (giữ tái lập, replay, chất clip gốc).
- E (đội) để Step 3. F, G loại.

## Thiết kế phác — Run mode
1. Chọn 1/3 vũ khí ngẫu nhiên (trong số đã mở khóa).
2. Run ≈ 8 trận / 3 màn; cuối màn: boss bóng khổng lồ (HP cao, bản "Super").
3. Trước mỗi trận: **xem trước đối thủ** (vũ khí, trait, arena) → chọn 1/3 thẻ: thêm trait / nâng chỉ số khởi đầu / đổi vũ khí; hoặc trả xu đổi bộ thẻ.
4. Trận tự đánh, nút xem nhanh ×2. Mục tiêu ≤ ~45 s/trận.
5. 3 mạng; thua mất 1; hết mạng → hết run.
6. Mở khóa vũ khí/trait theo thành tích → mỗi run khác nhau.

## Trait mẫu
| Trait | Hiệu ứng | Khắc / bị khắc |
|---|---|---|
| Heavy | To hơn, bị đẩy ít, chậm hơn | Khắc Brawler; thua tầm xa |
| Spiky | Chạm thân mất máu | Khắc Brawler / thân-thân |
| Vampire | Hồi 20% sát thương gây ra | Khắc độc; thua phản đòn |
| Thorns | Phản 25% sát thương cận chiến | Khắc Blade đã mạnh |
| Second Wind | 1 lần: ≤30% HP hồi 25 | Lật kèo |
| Glass Cannon | ×1.5 sát thương, −40 HP | Rủi ro cao |
| Parry Master | Parry +1 chỉ số | Khắc Fang |
| Bubble | Chặn đòn đầu mỗi 8 s | Khắc đánh lẻ tẻ |
| Twin Blade | Lưỡi thứ 2 đối diện, dài 60% | Phủ rộng, dễ bị parry |
| Poison Tip | Đòn gây độc 1/s × 3 s, cộng dồn | Khắc hồi máu chậm |

## 4 vũ khí mới (tên/art tự thiết kế; chỉ mượn ý tưởng cơ chế)
- **Bắn xa:** mỗi đòn trúng +1 mũi tên. Mạnh sân to, yếu sân nhỏ → arena thành quyết định.
- **Độc:** cộng dồn DoT. Khắc hồi máu; thua dồn sát thương nhanh.
- **Phản đòn:** không tự gây sát thương; phản khi parry. Đối thủ càng mạnh càng mạnh.
- **Turret:** xây tường/súng trên sân. Kiểm soát không gian; thua dồn sát thương sớm.

## Arena biến thể
Cột giữa, bumper tăng tốc, tường gai, trọng lực thấp, sân nhỏ, sân to. Hiện ra **trước** khi chọn thẻ.

## Hệ quả kỹ thuật
- Sim: thực thể đạn; hiệu ứng trạng thái (độc, khiên); móc sự kiện cho trait (OnHit, OnDamaged, OnWall, OnParry, Tick); vật cản tĩnh trong arena — vẫn tái lập.
- Cân bằng: không đo hết vũ khí × trait × arena → đo **tỉ lệ thắng của mỗi trait trên mọi tổ hợp còn lại** bằng lấy mẫu; mở rộng MatchupReport/BalanceSweep; test ngưỡng tự động.
- Màn hình: chọn thẻ, bản đồ run, xem trước đối thủ; lưu tiến trình (mở khóa).
- Ước lượng thô: 2–3× khối lượng bản 1.

## Rủi ro
1. Rối mắt trên 270 px → ký hiệu thị giác rõ cho đạn/độc/khiên/turret.
2. Lạm phát sức mạnh (combo trait) → giới hạn số trait + test ngưỡng.
3. Run quá dài → trận ≤ ~45 s, xem nhanh ×2.

## Câu hỏi còn mở (chốt ở bước plan)
- Số trait tối đa mỗi bóng (đề xuất 3) và có nâng cấp trait (cấp 1→2) không.
- Kinh tế xu: kiếm bao nhiêu/trận, giá đổi thẻ.
- Độ khó đối thủ tăng theo màn: thêm trait hay thêm HP/chỉ số.
- Lưu tiến trình: file local (PlayerPrefs/JSON) — đủ cho Step 2?
- Thứ tự làm: sim (trait/đạn/arena) trước hay run mode UI trước.

## Quyết định cho câu hỏi mở (Claude tự quyết 2026-10-09, user hiệu chỉnh sau)
1. **Trait:** tối đa **3 trait/bóng**. Chọn lại trait đã có → **lên cấp 2** (mạnh hơn, vẫn chiếm 1 ô). Tối đa cấp 2. Lý do: 3 ô đủ tạo build, vẫn đọc được trên HUD 270 px; cấp 2 thưởng cho việc theo đuổi một hướng.
2. **Kinh tế xu:** bắt đầu 2 xu; **thắng +3, thua +1** (thua vẫn có xu để gỡ). Mỗi trận được chọn **miễn phí 1 thẻ / 3**. **Đổi bộ thẻ: 1 xu. Mua thêm 1 thẻ: 3 xu.** Đơn giản, một loại tiền, tiêu ngay trong run (không mang qua run).
3. **Độ khó:** chủ yếu **thêm trait** (đọc được, tạo bài toán khắc chế); chỉ số chỉ tăng nhẹ.
   - Màn 1 (trận 1–2): 0–1 trait. Boss 1 (trận 3): bản Super, 250 HP.
   - Màn 2 (4–5): 1–2 trait, +10% HP. Boss 2 (6): Super + 2 trait, 350 HP.
   - Màn 3 (7): 2–3 trait (có cấp 2), +20% HP. Boss cuối (8): Super + 3 trait, 500 HP.
4. **Lưu tiến trình:** **JSON trong Application.persistentDataPath** (JsonUtility): mở khóa, kỷ lục, cài đặt, và run đang dở (để chơi tiếp). Đủ cho Step 2; cloud save để sau.
5. **Thứ tự:** **sim nền trước** (móc trait, trạng thái, đạn, vật cản) → **lát cắt dọc chơi được sớm** (run mode tối thiểu với 4 vũ khí cũ + 3 trait + 1 arena) → lấp nội dung (đủ trait, 4 vũ khí, arena) → ngôn ngữ hình ảnh → cân bằng độ khó. Lý do: run mode cần nội dung để có nghĩa, nhưng phải chơi thử vòng lặp sớm để biết có vui không trước khi đổ công làm nội dung.
6. **Tái lập run:** run có seed; cùng seed run + cùng lựa chọn → cùng đối thủ, cùng thẻ, cùng kết quả (replay/chia sẻ seed).
