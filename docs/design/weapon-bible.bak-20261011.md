# Weapon Bible — bản sắc 8 vũ khí (đề xuất Game Designer, 11/10/2026)

**Vì sao cần:** research report Step 3 chỉ ra 8 vũ khí map 1-1 với roster Earclacks: Blade↔Sword, Fang↔Dagger, Pike↔Spear, Brawler↔Unarmed, Volley↔Bow, Venom↔Scythe, Aegis↔Shield, Rig↔Wrench. Có review chê đối thủ là "blatant rip off".

**Nguyên tắc:**
1. Giữ **id nội bộ** (`blade`, `fang`…), vì save, unlock, test và hash đều dùng id. Chỉ đổi `DisplayName` trong `WeaponRegistry` (View/Run text).
2. Mỗi vũ khí có **1 quy tắc lớn dần + 1 điểm yếu cố ý**, nhìn thấy được trên màn hình.
3. Hình tượng **không phải vũ khí cầm tay cổ điển**, để tách khỏi sword/dagger/spear/bow/scythe/shield/wrench.
4. Tên EN ≤ 8 ký tự, dùng font 3x5 (A-Z), vừa lưới chọn 4x2 của Versus.

**Màu (Palette.cs hiện tại) và xung đột cần xử lý:**
| Mục | Màu | Ghi chú |
|---|---|---|
| Reflect damage | magenta (255,90,200) | Theo yêu cầu; **chưa thấy trong bản source đã giải nén** (FxView/DamagePopups ở đây chưa có). Coi là quy ước mới. **Không vũ khí nào được dùng hồng/magenta.** |
| Poison dots / FX | lime (96,255,64) | `StatusView`, `FxView` |
| Heal FX | (80,255,160) | |
| Heat tint lưỡi | trắng → Hot (255,70,50) | `BallView`: chỉ tô lưỡi, nên **Brawler không có heat hiển thị** |
| WallWarning | (232,180,60) | header boss, cảnh báo co sân |

Xung đột phát hiện:
- Fang (140,224,80) gần poison lime.
- Volley (240,200,64) gần như trùng WallWarning.
- Venom purple (176,96,224) khá gần magenta reflect.
- Brawler (176,176,192) gần Metal (200,205,220), nên trông như "không có gì".

Đổi màu chỉ đụng `Palette.Weapons` + chạy lại `Generate Placeholder Art`. **Không ảnh hưởng Sim.**

---

## 1. `blade` → **EMBER / Than Hồng**
- **Cá tính:** thợ rèn lì lợm. Chậm mà chắc, càng đánh càng nóng. Thuộc kiểu "người hùng ổn định", dễ hiểu nhất, là vũ khí đầu tiên người mới cầm.
- **Hình bóng:** thanh sắt nung, phần lõi đỏ dần (đã có heat tint trắng→Hot, HotAt = 20 dmg). Thân bóng như cục than có vân nứt. Hạt tia lửa khi trúng.
- **Màu:** giữ đỏ (232,72,72); lưỡi tint theo heat.
- **Quy tắc lớn dần (giữ):** dmg 1, +1 mỗi hit; dài 24, spin 6.
- **Điểm yếu cố ý:** bị parry nhiều thì không lớn. Thua kiểu chơi parry (Fang, Parry Master). Blade vs Fang 51.5/48.5 (balance first pass).
- **Tinh chỉnh tách biệt (tuỳ chọn):** `QUENCH`: mỗi lần bị parry, lưỡi "nguội" −0.5 dmg (không dưới 1), hiển thị heat giảm. Biến điểm yếu thành thứ người xem đọc được.
  - **[SimVer]**: đổi kết quả Versus và `VersusGoldenTests`, cần chạy lại ma trận.

## 2. `fang` → **MANTIS / Bọ Ngựa**
- **Cá tính:** hiếu động, cuồng loạn, chém liên hồi. Trận của nó trông "điên".
- **Hình bóng:** hai càng ngắn cong như lưỡi liềm nhỏ, không phải dao. Có afterimage (vệt mờ) khi spin > 24°/tick (view-only).
- **Màu:** đổi sang **xanh ô-liu đậm (112,168,56)** kèm sọc tối, để tách khỏi chấm độc lime sáng. Cần kiểm bằng capture BB_CAPTURE.
- **Quy tắc lớn dần (giữ):** spin 8 → tối đa 40 (+3 mỗi hit, gain ×0.98); dmg 1 +0.4/hit; cooldown riêng 3 tick; knockback ×0.3 (combo).
- **Điểm yếu cố ý:** thân không lưỡi (không parry được). Fang vs Brawler 33.2/66.8, sát sàn 30%.
- **Tinh chỉnh:** `FRENZY DECAY`: không trúng trong 2 s thì spin giảm 0.5°/tick về mức đầu. Nhấn bản sắc "phải giữ nhịp".
  - **[SimVer]** + đo lại. Có thể làm Fang yếu đi, nên đi kèm buff nhỏ (dmg/hit 0.4→0.45).

