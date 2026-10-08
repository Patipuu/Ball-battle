---
phase: 5
title: "Juice and audio"
status: pending
priority: P2
dependencies: [4]
effort: "0.5–1 ngày"
---

# Phase 5: Juice and audio

## Overview
Phần làm clip "đã": hitstop thấy rõ, rung màn, nháy trắng, hạt pixel, số sát thương bay, âm "clack". Chỉ ở View, đọc `SimEvent`.

## Requirements
- Hit: nháy trắng 2 frame bóng bị trúng, số dmg bay lên mờ dần, 4–6 hạt pixel màu kẻ tấn công.
- Parry: tia lửa pixel tại điểm chạm, âm kim loại; hitstop do Sim giữ.
- Rung màn theo số nguyên pixel (không subpixel), biên độ theo dmg, trần 3px.
- Death: nổ hạt + slow-mo 0.5 s chỉ ở View (sim vẫn đã kết thúc).
- Âm placeholder sinh bằng code (synth kiểu sfxr → WAV): hit, parry, wall, death, win. Cao độ hit tăng nhẹ theo chỉ số scale.
- Hạt pixel dùng pool, không sinh rác mỗi frame.

## Related Code Files
- Create: `View/FxView.cs`, `View/ScreenShake.cs`, `View/DamagePopup.cs`, `View/SfxPlayer.cs`, `Editor/PlaceholderSfxGenerator.cs`, `Assets/Audio/Generated/*.wav`

## Implementation Steps
1. SfxGenerator → WAV.
2. FxView map sự kiện → hiệu ứng + âm.
3. ScreenShake theo pixel nguyên.
4. Death slow-mo.

## Success Criteria
- [ ] Mỗi loại sự kiện có hiệu ứng + âm.
- [ ] Không GC alloc mỗi frame trong trận (Profiler).
- [ ] Rung màn không làm mờ pixel.

## Risk Assessment
- Âm dồn dập khi Fang combo → giới hạn số voice, gộp âm trong 1 tick.
