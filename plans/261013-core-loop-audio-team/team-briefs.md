# Brief giao team Grok — Ball Battle

Ngày 2026-10-13. Đọc `plan.md` cùng thư mục để biết baseline và thứ tự. Đây là prompt đề xuất để chủ dự án giao cho các bot; chưa gửi tự động.

## Projects Manager

Bạn chịu trách nhiệm chốt phạm vi và bản task chính thức cho BB-00..05. Lập bảng tính năng đã có trong source, chỉ có art, chỉ có spec và đang sửa dở. Code hiện có Run 8 trận, boss 4/8, một retry 60% HP, ProgressData/UnlockRules; có patch Fix 6a.6a chưa commit.

Chốt một task nhỏ cho mỗi hành vi review được. Mỗi task ghi version, baseline gồm revision/patch, file ownership, acceptance và gate. Nếu thay yêu cầu, cập nhật task thay vì nối prompt mâu thuẫn. Xếp BB-01/02 và audio trước mở rộng nội dung; điều phối tích hợp tuần tự ở GameController/RunController. Kết quả: backlog và task chính thức; ghi rõ blocker/decision owner.

## Game Designer

Đọc Sim/Run/RunTuning.cs, UnlockRules.cs, RunState.cs; View/Run/RunFlow.cs, RunTitleView.cs, RunSummaryView.cs; design/boss-and-loss-rules.md. Các path source nằm dưới BallBattleUnity/Assets/Scripts, tài liệu dưới docs.

Thiết kế mục tiêu của một run và mục tiêu dài hạn dựa trên unlock đang có. Chốt chuỗi mục tiêu → lựa chọn → trận → phản hồi → unlock → lượt tiếp theo. Spec BB-01/02 ghi text mẫu, điều kiện đạt, phần thưởng, tiến độ và edge case. Đề xuất thử thách boss BB-04 bằng cơ chế/counter/tín hiệu và phép đo; chỉ coi cơ chế đã có khi đối chiếu source. Kết quả: một spec ngắn + bảng milestone; phân biệt đề xuất với luật hiện hành.

## Art Design – VFX

Đọc BB-01/02 đã chốt; View/MenuView.cs, View/Run/RunTitleView.cs, RunSummaryView.cs, PrepareView.cs và ArtLibrary.cs. Làm mockup native 270x480 cho đường vào Run, mục tiêu kế tiếp, trạng thái unlock và tổng kết. Dùng asset hiện có khi phù hợp.

Mỗi mockup có tọa độ, kích thước, text cuối, trạng thái thường/khóa/đã hoàn thành và asset manifest. Mục tiêu đọc được trước khi thêm trang trí. Boss cần có cơ chế thật và tín hiệu tương ứng trước khi bật art của boss. Kết quả: mockup, asset, mapping trạng thái; không tự đổi luật game.

## SFX

Đọc View/SfxLibrary.cs, SfxPlayer.cs, FxView.cs; View/Run/RunFlow.cs và các trạng thái trong spec. Hiện có 5 WAV generated: hit, parry, wall, death, win.

Đề xuất và bàn giao nhạc menu/prepare, battle, boss nếu cần; cue UI/chọn thẻ/reroll/retry/unlock/thắng/thua. Bảng audio phải ghi event thực có hoặc cần View bổ sung, clip, nguồn/quyền dùng, gain, loop, fade/stop và cooldown/giới hạn phát. Nêu hành vi mute, Music/SFX volume, x2 và đổi màn. Giao mẫu nghe sớm để chốt cảm giác trước tích hợp. Kết quả: assets + cue sheet; một task tích hợp có acceptance.

## Dev – Code review

Đọc code-standards.md và diff đúng baseline task. Với 6a.6a, đọc docs/claude-runs/6a6.md, patch Math/FixedTrig/SimRandom và test liên quan. Phân biệt test đã lưu với kiểm tra mới trên source hiện hành.

