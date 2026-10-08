# Project Overview / PDR — Ball Battle

## 1. Nguồn cảm hứng (nghiên cứu 2026-10-08)
Trend "Weapon Ball Battles" của Earclacks: video mô phỏng vật lý, bùng nổ TikTok/Twitter cuối 2025, tập đầu 14/07/2025 (Sword vs Dagger).
Nguồn: knowyourmeme.com/memes/weapon-ball-battles-earclacks, en.namu.wiki/w/Earclacks/Weapon%20Ball, scratch.mit.edu/users/Earclacks.

### Luật lõi rút ra
- 2+ bóng trong arena kín, có trọng lực, nảy tường; mỗi bóng có 1 vũ khí xoay quanh tâm.
- Vũ khí chạm thân địch → gây sát thương → bóng tấn công **tăng chỉ số** (snowball). Chỉ tăng khi đánh trúng (trừ ngoại lệ).
- Hai vũ khí chạm nhau → **parry**: cả hai khựng ngắn (hitstop), bật ra, đảo chiều xoay.
- HP mặc định 100 (bản "Super" 500). Hết HP → biến mất, mọi hiệu ứng nó để lại bị xoá. Bóng/đội cuối cùng thắng.
- Không có input người chơi trong trận.

### Archetype vũ khí (để thiết kế bộ riêng, không sao chép tên/art)
| Archetype | Quy tắc scale khi trúng | Điểm yếu cố ý |
|---|---|---|
| Sát thương tuyến tính | +1 dmg | đơn giản, thua parry giỏi |
| Tốc độ xoay | + tốc xoay (giảm dần), dmg cố định thấp | thua vũ khí không parry được |
| Tầm với | + dài + dmg 0.5 | càng dài càng dễ bị parry |
| Bắn đạn | + số đạn mỗi loạt | đầu trận yếu |
| DoT/độc | + stack độc | nhịp chậm |
| Phản đòn | phản dmg khi parry, không tự gây dmg | phụ thuộc đối thủ mạnh |
| Thân không vũ khí | dmg theo vận tốc, + tốc tối đa khi chạm tường | không parry được |
| Không thể bị parry | + trần dmg/tốc xoay, reset khi trúng | yếu khi đông địch |
| Đặt chướng ngại | đặt turret/tường | khởi động chậm |
| Hút máu | + lifesteal | đầu trận yếu |
| Chí mạng | + tỉ lệ/dmg chí mạng | may rủi |

Trục cân bằng: cận chiến ↔ tầm xa (kích thước arena quyết định), parry ↔ chống parry, nhịp nhanh ↔ khởi động chậm, ổn định ↔ may rủi.

### Chế độ trong series gốc
1v1 (Short ≤3 phút, có best-of-3), 1 vs 100 (sân co lại), Battle Royale 6–8 (gói hồi máu 25/50), Boss Raid (boss ~10.000 HP), Team 4v4, Dodgeball, Zombies (sóng), Dungeon, Tournament cuối mùa.

### Vì sao viral → yêu cầu sản phẩm
1. Luật hiểu trong 2 giây, kết quả khó đoán (vật lý + lật kèo).
2. Snowball nhìn thấy được: số chỉ số trên màn, vũ khí to/đổi màu.
3. Âm "clack", hitstop, rung màn khi va chạm.
4. Mỗi bóng có tính cách → cộng đồng tạo meme/lore.
5. Vừa khung Shorts; có mùa giải.
6. Thêm vũ khí = thêm 1 quy tắc scale → mở rộng dễ.

## 2. Định hướng sản phẩm
- 3 bản đầu: auto-battler (chọn trận → xem). Tương tác người chơi để sau.
- Engine: Unity 6000.5.1f1.
- Trận phải **tái lập được từ seed** (cần cho replay, cân bằng tự động, quay clip).
- Bản 1 (chốt 2026-10-08): mobile dọc 9:16, 4 vũ khí cân bằng, chế độ 1v1 (có best-of-3).
- Đồ họa: **pixel art** (đổi từ tối giản phẳng, 2026-10-08). Hệ quả kỹ thuật: Pixel Perfect Camera, render độ phân giải thấp rồi phóng nguyên lần; vũ khí xoay phải giữ lưới pixel (không bị răng cưa lệch ô).

## 3. Câu hỏi còn mở
- Độ phân giải gốc pixel (vd 180x320 hay 270x480), nguồn sprite (tự vẽ / tạo bằng AI / placeholder vẽ bằng code) — chốt ở bước plan.

