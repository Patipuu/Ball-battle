# Game feel audit — 20/10/2026

## Phạm vi và cách đo

Đo bằng Sim thực của project, chưa có đánh giá cảm giác bằng người chơi. Probe tại `docs/claude-runs/gamefeel-probe` tạo trận đầu của Tutorial Run, chọn thẻ đầu tiên hợp lệ, cho bóng 0 dùng kỹ năng khi sẵn sàng từ giây thứ 5. Đối chứng không bấm kỹ năng. Mỗi kết quả theo seed cố định của tutorial là **một trận**, không phải tỷ lệ thắng thống kê. Ngoài ra chạy 20 seed biến thiên để kiểm tra xu hướng. Probe không chạm vào save của người chơi.

Lệnh: `dotnet run -c Release --project docs/claude-runs/gamefeel-probe/GameFeelProbe.csproj -- 1 tap 60 fixed` (đổi `tap` thành `no-tap` cho đối chứng).

## Vấn đề đã xác nhận

1. Trận hướng dẫn cũ gặp Pike. Với chính bóng Pike, seed tutorial cố định mất 2,1 giây mới gây sát thương, có quãng im 8,8 giây và kéo dài 49,4 giây khi tự động bấm kỹ năng sẵn sàng. Không bấm: 55,6 giây. Các bóng mở đầu khác cũng gặp quãng ít sự kiện.
2. Hướng dẫn đầu trận làm chậm mô phỏng còn 35% **không có giới hạn thời gian** nếu người mới chưa bấm kỹ năng. Người không nhận ra lời nhắc có thể phải chờ quá lâu. Đây là lỗi trải nghiệm, độc lập với cân bằng sát thương.

## Thay đổi và kết quả đo lại

- Đối thủ trận hướng dẫn là Wrecker (60 HP, sát thương giảm 25% như cũ), chỉ ở trận 1 của Tutorial Run. Wrecker tiến lại gần và tạo va chạm liên tục; các trận sau không đổi.
- Đoạn chậm để nhấn mạnh kỹ năng đầu tiên dừng sau 2,5 giây thời gian thực. Lời nhắc còn hiện cho đến khi người chơi bấm.

| Bóng khởi đầu | Trước: chạm đầu / hết trận | Sau: chạm đầu / hết trận | Quãng im dài nhất trước → sau |
|---|---:|---:|---:|
| Ember | 1,0 / 22,3 giây | 1,0 / 25,5 giây | 3,6 → 2,9 giây |
| Mantis | 5,0 / 37,1 giây | 1,0 / 24,6 giây | 4,2 → 2,5 giây |
| Angler | 2,1 / 49,4 giây | 1,0 / 29,3 giây | 8,8 → 2,9 giây |
| Wrecker | 1,0 / 25,2 giây | 1,0 / 21,0 giây | 2,8 → 2,2 giây |

Với 20 seed biến thiên sau đổi đối thủ, trung vị trận đầu khi bấm kỹ năng là 21–30 giây cho bốn bóng mở đầu, và cả 80/80 trận đều thắng. Không bấm kỹ năng, trung vị 29–38 giây, win rate 90–100%. Điều này giữ trận đầu dễ thắng nhưng bấm kỹ năng thường giúp kết thúc sớm hơn. Tỷ lệ này chỉ là proxy tự động, không đo niềm vui hay khả năng giữ chân.

## Kiểm tra cần làm trong Unity và với người mới

Unity EditMode đã được yêu cầu chạy test `BbTut2Tests.FirstSkillPromptStopsSlowingTheFightWhenThePlayerDoesNotTap`, nhưng Unity Licensing Client mất kết nối trong batch mode; chưa có kết quả Editor/visual/audio ở lần audit này. Cần mở Editor khi license hoạt động, chạy test và chơi trực tiếp bản mới trước khi build phát hành.

`dotnet build BallBattleUnity/SimTests/SimTests.csproj -c Release --no-restore` đạt, 0 warning và 0 error. Test `FirstRunOnASaveGetsShellKingAndMimic` đạt. Probe sau chỉnh sửa biên dịch toàn bộ Sim và kiểm tra lại seed tutorial cố định; bốn bóng mở đầu đều qua ngưỡng chạm đầu ≤2 giây, trận ≤40 giây và thắng. Lượt `dotnet test` toàn bộ bị VSTest mất kết nối với testhost; lượt chạy ngoài sandbox cũng không hoàn tất, nên chưa được tính là một suite pass.

Cho 5 người chưa biết game chơi bản mới mà không giải thích. Ghi thời gian đến đòn đầu, lần bấm kỹ năng đầu, có hiểu vì sao thắng/thua, và họ có tự chọn trận tiếp theo sau kết quả không. Hỏi sau 10 phút: bóng nào muốn chơi tiếp và vì sao; chọn một bản cũ làm đối chứng nếu có thể. Mẫu 5 người chỉ phát hiện ma sát rõ ràng, không đủ chứng minh retention.
