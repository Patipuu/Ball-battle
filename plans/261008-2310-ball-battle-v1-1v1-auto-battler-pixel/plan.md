---
title: Ball Battle v1 - 1v1 auto-battler pixel
description: >-
  Bản 1 chơi được: 2 bóng mang vũ khí tự đấu 1v1 trong arena pixel 270x480, 4 vũ
  khí khắc chế nhau, best-of-3, trận tái lập từ seed.
status: in-progress
priority: P1
branch: master
tags:
  - feature
  - gameplay
  - unity
  - pixel-art
blockedBy: []
blocks: []
created: '2026-10-08T16:04:44.898Z'
createdBy: 'ck:plan'
source: skill
---

# Ball Battle v1 - 1v1 auto-battler pixel

## Overview
Auto-battler kiểu Weapon Ball Battles. Người chơi chọn 2 bóng (hoặc ngẫu nhiên) → xem trận tự diễn ra → best-of-3 → kết quả, đấu lại.
Bối cảnh + nghiên cứu luật gốc: `docs/project-overview-pdr.md`.

## Yêu cầu đã chốt (2026-10-08)
| Mục | Giá trị |
|---|---|
| Engine | Unity 6000.5.1f1, project `BallBattleUnity/` |
| Nền tảng | mobile dọc 9:16 (Editor + Windows build dọc + APK Android để playtest) |
| Đồ họa | pixel art, gốc 270x480, phóng nguyên x4 → 1080x1920; render thấp rồi phóng (xoay mượt, pixel thẳng lưới) |
| Art | placeholder pixel sinh bằng code (Editor tool), thay art thật sau không sửa code |
| Nội dung | 4 vũ khí, chế độ 1v1 best-of-3, không input trong trận |

## Tiêu chí nghiệm thu (toàn plan)
1. Editor Play: Menu → chọn 2/4 vũ khí hoặc Random → trận tự chạy tới khi có người thắng → best-of-3 hiện tỉ số → màn kết quả có Đấu lại / Về menu.
2. Cùng seed + cùng cặp vũ khí → kết quả và hash state cuối giống hệt (test dotnet).
3. Mỗi cặp không gương: tỉ lệ thắng 30–70% trên 1000 trận mô phỏng; trung vị thời lượng 20–90 s. Sau 90 s arena co dần tới kích thước tối thiểu; trần an toàn 180 s (bên nhiều % HP hơn thắng, bằng nhau → hòa, đánh lại ván). Mục tiêu: < 1% trận chạm trần.
4. Không mờ subpixel: toàn cảnh render 270x480, point filter.
5. 0 lỗi compile; toàn bộ SimTests + EditMode tests xanh.

## Ngoài phạm vi
Input người chơi, chế độ khác (BR, team, raid), art/âm thanh thật, multiplayer, kiếm tiền, đồng nhất kết quả giữa các nền tảng CPU khác nhau (v1 dùng float, đổi fixed-point khi làm online), phát hành store (AAB ký, listing).

## Kiến trúc (theo mô hình Boom Unity)
- `Assets/Scripts/Sim/` asmdef `BallBattle.Sim`, `noEngineReferences: true` — C# thuần, tick cố định 60 Hz, RNG riêng có seed, xuất danh sách sự kiện mỗi tick.
- `Assets/Scripts/View/` asmdef `BallBattle.View` — đọc snapshot + sự kiện, nội suy, vẽ, âm thanh. Không ghi ngược vào Sim.
- `Assets/Scripts/Editor/` — sinh sprite/âm placeholder, dựng scene batch.
- `SimTests/` — dự án dotnet link nguồn Sim, chạy ngoài Unity.

## Phases

| Phase | Name | Status |
|-------|------|--------|
| 1 | [Project setup](./phase-01-project-setup.md) | Completed |
| 2 | [Deterministic sim core](./phase-02-deterministic-sim-core.md) | Completed |
| 3 | [Four weapons](./phase-03-four-weapons.md) | Completed |
| 4 | [Pixel view](./phase-04-pixel-view.md) | Completed |
| 5 | [Juice and audio](./phase-05-juice-and-audio.md) | In Progress |
| 6 | [Match flow UI](./phase-06-match-flow-ui.md) | Pending |
| 7 | [Tests and balance](./phase-07-tests-and-balance.md) | Pending |

