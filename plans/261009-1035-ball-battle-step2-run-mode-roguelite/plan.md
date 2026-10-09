---
title: "Ball Battle Step 2 - Run mode roguelite"
description: "Chế độ Run roguelite: chuỗi 8 trận/3 màn có boss, xem trước đối thủ, chọn 1/3 thẻ (trait, chỉ số, vũ khí), kinh tế xu, 10 trait, 4 vũ khí mới, 6 arena biến thể, trong trận vẫn auto thuần."
status: in-progress
priority: P1
branch: "master"
tags: [feature, gameplay, roguelite, unity, pixel-art]
blockedBy: []
blocks: []
created: "2026-10-09"
createdBy: "ck:plan"
source: skill
---

# Ball Battle Step 2 - Run mode roguelite

## Overview
Thêm chiều sâu chiến thuật cho auto-battler bằng quyết định trước/giữa trận. Bối cảnh + lựa chọn: `plans/reports/brainstorm-261009-1030-step2-run-mode-tactical-depth-report.md`.
Nền: Step 1 (`plans/261008-2310-ball-battle-v1-1v1-auto-battler-pixel/`) — sim tái lập 60 Hz, 4 vũ khí cân bằng, view pixel 270x480, FX/âm thanh, menu + best-of-3.

## Quyết định thiết kế (Claude tự quyết, user hiệu chỉnh sau)
| Mục | Giá trị |
|---|---|
| Khung | Run: 8 trận / 3 màn, boss trận 3/6/8, 3 mạng; mỗi trận 1 ván |
| Trước trận | Xem trước đối thủ (vũ khí, trait, arena) → chọn miễn phí 1/3 thẻ: trait / chỉ số / đổi vũ khí |
| Trait | Tối đa 3/bóng; chọn lại = cấp 2 (tối đa 2) |
| Xu | Bắt đầu 2; thắng +3, thua +1; đổi bộ thẻ 1; mua thêm thẻ 3; không mang qua run |
| Độ khó | Chủ yếu thêm trait (0–1 → 1–2 → 2–3, có cấp 2), HP +0/+10/+20%; boss = **bóng khổng lồ** (r=24) 250/350/500 HP + 1/2/3 trait |
| HP người chơi | **Mang qua các trận**; thắng hồi 20% HP tối đa; thẻ hồi máu (Hồi 50%, +15 HP tối đa); thua trận = mất 1 mạng và hồi đầy HP |
| Mở khóa | Bắt đầu 4 vũ khí cũ + 5 trait; còn lại mở theo thành tích (hạ boss, thắng run) |
| Versus (Step 1) | Thêm chọn arena + đủ 8 vũ khí; không trait |
| Trong trận | Auto thuần, xem nhanh ×2 |
| Lưu | JSON ở persistentDataPath: mở khóa, kỷ lục, cài đặt, run đang dở |
| Tái lập | Seed run + cùng lựa chọn → cùng đối thủ/thẻ/kết quả |

## Tiêu chí nghiệm thu (toàn plan)
1. Chơi trọn 1 run: chọn vũ khí → 8 trận (3 boss khổng lồ) → thắng/thua run → màn tổng kết → mở khóa; HP mang qua trận đúng luật (thắng +20%, thua = mất mạng + hồi đầy); tiếp tục run dở sau khi thoát game.
2. Cùng seed run + cùng chuỗi lựa chọn → cùng đối thủ, thẻ, hash từng trận (test dotnet).
3. 8 vũ khí: mọi cặp không gương 30–70% (500 seed/bên); mỗi trait: chênh lệch tỉ lệ thắng trên toàn sân (trait vs không trait) trong +3..+15 điểm, không trait nào > +15 (OP) hay < +3 (vô dụng); mỗi arena: không vũ khí nào < 25% hoặc > 75% trung bình trên arena đó.
4. Độ khó: bot chơi ngẫu nhiên thắng run 5–20%; bot "tham lam" (chọn thẻ khắc chế theo heuristic) thắng 30–60% (mô phỏng 2.000 run).
5. Trận trung vị 25–60 s; không GC mỗi frame trong trận; pixel-perfect giữ nguyên (0 khối lệch ở 1080x1920).
6. Toàn bộ SimTests + EditMode xanh; 1v1 Versus của Step 1 vẫn chơi được.

## Ngoài phạm vi
Đội 2v2/3v3 (Step 3), cloud save, tài khoản, kiếm tiền/quảng cáo, PvP online, art/âm thanh thật, build store.

