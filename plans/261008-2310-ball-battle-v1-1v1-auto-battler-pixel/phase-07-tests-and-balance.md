---
phase: 7
title: "Tests and balance"
status: pending
priority: P1
dependencies: [3, 6]
effort: "1 ngày"
---

# Phase 7: Tests and balance

## Overview
Chốt cân bằng bằng mô phỏng hàng loạt không hình, chạy toàn bộ test, build Windows dọc để chơi thử.

## Requirements
- Công cụ `SimTests/Balance/BalanceRunner` (chạy bằng `dotnet test --filter Balance` hoặc console): mỗi cặp 1000 trận, seed 1..1000, xuất bảng CSV + Markdown: tỉ lệ thắng, trung vị/P90 thời lượng, % hòa.
- Ngưỡng: cặp không gương 30–70%; trung vị 20–90 s; hòa < 5%; chạm trần 180 s < 1%.
- Chỉnh số liệu trong 1 file `Sim/Weapons/WeaponTuning.cs`; mọi lần chỉnh ghi vào report.
- Test hồi quy giữ ngưỡng (test fail nếu cân bằng trôi khỏi ngưỡng).
- Build Windows Standalone 1080x1920 cửa sổ dọc để playtest.
- Build APK Android development (ARM64, IL2CPP, portrait, key debug) để playtest trên điện thoại; đo fps trên máy thật. <!-- Updated: Validation Session 1 - thêm APK Android -->

## Related Code Files
- Create: `Editor/BuildScript.cs` (Windows + Android batch), `SimTests/Balance/BalanceRunner.cs`, `SimTests/Balance/BalanceThresholdTests.cs`, `plans/.../reports/balance-*.md`
- Modify: `Sim/Weapons/WeaponTuning.cs`

## Implementation Steps
1. BalanceRunner + report.
2. Vòng chỉnh số → chạy lại tới khi đạt ngưỡng.
3. Test ngưỡng.
4. Chạy toàn bộ SimTests + EditMode (Unity batch `-runTests`).
5. Build Windows + APK Android (batch qua Editor/BuildScript.cs), playtest 10 trận mỗi bản, ghi lỗi.
6. Spawn `code-reviewer` theo quy trình cook; cập nhật `docs/` (codebase-summary, system-architecture, game-design).

## Success Criteria
- [ ] Bảng cân bằng đạt mọi ngưỡng.
- [ ] 100% test xanh.
- [ ] Build Windows chạy được vòng chơi đầy đủ.
- [ ] APK cài được, chạy vòng chơi đầy đủ, ≥ 55 fps trên điện thoại thử.
- [ ] Review không còn vấn đề mức cao.

## Risk Assessment
- Không đạt ngưỡng bằng chỉnh số → báo user, đề xuất đổi cơ chế 1 vũ khí (không tự đổi thiết kế).
- 6 cặp × 1000 trận × tới 180 s chạy lâu → chạy song song theo cặp, trần thời gian.

## Tiến độ sớm (2026-10-09)
- Cân bằng lượt 1 đã làm trước theo yêu cầu: `reports/balance-261009-first-pass.md`. Mọi ngưỡng đạt (500 seed/bên).
- `BalanceRunner` = `SimTests/MatchupReport.cs` (song song) + `BalanceSweep.cs`; test ngưỡng `BalanceThresholdTests.cs` đã có trong bộ test thường.
- Còn lại ở Phase 7: chỉnh lại sau playtest, build Windows + APK, review cuối.
