# Tám boss và sửa HUD đấu lại — 20/10/2026

## Cơ chế đã vào game

Mỗi run vẫn có hai trận boss (trận 4 và 8). Pool hiện có bốn boss cho mỗi mốc; run đầu giữ Vua Vỏ Cứng và Bóng Bắt Chước để hướng dẫn người chơi, các run sau chọn theo seed và tránh lặp boss của run trước ở cùng mốc.

| Trận 4 | Cách đối phó |
| --- | --- |
| Vua Vỏ Cứng | Khi dựng khiên, mặt trước chặn đòn và tung cú đập ngắn. Vòng ra sau; lúc hạ khiên, boss nhận thêm sát thương. |
| Nam Châm | Báo trước lực hút, kéo người chơi vào gần rồi nổ văng ra. Tránh tâm vào cuối pha hút. |
| Kẻ Săn | Đánh dấu vị trí cũ rồi lao tới đó. Đổi hướng sau khi thấy dấu. |
| Chuông Rền | Phát sóng tròn nở dần. Né qua mép sóng hoặc giữ khoảng cách. |

| Trận 8 | Cách đối phó |
| --- | --- |
| Bóng Bắt Chước | Sao chép vũ khí và kỹ năng chủ động, không sao chép trait. Sau 15 giây, kháng loại sát thương gây lên nó nhiều nhất. |
| Cai Ngục | Tường thu hẹp theo nhịp báo trước; phần tường vừa dịch chuyển có gai, vùng mép gây thêm sát thương. |
| Lò Than | Đánh dấu vị trí người chơi rồi đốt vùng đó trong thời gian ngắn. Rời dấu trước khi bùng cháy. |
| Lăng Kính | Báo làn ngang/dọc luân phiên, sau đó bắn qua làn đã khóa. Di chuyển khỏi làn trước khi tia xuất hiện. |

Bảy boss không còn quay vũ khí người chơi ngẫu nhiên: chúng dùng thân boss và đòn riêng. Bản Sao là ngoại lệ có chủ đích. Bốn boss mới có chân dung pixel, vùng báo đòn, tên và gợi ý tiếng Việt/Anh; các hiệu ứng âm thanh hiện dùng lại cue boss đã có.

## HUD đấu lại

Viền đấu lại từng cắt ngang dòng kỹ năng và đi qua nút bánh răng. Mép dưới nay ở giữa thanh hồi kỹ năng và nhãn trận; mép trên chừa vùng nút bánh răng. Màn chuẩn bị cũng xử lý `boss_core` như thân boss, không tra nó như một vũ khí chọn được.

Ảnh kiểm tra ở độ phân giải gốc: `docs/claude-runs/boss8-ui-check/vi-run-retry-countdown.png`.

## Kiểm tra

- Unity EditMode: 576/576 đạt.
- Kiểm tra mô phỏng: đủ 8 loadout, sự kiện boss, replay xác định, Bản Sao thích nghi đều đạt.
- Bot Greedy, seed 1–240: 62/240 run thắng (25,8%). Tỷ lệ thắng khi gặp từng boss: Vua Vỏ Cứng 78,8%; Nam Châm 75,9%; Kẻ Săn 83,3%; Chuông Rền 82,8%; Bóng Bắt Chước 51,6%; Cai Ngục 61,9%; Lò Than 61,8%; Lăng Kính 63,2%. Cỡ mẫu từng boss 19–36 trận, nên đây là kiểm tra sơ bộ, không thay cho chơi thử bằng người.
- Windows build: `Builds/Windows-boss8-20261020/BallBattle.exe`, thành công, 0 lỗi và 7 cảnh báo biên dịch hiện có.

Các thay đổi chưa được commit trong lượt này.