## 3. `pike` → **ANGLER / Cần Câu**
- **Cá tính:** ông câu kiên nhẫn. Đầu trận vụng, cuối trận "thả câu" quét cả sân. Đúng vai "bóng tiềm năng".
- **Hình bóng:** cần câu với dây kéo dài, đầu là lưỡi câu (không phải mũi giáo). Sprite 9-slice đã kéo dài theo BladeLength, nên phần giữa là dây, phần đầu là móc.
- **Màu:** giữ cyan (64,208,232) (nước). Dây trắng-xám.
- **Quy tắc lớn dần (giữ):** dài 26 → 90, +0.75 cả dài lẫn dmg mỗi hit; spin 4.
- **Điểm yếu cố ý:** dây dài thì dễ bị parry. Arena co về 110 thì dây 90 gần như không chỗ quay (cần đo riêng Pike × tight; report arena hiện không tách số Pike).
- **Tinh chỉnh:** `SWEET SPOT`: 25% đầu móc gây ×1.5, phần dây ×0.75. Thưởng cho đòn "móc" đúng tầm.
  - **[hook mới]**: `HitContext` cần thêm khoảng cách điểm chạm dọc lưỡi. **[SimVer]**.

## 4. `brawler` → **WRECKER / Quả Tạ**
- **Cá tính:** trâu phá dỡ. Càng chạy càng hung, đập tường để lấy đà.
- **Hình bóng:** quả tạ sắt có đinh tán, không vũ khí. Vệt tốc độ phía sau khi gần trần tốc. Thân nóng đỏ dần (**heat tint lên thân**, vì hiện bladeless không có heat).
- **Màu:** đổi sang **sắt đậm (112,112,128)** để tách khỏi Metal. Viền đỏ khi nóng (ColdAt 7 → HotAt 14 theo trần tốc hiện có).
- **Quy tắc lớn dần (giữ):** dmg = tốc × 0.1 (+0.01/hit); trần tốc −3, +0.5/hit, +0.2/tường, tối đa +4; mỗi lần đập tường +8% tốc.
- **Điểm yếu cố ý:**
  - Heavy (khối lượng ∝ r²), Spiky (Reflect khi va thân), Aegis phản 50% đòn thân.
  - Spike-walls 36.5% (đập tường là tự hại).
  - Gương Wrecker: chỉ bóng nhanh hơn gây sát thương.
- **Tinh chỉnh:** không đổi luật. Ưu tiên **cân lại combo** Brawler + Vampire2 + SecondWind2 + Thorns2 = 85.7% (xem card-pool-v2 §c). Heat thân + vệt tốc là view-only, không [SimVer].

## 5. `volley` → **SPARKLER / Pháo Hoa**
- **Cá tính:** kẻ thích phô diễn. Đứng xa bắn pháo, giáp lá cà thì lúng túng.
- **Hình bóng:** ống pháo hoa ngắn (thay cung), đạn là tia sáng 2–3 px để lại vệt lấp lánh. Đạn đã nảy 1 lần (`Bounces = 1`), hợp hình ảnh pháo hoa dội tường.
- **Màu:** đổi sang **vàng nhạt (255,224,128)** + vệt trắng, tách khỏi WallWarning (232,180,60).
- **Quy tắc lớn dần (giữ):** +1 tia mỗi hit cận chiến (tối đa 8), +0.1 dmg tia; loạt mỗi **90 tick = 1.5 s** (report Phase 4 ghi 1.25 s là sai so với code); tia 0.9 dmg, tốc 5, sống 3 s.
- **Điểm yếu cố ý:** sân nhỏ (tight 37.4%), bumpers 39.3%, parry đánh bật tia (đổi chủ); lưỡi cùn 12 px.
- **Tinh chỉnh:** `FINALE`: khi đủ 8 tia, loạt kế tiếp nổ hình quạt rộng gấp đôi (spread 8°→16°) kèm hiệu ứng pháo hoa.
  - Spread mỗi instance cần field mới. **[SimVer]** nhẹ (chỉ trận có 8 tia).

## 6. `venom` → **SPORE / Nấm Độc**
- **Cá tính:** kẻ lặng lẽ, thắng bằng thời gian. "Không vội, cứ để nấm mọc."
- **Hình bóng:** lưỡi dày 6 thành **mũ nấm** bản rộng. Thân bóng có đốm. Bào tử lime bay khi trúng (khớp màu chấm độc).
- **Màu:** tím dịu về **(140,80,210)** (bớt đỏ) để tách khỏi magenta reflect và badge Glass Cannon (170,80,170).
- **Quy tắc lớn dần (giữ):** giới hạn stack độc 1 → 8 (+1/hit), độc 1 dps × 4 s mỗi stack; melee 1.5 +0.25/hit.
- **Điểm yếu cố ý:** thua dồn sát thương nhanh (burst); đầu trận yếu (1 stack).
- **Outlier:** spike-walls 72% (report Phase 5: đổi sát thương tường 1 hoặc 3 không hạ được, nguồn là lối đánh tiêu hao).
- **Tinh chỉnh:** thử `VenomSeconds` 4→3 cùng `VenomDps` 1→1.2 (tổng/stack 4→3.6, độc "nhanh hơn, ngắn hơn" ít ăn lợi ở trận kéo dài) rồi đo lại arena. **[SimVer]**.

