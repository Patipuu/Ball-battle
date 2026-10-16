# Ball Battle — kế hoạch core loop, audio và phân công Grok

Ngày: 2026-10-13. Trạng thái: đề xuất để PM chuyển thành task thực thi.
Phạm vi: hoạch định; chưa thay đổi code, chạy game, chạy lại test, commit hoặc push.

## Căn cứ từ repo

- HEAD khi khảo sát: `29fa2c3`. Working tree có thay đổi fixed-point 6a.6a và các file ngoài lề; phải bảo toàn. HEAD chưa đại diện đầy đủ cho code đang có trên máy.
- `RunTuning.cs`: Run 8 trận, boss ở trận 4/8, một lượt retry với 60% HP.
- `RunState.cs`, `RunFlow.cs`: chọn vũ khí/thẻ, reroll, kết quả, lưu run, retry đã có.
- `UnlockRules.cs`: có Achievements, RunsPlayed, RunsWon, BestFight và mở khóa vũ khí/trait. Tiến trình có nền tảng; cần hiển thị mục tiêu kế tiếp rõ hơn.
- `MenuView.cs`: menu chính tập trung chọn hai bên Versus; nút Run ở phía dưới. Đây là giả thuyết gây lạc hướng, cần playtest.
- `RunTitleView.cs:25`: còn ghi `8 FIGHTS  3 BOSSES  3 LIVES`; `RunSummaryView.cs:46`: còn `BOSSES .../3`. Hai chuỗi mâu thuẫn với luật hiện tại.
- `SfxLibrary.cs`: Hit, Parry, Wall, Death, Win. Assets/Audio có năm WAV generated; chưa thấy bộ nhạc hoặc điều khiển âm lượng nhạc trong source đã khảo sát.
- `RunController.BossArtEnabled=false`; EnemyGenerator chỉ sinh vũ khí/trait và chỉ số boss. Art Shell King/Mimic đã có, nhưng chưa thấy luật boss riêng tương ứng trong Sim; ANTIDOTE hiện là mẫu CardFace.
- `docs/claude-runs/6a6.md`: báo cáo nền tảng Fix đã qua test, chưa chuyển MatchSim/Vec2 sang fixed-point. XML đi kèm ghi 189 pass, 0 fail, 1 skip. Đây là kết quả lưu trước đó, không phải lần chạy mới.
- README, system-architecture và plan Step 3 chứa mô tả cũ. Spec đã chốt trong tài liệu không đồng nghĩa tính năng đã có trong build.

## Mục tiêu bản chơi gần nhất

Người mới biết chọn Run, hiểu cần vượt 8 trận và 2 boss, đọc được ảnh hưởng lựa chọn thẻ, thấy mục tiêu mở khóa kế tiếp, hiểu retry và có nhạc/âm thanh xuyên suốt phiên chơi.

Tận dụng Run hiện có. Tuyến 5 thử thách từng được đề xuất trước khi đọc repo không phải phạm vi của kế hoạch này.

## Thứ tự công việc

| Task | Ưu tiên / độ lớn tương đối | Chủ trì | Phạm vi | Phụ thuộc / nghiệm thu |
|---|---|---|---|---|
| BB-00 Chốt baseline và spec hiện hành | P0 / nhỏ | PM + Reviewer | Ghi HEAD, working-tree thay đổi, trạng thái 6a.6a; bảng đã có/đã chốt/chưa làm; sửa mô tả tài liệu đang gây nhầm | Trước mọi task code. Bản QA có build ID, revision và mô tả patch |
| BB-01 Làm rõ đường vào Run và luật | P0 / nhỏ-vừa | Designer + Art; agent code triển khai | Menu, RunTitle, Summary; cách giới thiệu Run, 8 trận/2 boss/1 retry; hướng dẫn ngắn ở điểm ra quyết định | Sau BB-00. Chuỗi UI lấy từ luật chung, không hardcode luật cũ; nút/khung chữ không tràn 270x480 |
| BB-02 Hiện mục tiêu mở khóa kế tiếp | P0 / vừa | Designer; agent code triển khai | Đọc ProgressData/UnlockRules; hiện điều kiện, phần thưởng, tiến độ; Summary nêu thay đổi và mục tiêu kế tiếp | Có spec BB-01. Vượt boss đầu, thắng Run đầu, đạt 3 lần thắng được phản ánh đúng; đóng/mở giữ tiến độ; đầy đủ trạng thái đã mở hết |
| BB-03 Nhạc và phản hồi audio | P0 / vừa | SFX; agent code tích hợp | Nhạc menu/prepare, battle, boss nếu cần; cue thắng/thua/UI/chọn thẻ/reroll/retry/unlock; âm lượng Music/SFX và lưu lựa chọn | Asset/spec làm song song BB-01/02; tích hợp sau chốt sự kiện. Không phát chồng nhạc, chuyển màn hợp lý, x2 không tự tăng pitch nhạc, không đổi kết quả Sim |
| BB-04 Thử thách có lời giải rõ | P1 / vừa-lớn | Designer + Reviewer | Đề xuất Shell King trước, Mimic sau; cơ chế, counter, preview, tín hiệu; đo độ khó | Sau baseline Sim ổn định và playtest BB-01/02. Không cùng sửa Sim với đợt chuyển fixed-point; boss có hành vi thật trước khi bật art hứa hẹn cơ chế |
| BB-05 QA và playtest bản tích hợp | P0 / vừa | Tester + Playtest Operator | Đúng luật, save/resume/retry/unlock/audio; quan sát người mới | Sau BB-01/02/03. Lỗi có revision, seed, lựa chọn, bước tái hiện; báo cáo playtest tách khỏi kết quả test tự động |

