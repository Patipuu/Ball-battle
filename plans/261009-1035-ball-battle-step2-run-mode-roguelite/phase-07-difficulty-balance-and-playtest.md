---
phase: 7
title: "Difficulty balance and playtest"
status: pending
priority: P1
dependencies: [6]
effort: "1.5 ngày"
---

# Phase 7: Difficulty balance and playtest

## Overview
Chỉnh độ khó Run bằng bot mô phỏng hàng nghìn run, rồi playtest người thật, review cuối.

## Requirements
- Bot ngẫu nhiên và bot tham lam (heuristic: chọn thẻ khắc vũ khí/trait đối thủ kế tiếp theo bảng khắc chế đo được) chạy 2.000 run mỗi loại, song song.
- Mục tiêu: bot ngẫu nhiên thắng run 5–20%; bot tham lam 30–60%; boss khổng lồ màn 3 thắng người chơi ≥ 40% ở bot ngẫu nhiên.
- Chỉnh `RunTuning` (HP bonus, số trait đối thủ, xu, **% hồi khi thắng, giá trị thẻ hồi máu**) trước, chỉ đụng Weapon/TraitTuning nếu bắt buộc.
- Theo dõi thêm: HP trung bình trước boss; tỉ lệ run thua vì 'chết dần' (thua ≥ 2 trận liên tiếp sau boss). <!-- Updated: Validation Session 1 -  HP mang qua trận -->
- Test ngưỡng hồi quy cho độ khó (seed cố định).
- Playtest: user chơi ≥ 3 run; ghi cảm nhận (nhịp, độ rõ, thẻ có ý nghĩa không) → chỉnh.
- Code review cuối; cập nhật `docs/` (GDD Run mode, kiến trúc, cân bằng).

## Related Code Files
- Create: `SimTests/RunBotReport.cs`, `SimTests/RunDifficultyThresholdTests.cs`, `reports/run-balance-*.md`, `reports/playtest-*.md`
- Modify: `Sim/Run/RunTuning.cs`

## Success Criteria
- [ ] Ngưỡng độ khó đạt; test hồi quy xanh.
- [ ] Mọi tiêu chí nghiệm thu toàn plan đạt.
- [ ] Review không còn vấn đề mức cao.

## Risk Assessment
- Bot không phản ánh người thật → playtest là quyết định cuối; bot chỉ để chặn hai đầu (quá dễ/quá khó).