## 7. `aegis` → **MIRROR / Gương Thần**
- **Cá tính:** đấu sĩ kiêu ngạo, phản đòn. "Mày đánh càng mạnh, tao trả càng đau." Nhân cách hợp meme "nó ghét PC của Earclacks", nhưng là của riêng mình.
- **Hình bóng:** tấm gương bầu dục xoay (thay khiên), mặt gương loé sáng khi parry. Reflect bay ra màu **magenta**, trở thành chữ ký của Mirror.
- **Màu:** giữ xanh (80,128,232); khung bạc.
- **Quy tắc lớn dần (giữ):** độ rộng 7 → 14 (+0.25/hit); chip 0.7 + 0.25/đơn vị rộng.
  - Parry phản 40% đòn đối thủ; đòn thân phản 50%; đạn: hồi 20% thành máu; regen 0.25/s.
  - "Rage" từ 900 tick (15 s), +100% mỗi 600 tick.
- **Điểm yếu cố ý:** độc (Venom), sân chật (tight **26.2%**, thấp nhất bảng arena).
- **Tinh chỉnh:**
  - Đặt tên và hình cho rage là `CRACK`: sau 15 s gương nứt dần (sprite vết nứt theo mức rage), đánh mạnh hơn. View-only, không [SimVer]. Người xem hiểu vì sao Mirror tự nhiên mạnh lên.
  - Tight 26.2%: cân nhắc rage sớm hơn khi sân nhỏ. Không khuyến nghị (đụng ArenaConfig). Nên để thẻ counter/arena preview giải quyết, chờ số đo.

## 8. `rig` → **HIVE / Tổ Ong**
- **Cá tính:** kỹ sư cần mẫn, xây trước đánh sau. Chậm khởi động, cuối trận cả sân là "tổ".
- **Hình bóng:** mỗi turret là **ô lục giác** (thay ụ súng / cờ lê), bắn "ngòi". Ô bị sân co nuốt thì vỡ thành mảnh vụn (đã có luật turret mất khi arena chạm tới).
- **Màu:** giữ cam mật (232,144,64); ô tổ viền nâu (Wood 140,92,56).
- **Quy tắc lớn dần (giữ):** mỗi hit melee đặt 1 ô (tối đa 6, thay ô cũ nhất); tất cả bắn mỗi 68 tick; ngòi 1.1 dmg, tốc 3.5, sống 2.5 s; melee 1.5 +0.2/hit.
- **Điểm yếu cố ý:** đầu trận chưa có ô (phải trúng trước); sân co nuốt ô; Parry Master trên Rig đặt ô khi parry (cần theo dõi khi retune).
- **Tinh chỉnh (tuỳ chọn):** `SWARM`: khi đủ 6 ô, interval 68→56. Thưởng xây đủ tổ. Field instance mới + **[SimVer]**.

---

## Tóm tắt tác động
| Hạng mục | Sim | Ghi chú |
|---|---|---|
| Đổi DisplayName, sprite, màu, afterimage, heat thân Brawler, CRACK Mirror | không | `WeaponRegistry` DisplayName, `Palette`, ArtPainter, BallView, FxView |
| QUENCH, FRENZY DECAY, Venom 3s/1.2, FINALE, SWARM | **[SimVer]** + chạy lại `MatchupReport` / `BalanceThresholdTests` / `ArenaBalanceReport` | Làm sau đợt view-only; mỗi lần đổi 1 vũ khí để số liệu sạch |
| SWEET SPOT (Angler) | **[hook mới]** + [SimVer] | `HitContext` thêm vị trí điểm chạm |

**Việc kèm theo:**
- Mỗi vũ khí có 1 câu "tagline" ≤ 24 ký tự cho Prepare/Versus, ví dụ `HOTTER EVERY HIT`, `HIT ME. I DARE YOU`.
- Animation thắng riêng (GĐ0 Step 3).
- Kiểm tên EN không trùng game cùng thể loại trước khi chốt. Chưa kiểm trademark.

**Câu hỏi cho Phú:**
- Có đồng ý đổi tên hiển thị và hình tượng không?
- Muốn giữ chất "vũ khí" hay chấp nhận hướng sinh vật/đồ vật (Mantis, Hive, Spore)?