## Ba luồng làm việc

1. Sản phẩm: BB-00 → BB-01 → BB-02 → BB-05.
2. Audio: SFX làm asset/spec song song → một task tích hợp BB-03 → BB-05.
3. Kỹ thuật: Reviewer kiểm tra diff 6a.6a và xác nhận tình trạng. PM chọn mục tiêu cần determinism giữa runtime trước khi mở đợt migration tiếp. Nội dung Sim BB-04 chờ nhánh nền tảng ổn định.

Designer không giao thêm hệ XP, currency, daily leaderboard hoặc progression mới trước khi đo hiệu quả của unlock hiện có. Đây là giới hạn đề xuất cho bản chơi gần nhất, không phủ nhận roadmap dài hạn.

## Quy trình tiết kiệm token

- PM duy trì một task/spec chính thức có version; thông điệp chat chỉ dẫn tới bản này.
- Mỗi task có người chốt, agent code thực thi, reviewer độc lập, files được sửa và files dùng làm context.
- Mỗi file/module có một người ghi tại một thời điểm. Hai task cùng chạm GameController/RunController phải tích hợp tuần tự.
- Trước code, chốt luật, save contract và event audio/UI. Reviewer làm một kiểm tra khả thi ngắn cho thay đổi lớn.
- Agent code đọc context giới hạn theo task, tự kiểm tra và bàn giao diff + kết quả kiểm tra; không gửi toàn bộ repo/lịch sử chat.
- Reviewer tập trung correctness, save, determinism, Unity compatibility và acceptance. Nhận xét phải có vị trí và tác động cụ thể; góp ý nhỏ gom backlog.
- Một lượt review chính, một lượt xác minh sửa. Nếu còn lỗi chặn, tiếp tục xử lý; không giới hạn vòng bằng cách bỏ qua lỗi. Lặp do đổi yêu cầu thì quay về sửa spec.
- Chạy test hẹp khi phát triển; trước nghiệm thu code hoàn tất gate dự án: SimTests, Unity compile và EditMode. Chỉ chạy lại gate đã pass khi source/baseline đổi hoặc có rủi ro cụ thể; audio cần nghe thực tế.
- Không nới threshold/golden chỉ để test xanh. Đổi luật và baseline phải có quyết định riêng cùng lý do.
- PM cập nhật một báo cáo tích hợp; không yêu cầu mọi bot viết lại cùng nội dung.

## Nghiệm thu trải nghiệm (đề xuất thăm dò)

Playtest Operator dùng 5 người mới trên cùng build. Ghi hành vi trước khi giải thích:

- Có tìm được Run và nói đúng mục tiêu không?
- Có hiểu thẻ thay đổi build và vì sao chọn thẻ đó không?
- Có nói đúng hậu quả thất bại/retry không?
- Có nhận biết phần thưởng và mục tiêu tiếp theo không?
- Có muốn bắt đầu lượt nữa; lý do tiếp tục/thoát là gì?

Mục tiêu ban đầu: ít nhất 4/5 người hiểu mục tiêu và bước tiếp theo. Mẫu này dùng phát hiện vấn đề, không chứng minh retention hoặc cân bằng.

## Tài liệu thực thi

- [Brief cho từng bot và mẫu task](./team-briefs.md).
- Context đọc có chọn lọc: `docs/code-standards.md`, `docs/design/boss-and-loss-rules.md`, `docs/design/bot-sweep-v4.md`, `docs/claude-runs/6b.md`, `docs/claude-runs/6a6.md`.

## Quyết định còn cần PM chốt khi mở task

- Menu vào Run mặc định hay hub hai chế độ; lựa chọn phải thống nhất với mockup.
- Giữ nguyên unlock milestones trước, hay thay luật unlock; mặc định đề xuất giữ nguyên để đo UI.
- Có mục tiêu phát hành cần replay chung giữa runtime ngay không; ảnh hưởng thứ tự migration fixed-point.
- Boss/content đang có spec nhưng chưa triển khai: xác nhận phần quyết định còn hiệu lực trước khi giao code.
