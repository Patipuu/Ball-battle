---
phase: 5
title: Juice and audio
status: completed
priority: P2
dependencies:
  - 4
effort: 0.5–1 ngày
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
- [x] Mỗi loại sự kiện có hiệu ứng + âm.
- [x] Không GC alloc mỗi frame trong trận (Profiler).
- [x] Rung màn không làm mờ pixel.

## Risk Assessment
- Âm dồn dập khi Fang combo → giới hạn số voice, gộp âm trong 1 tick.

## Kết quả (2026-10-09)
- FxView (cùng GameObject với ArenaView): nháy trắng 0.06 s, số sát thương bay (2 cỡ, chuỗi được cache), hạt pixel 1–2 px (pool 96), rung màn theo pixel nguyên (≤3 px), slow-mo hiệu ứng 0.5 s khi hạ gục, nhạc thắng sau slow-mo.
- Âm thanh synth (WAV 16-bit, tất định, fade 2/4 ms): hit "clack", parry kim loại, wall, death, win. SfxPlayer 8 kênh, cùng clip tối đa 1 lần/30 ms.
- Kiểm: Profiler Recorder GC.Alloc = 0 trong khung giữa trận; render 1080x1920 ngay lúc trúng đòn: 0 khối lệch; ~450 fps; EditMode 12/12; console sạch từ khung đầu.
- Ảnh: `reports/phase-05-hit-capture.png`.

## Ghi chú cho Phase 6 (từ review)
- Tạm dừng: dùng một đồng hồ chung (sim, FX, âm thanh); tạm dừng AudioSource/AudioListener.pause.
- Sang ván mới sau khi slow-mo 0.5 s kết thúc (không cắt hiệu ứng hạ gục); phân biệt nhạc thắng ván / thắng trận.
- Chỉ một AudioListener (đang ở camera Arena).
- Tuỳ chọn: PixelText.SetColor chỉ đổi màu (tránh dựng lại glyph khi popup mờ dần).
