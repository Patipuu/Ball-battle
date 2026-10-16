# BB-12 — ứng viên cân bằng tiếp theo (harness v5)

Giữ **toàn bộ tên tiếng Việt và cấu trúc web** trong [`bb12-persona-web-v2.md`](bb12-persona-web-v2.md). File này chỉ ghi các tham số/cơ chế đổi sau khi đo v2; những node không có trong bảng vẫn dùng mô tả v2. Đây là ứng viên trong harness, **chưa thay Sim của Unity**.

| Bóng | Phần đổi so với v2 | Lý do từ phép đo |
|---|---|---|
| EMBER | Kỹ năng EDGE nhánh A: mỗi hit được cộng 0,625 sát thương ở cấp 2 hoặc 0,75 ở cấp 3, thay vì 0,5. | Ở build không Tài năng trên 500 seed, EDGE A3 thắng 47,2% còn HONE B3 60,8%; số hit cộng thêm của EDGE đến quá muộn. |
| WRECKER | Riêng Run: `DamageFactor` ban đầu ×0,90 và mức tăng `DamageFactor` sau mỗi hit ×0,95. Node **Húc lấy húc để** vẫn cộng 2%/bậc trên mức tăng đã chỉnh. | V2 có build LV10 đạt 78,8% trên 500 seed; chỉ giảm hệ số đầu trận chưa đủ, giảm mức tăng 15% lại làm build 71,5→58%. |
| SPARKLER | **Mưa pháo** vẫn thêm một tên (tối đa chín); quạt tên mới hẹp hơn 20% so với quạt của số tên cũ và tổng sát thương loạt tăng 5% thay vì giữ đúng bằng. | V2 nhánh A/B cùng root 58,4/50,0% trên 500 seed; chia cùng ngân sách damage khiến tên thêm khó trúng và không bù được nhánh kỹ năng. |
| SPORE | **Dai dẳng** gây 95% DPS và kéo thời gian ×20/19, thay vì 90% DPS và ×10/9. | Bản cũ mất sát thương đầu trận; trong vài build lệch A/B 9–11 điểm. |
| MIRROR | Riêng Run: khiên nền rộng thêm 0,25. **Gương hộ thân**: mỗi bậc còn giúp phản một đòn chip 0,5 sát thương × hệ số `DamagePct` khi parry một MIRROR khác; đòn chip có thể hưởng hệ số của Tài năng A/B. **Dựng khiên** vẫn thêm 0,25. | Build mạnh v2 chỉ thắng MIMIC 47,8% số lượt; hai MIRROR parry nhau làm đầu tư vào width/reflect khó chuyển thành lợi thế. |
| HIVE | Kỹ năng OVERCLOCK nhánh A ở cấp 3 có 5 loạt bắn thay vì 4; cấp 2 giữ 3. | V2 nhánh DEPLOY B mạnh hơn ở nhiều build, nhưng thử 6 loạt khiến A vượt B tới 10 điểm. |

Các thay đổi trên chỉ tác động bóng người chơi trong Run của harness. Đối thủ và Versus giữ logic v2. Công thức MIRROR được kích hoạt khi đối thủ cũng là MIRROR, không gắn riêng vào tên boss MIMIC. Nó cần kiểm xem có làm matchup Versus lệch nếu sau này triển khai, và cần chuẩn hóa PvP trước khi đưa vào Sim chính.

## Trạng thái đo

Thử nhanh trên cùng seed 1301–1500, Greedy, 200 run/bóng/build:

- Build V2/P2/G2/START/SKILL A3/không Tài năng: WRECKER 71%, MIRROR 58,5%.
- Cùng root với SKILL B3: WRECKER 69,5%, MIRROR 61,5%.
- Cặp V1/P2/G2/không START/Tài năng A/B: SPARKLER 58,5/56%; SPORE 54/54,5%; HIVE 61,5/61,5%.

Đây là thử lựa chọn thông số, chưa phải gate. Sweep 18 build × tám bóng và xác nhận trên seed mới sẽ quyết định có giữ ứng viên này hay không.