## Phases

| Phase | Name | Status |
|-------|------|--------|
| 1 | [Sim foundation for traits projectiles arenas](./phase-01-sim-foundation-for-traits-projectiles-arenas.md) | Completed |
| 2 | [Run core and vertical slice](./phase-02-run-core-and-vertical-slice.md) | Completed |
| 3 | [Ten traits](./phase-03-ten-traits.md) | Completed |
| 4 | [Four new weapons](./phase-04-four-new-weapons.md) | Completed |
| 5 | [Arena variants](./phase-05-arena-variants.md) | Completed |
| 6 | [Visual language and run UI](./phase-06-visual-language-and-run-ui.md) | Pending |
| 7 | [Difficulty balance and playtest](./phase-07-difficulty-balance-and-playtest.md) | Pending |

Thứ tự: 1 → 2 (lát cắt dọc chơi được) → 3, 4, 5 (song song được về nội dung, nhưng đều động vào balance → làm tuần tự để số liệu sạch) → 6 → 7.

## Dependencies
Không chặn plan khác. Dựa trên kiến trúc Step 1 (`docs/system-architecture.md`, `docs/code-standards.md`). Mọi thay đổi luật → tăng `SimVersion.Rules`.

## Câu hỏi còn mở
- Tên/ý tưởng hình ảnh 4 vũ khí mới (đang dùng tên tạm: Volley, Venom, Aegis, Rig).

## Validation Log

### Session 1 — 2026-10-09
**Kiểm chứng giả định (9 kiểm, 7 đúng, 2 sai):**
- ❌ "Boss bản Super" — code chưa có biến thể Super nào → thay bằng boss khổng lồ (tham số hóa bán kính/HP qua `BallLoadout`).
- ❌ Arena Wide 250x300 — xung đột HUD cố định mép ±115 (`HudView`) và điểm overlay y=128 → Wide đổi thành 260x230, mọi arena cao ≤ 230.
- ✅ Sim 60 Hz tái lập + hash; `WeaponRule` mở rộng được; `Series` tách được; HP đơn giản trong `BallState`; công cụ balance chạy song song; HUD/overlay cache theo giá trị; `MatchFlow` có state machine để mở rộng.

**Câu hỏi & trả lời:**
| # | Câu hỏi | Chốt | Lý do |
|---|---|---|---|
| 1 | Boss trông thế nào khi không có bản Super? | Bóng khổng lồ r=24, 250/350/500 HP, 1/2/3 trait | Dễ đọc trên 270 px, tái dùng sim |
| 2 | Arena Wide vs HUD cố định? | Cao ≤ 230; Wide = 260x230 | Không phải làm lại HUD |
| 3 | HP giữa các trận? | Mang qua trận | Tạo căng thẳng run, thẻ hồi máu có giá trị |
| 4 | Mỗi trận mấy round? | 1 round | Run ≈ 8 trận vẫn ngắn |
| 5 | Thua trận thì sao? | Mất 1 mạng + hồi đầy HP | Tránh vòng xoáy chết |
| 6 | Hồi máu? | Thắng +20% MaxHp; thẻ Hồi 50% / +15 MaxHp | Lựa chọn hồi vs mạnh lên |
| 7 | Mở khóa? | Theo thành tích; 4 vũ khí mới khóa trong Run | Mục tiêu dài hạn |
| 8 | Versus của Step 1? | Chọn arena + đủ 8 vũ khí | Sân thử nghiệm tổ hợp |

**Tác động lên phase:** P1 (`BallLoadout` có MaxHp/HP hiện tại/bán kính), P2 (luật HP, boss khổng lồ, thẻ hồi máu, mở khóa, lưu HP), P4 (khóa trong Run, mở trong Versus), P5 (Wide 260x230, test cao ≤ 230), P6 (UI Versus arena + 8 vũ khí, hiển thị HP, sprite boss 48x48, icon khóa), P7 (tinh chỉnh hồi máu, theo dõi vòng xoáy chết).

**Quét nhất quán toàn plan:** không còn "Super" / "250x300" trong plan và phase (báo cáo brainstorm giữ nguyên vì là lịch sử); "best-of-3" chỉ còn ở dòng mô tả nền Step 1 (đúng); thống nhất "6 arena biến thể + Classic = 7 cấu hình"; phase-07 "boss cuối" → "boss khổng lồ màn 3".