Thứ tự: 1 → 2 → 3 → (4, 5 song song được) → 6 → 7. Test Sim viết dần từ phase 2, phase 7 chốt cân bằng.

## Dependencies
Không có plan khác. Công cụ: Unity 6000.5.1f1 (`D:\Unity\6000.5.1f1`), dotnet 10, Unity MCP (`com.coplaydev.unity-mcp`) để agent điều khiển Editor.

## Câu hỏi còn mở
- Tên 4 vũ khí (đang dùng tên tạm: Blade, Fang, Pike, Brawler).

## Validation Log

### Session 1 — 2026-10-08
**Trigger:** /ck:plan validate sau khi tạo plan
**Questions asked:** 4

#### Verification Results
- Claims checked: 9 (package versions URP 17.5.0 / pixel-perfect 6.0.0 / test-framework 1.7.0 / ugui 2.5.0 khớp Boom manifest; editor D:\Unity\6000.5.1f1 có; module Android + Windows có; nuget.org truy cập được; ck CLI + set-active-plan chạy được; Boom SimTests dùng NUnit)
- Verified: 8 | Failed: 1 | Unverified: 0
- Tier: Full (7 phase; project greenfield nên chủ yếu kiểm môi trường)
- Failures: phase-01 ghi xUnit, Boom SimTests.csproj dùng NUnit 4.3.2 → đã hỏi user, sửa sang NUnit

#### Questions & Answers
1. **[Verification]** Phase 1 ghi SimTests dùng xUnit, nhưng Boom dùng NUnit. Dùng cái nào?
   - Options: NUnit như Boom | xUnit
   - **Answer:** NUnit như Boom
   - **Rationale:** cùng framework với Unity Test Framework, copy được csproj đã chạy.
2. **[Assumption]** Luật hết giờ 150 s so % HP: giữ hay đổi?
   - Options: Arena co dần sau 90 s | Giữ 150 s so % HP | Sát thương tăng dần sau 90 s
   - **Answer:** Arena co dần sau 90 s
   - **Rationale:** trận luôn kết thúc bằng hạ gục, hợp quay clip; cần logic tường động trong Sim.
3. **[Scope]** Phase 7 có build APK Android không?
   - Options: Có, build cả APK | Chỉ Windows
   - **Answer:** Có, build cả APK
   - **Rationale:** nền tảng đích mobile dọc; kiểm hiệu năng + cảm giác pixel trên máy thật sớm.
4. **[Architecture]** Tái lập seed cùng máy hay mọi nền tảng?
   - Options: Cùng máy là đủ cho v1 | Giống mọi nền tảng ngay (fixed-point)
   - **Answer:** Cùng máy là đủ cho v1
   - **Rationale:** giữ float, đơn giản; fixed-point khi làm online.

#### Confirmed Decisions
- Test lib: NUnit — theo Boom.
- Kết thúc trận: arena co từ 90 s tới tối thiểu trong 30 s, trần an toàn 180 s.
- Build: Windows dọc + APK Android dev.
- Determinism: cùng build/máy, float.

#### Impact on Phases
- Phase 1: xUnit → NUnit.
- Phase 2: thêm tường co, trần 180 s, tiêu chí không xuyên tường khi co.
- Phase 4: vẽ tường co + cảnh báo.
- Phase 7: ngưỡng chạm trần < 1%, BuildScript Windows + Android, tiêu chí APK ≥ 55 fps.

### Whole-Plan Consistency Sweep
- Đã đọc lại plan.md + 7 phase; tìm 'xUnit', '150 s', 'Chỉ Windows': chỉ còn trong log/marker.
- Phase 1 đặt target Android → khớp build APK ở phase 7. Test hash 9000 tick (phase 2) vẫn hợp lệ (< trần 180 s).
- Mâu thuẫn chưa giải quyết: 0. Đủ điều kiện sang /ck:cook.