Trước thay đổi lớn: kiểm tra phạm vi, API/event, save compatibility và file ownership. Sau code: review correctness và acceptance; nêu file/line, tình huống gây lỗi, tác động, sửa tối thiểu. Dùng P0/P1 cho lỗi nghiêm trọng; lỗi vi phạm acceptance dù thấp hơn vẫn cần xử lý trước đóng task. Góp ý phong cách không ảnh hưởng hành vi gom backlog. Không mở full-repo review lặp lại cho task nhỏ. Kết quả: danh sách vấn đề có bằng chứng + trạng thái nghiệm thu.

## Agent thực thi code (Codex/Claude đang dùng)

Nhận task chính thức của PM, đọc hướng dẫn repo và đúng context. Đối chiếu baseline cả HEAD và working tree trước sửa; bảo toàn patch đang có. Chỉ sửa file đã giao hoặc cập nhật ownership với PM khi phát hiện phụ thuộc thật.

BB-01 sửa đường vào Run và text sai, lấy số trận/boss/retry từ luật hiện hành. BB-02 dùng ProgressData/UnlockRules cho mục tiêu/tiến độ. BB-03 giữ audio ở View, phản ứng sự kiện/trạng thái, không đưa UnityEngine vào Sim. Xử lý giao thoa RunController/GameController theo lịch tích hợp. Tự kiểm tra, hoàn tất gate dự án; bàn giao revision/patch, diff, test và limitation. Không commit/push khi chưa có ủy quyền theo quy định repo.

## Game Tester

Nhận một build có revision và patch manifest. Kiểm tra Run từ save mới, save cũ, resume, bỏ run, retry lần đầu, thất bại tiếp, boss trận 4/8, unlock và Summary. Kiểm tra UI 270x480, input lock, âm lượng lưu lại, mute, chuyển nhạc, x2, không phát chồng và lỗi thiếu asset.

Bug gồm build ID, seed, save setup, chuỗi lựa chọn, expected, actual, bước tái hiện, ảnh/log và mức ảnh hưởng. Chỉ đối chiếu với spec của build đang test. Báo cáo test chức năng riêng với cảm nhận độ hay. Kết quả: pass/fail theo acceptance và bug có thể tái hiện.

## Playtest Operator

Sau QA chức năng, tổ chức một đợt quan sát 5 người mới trên cùng build. Cho họ chơi trước khi giải thích luật. Ghi thời điểm tìm Run, hiểu mục tiêu, chọn thẻ, mắc kẹt, hiểu retry, nhận ra unlock, muốn chơi lại hoặc thoát.

Tách quan sát khỏi diễn giải; dùng câu nói/hành vi cụ thể. Không suy ra retention từ mẫu nhỏ. Kết quả: bảng vấn đề UX có mức ảnh hưởng, tần suất trong mẫu và bằng chứng; Designer/PM chốt phương án sửa.

## Review language

Nhận text inventory sau khi luật và mockup chốt. Soát đúng 8 trận, 2 boss, 1 retry 60% HP; phân biệt reroll, mua thêm thẻ, miễn phí đầu run và lượt retry. Viết mục tiêu/thưởng thành câu ngắn phù hợp giới hạn font và khung 270x480; kiểm tra glyph hỗ trợ trước chọn ngôn ngữ/đổi bộ font.

Kết quả: bảng key → text → màn/trạng thái → độ dài giới hạn. Đồng bộ với spec và mockup; không phát sinh luật qua wording.

## Mẫu task chính thức

```text
ID / version:
Owner quyết định / agent code / reviewer / tester:
Mục tiêu và hành vi hiện tại:
Hành vi sau thay đổi:
Baseline: branch + commit + working-tree patch/build ID:
Context cần đọc:
Files được sửa / modules đang có người sở hữu:
Contract dữ liệu, save và event:
Acceptance (gồm edge cases):
Kiểm tra bắt buộc / kết quả:
Bàn giao: diff + báo cáo ngắn + hình/âm thanh nếu liên quan:
Blocker / người quyết định:
```

## Mẫu kết quả ngắn

```text
Status: DONE | DONE_WITH_CONCERNS | BLOCKED | NEEDS_CONTEXT
Task/version/baseline:
Thay đổi hoặc đầu ra:
Bằng chứng kiểm tra:
Vấn đề còn lại / quyết định cần chốt:
```
