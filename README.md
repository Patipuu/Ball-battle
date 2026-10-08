# Ball Battle

Auto-battler lấy cảm hứng từ trend "Weapon Ball Battles" (Earclacks, viral TikTok/Shorts cuối 2025):
các quả bóng mang vũ khí xoay quanh mình, nảy trong arena, mạnh lên mỗi lần đánh trúng; bóng cuối cùng còn máu thắng.
Luật chơi tự thiết kế lại; không dùng tên, art, âm thanh hay nhân vật của Earclacks.

## Trạng thái (2026-10-08)
- Khung project đã dựng: `.claude/` (ClaudeKit, copy từ Boom), Unity 6000.5.1f1 trống tại `BallBattleUnity/`.
- Chưa có plan được duyệt, chưa có code game.
- Hướng: 3 bản đầu là **auto-battler** (người chơi chỉ chọn trận rồi xem, không điều khiển).

## Đọc theo thứ tự
1. `CLAUDE.md` — quy trình làm việc (scout → chốt yêu cầu → plan → cook → test → review → finalize).
2. `docs/project-overview-pdr.md` — nghiên cứu luật chơi gốc + định hướng sản phẩm.
3. `plans/` — plan đang chạy.

## Sơ đồ
```
CLAUDE.md, README.md
.claude/             ClaudeKit: rules, agents, skills (copy từ boom-online-handoff-260922)
BallBattleUnity/     project Unity 6000.5.1f1
docs/                tài liệu sống của dự án
plans/               plan + plans/reports/
journals/            nhật ký kỹ thuật
```

## Kiến trúc dự kiến (theo mô hình Boom Unity đã chứng minh)
- `Assets/Scripts/Sim/` — logic thuần C#, không phụ thuộc UnityEngine, chạy theo tick cố định, có seed → trận tái lập được.
- `Assets/Scripts/View/` — hiển thị, âm thanh, hiệu ứng; chỉ đọc state từ Sim.
- `Assets/Scripts/Editor/` — dựng scene bằng batch.
- `SimTests/` — test dotnet chạy ngoài Unity.

## Công cụ
- Unity Editor: `D:\Unity\6000.5.1f1\Editor\Unity.exe`
- dotnet 10, git.
