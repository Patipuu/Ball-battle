# 06 — Module tùy chọn: bật/tắt theo game

> Mục đích: danh mục module ngoài lõi (lõi = identity + lobby + transport + barrier ở `01`/`02`), mỗi module có điều kiện bật, phụ thuộc, bất biến và nguồn KT.
> Baseline: KT commit `7d96acd5` (HEAD 2026-10-05).
> Đọc khi: chốt scope tính năng multiplayer của game mới. Đối tượng: tech lead, dev netcode/lobby.

Nhãn: **[GENERIC]** mọi engine · **[KT-SPECIFIC]** chi tiết riêng KT · **CHƯA KIỂM CHỨNG** = chưa đo trên thiết bị.
Template mỗi module: **Mục đích · Khi nào bật · Phụ thuộc · Bất biến · Nguồn KT**. Bất biến đã có trong `04` thì chỉ trỏ mã lỗi.

## Bảng tổng

| # | Module | Mặc định đề xuất | Chi phí bật | Trạng thái verify ở KT |
|---|---|---|---|---|
| 1 | Voice (EOS RTC) | Tắt cho bản ship đầu | Cao: quyền mic, 2 đường input, matrix thiết bị | Unit test; thiết bị: chưa xong 2 chiều |
| 2 | Lobby chat | Bật nếu có màn lobby | Thấp | Unit test + code (7d96acd5) |
| 3 | Invite port | Bật nếu game có bạn bè | Trung bình (backend game) | 2 AVD gửi/nhận bạn bè; invite ngoài Main Menu: chưa |
| 4 | Rejoin | Bật | Trung bình | Một phần (đường gym) |
| 5 | Random match | Bật nếu có nút Quick Play | Thấp | Unit test |
| 6 | PvP teams | Chỉ game có đội | Thấp | Unit test |
| 7 | Solo loopback | Bật nếu có chế độ 1 người | Thấp | Production |
| 8 | Kick | Bật | Thấp | Code có; 2 thiết bị: chưa |
| 9 | Anti-cheat nhẹ | **Luôn** bật | Thấp | Production |
| 10 | Ranked / phần thưởng server-side | Bắt buộc nếu có giá trị thật | Cao (backend) | KT chưa làm cho co-op stars |

---

## 1. Voice — EOS RTC phòng lobby [GENERIC cơ chế, KT-SPECIFIC chi tiết]

| Mục | Nội dung |
|---|---|
| Mục đích | Voice theo phòng, không server riêng: lobby tạo kèm RTC room (`EnableRTCRoom`), mọi member tự vào room voice |
| Khi nào bật | Game cần phối hợp nói; chấp nhận chi phí xin quyền mic + test thiết bị. KT có kill-switch ship (`AppConfig.ShipForceVoiceOff`) vì bản Godot production tắt voice |
| Phụ thuộc | Client policy có Voice `createLobbyConference` (`05` §1). Android `RECORD_AUDIO` trong manifest; iOS `NSMicrophoneUsageDescription`. Lõi lobby (`02`) |
| Bất biến | (1) **Join muted**: `LocalAudioDeviceInputStartsMuted = true`, bind xong force-mute. (2) **Chưa có quyền mic → `UseManualAudioInput = true`** để EOS không mở thiết bị mic (Android log ngập lỗi capture, `04` F1). (3) Xin quyền **muộn** — chỉ khi người chơi bấm unmute. (4) Lần unmute đầu sau khi được cấp quyền trong phòng manual-input: **rời và vào lại CHỈ RTC** với native input, vẫn muted; lobby/gameplay giữ nguyên; khôi phục deafen + mute từng member rồi mới bật gửi (`04` F2). (5) Callback native trễ không bao giờ được unmute; mute chỉ "thành công" khi native xác nhận trong cùng phiên. (6) Phục hồi thất bại → tắt binding voice, bảo người chơi rời/vào lại phòng, không đổi membership. (7) Mọi static reset mỗi lần Play (`04` F3). (8) Editor/desktop coi như luôn có quyền — không phải bằng chứng thiết bị |
| Nguồn KT | `KT:Assets/Scripts/Net/EosLobbyService.cs:182-205` (create: RTC options), `:1046-1066` (join) · `KT:Assets/Scripts/Net/Voice/VoiceChatFeature.cs:8-55` · `KT:Assets/Scripts/Core/AppConfig.cs:314-318,358-364` · `KT:Assets/Scripts/Net/Voice/VoiceRtcInputRecovery.cs:14-41` · `KT:Assets/Scripts/Net/Voice/EosLobbyVoiceService.InputRecovery.cs` · `KT:Assets/Scripts/Net/Voice/VoiceSendingGuard.cs` · `KT:Assets/Scripts/Net/Voice/MicrophonePermission.cs:11,31-74` · `KT:Assets/Plugins/Android/EOS/eos_dependencies.androidlib/AndroidManifest.xml` (RECORD_AUDIO) · `KT:ProjectSettings/ProjectSettings.asset` (`microphoneUsageDescription`) · `KT:docs/voice-chat/doc.md:5-40,54` (emulator: 2 chiều CHƯA KIỂM CHỨNG) |

## 2. Lobby chat — member attribute mailbox [GENERIC]

| Mục | Nội dung |
|---|---|
| Mục đích | Bong bóng chat ngắn trong lobby, không server chat, không lịch sử |
| Khi nào bật | Có màn lobby chờ; không cần lịch sử/kiểm duyệt nặng (cần lưu/kiểm duyệt → dùng chat backend game) |
| Phụ thuộc | Lõi lobby + polling (`03` §7); 4 member attribute `chat_0..chat_3` (scope Member) trong ngân sách attribute (`02` §2.2) |
| Bất biến | (1) Payload `v1|<guid32 phiên>|<seq>|<text>`; text ≤ **256** UTF-16, không `\0`, UTF-8 hợp lệ; raw > 256+64 bị bỏ trước khi parse. (2) Ghi xoay vòng `chat_[(seq-1) % 4]` — 4 ô sống qua coalescing của poll. (3) **Danh tính người gửi = PUID của member snapshot**, không bao giờ từ payload. (4) Khi vào phòng chạy `Poll(baseline:true)` để không phát lại tin cũ. (5) Dedupe theo `(PUID, phiên) → seq lớn nhất`; tối đa 128 cursor (chống bơm token). (6) Một lần gửi tại một thời điểm; echo local + xoá ô nhập **chỉ sau khi EOS chấp nhận** write; timeout UI 10 s không mở khoá ghi chồng. (7) Write attribute phải kiểm từng add + release handle (`04` B10) |
| Nguồn KT | `KT:Assets/Scripts/Net/LobbyChatChannel.cs:16-122` · `KT:Assets/Scripts/Net/LobbyAttributes.cs:58-61,104-107` · `KT:Assets/Scripts/Net/ProductLobbySession.cs:2305-2311` · commit `7d96acd5` |

## 3. Invite port — backend game cung cấp [GENERIC contract, transport KT-SPECIFIC]

| Mục | Nội dung |
|---|---|
| Mục đích | Mời bạn vào phòng. Bạn bè/presence/kênh đẩy thuộc **backend game**; skill chỉ quy định port `SendInvite(friendUid, payload)` / `OnInvite(payload, senderUid)` |
| Khi nào bật | Game có hệ bạn bè trên backend riêng. Không dùng EOS Custom Invites/Friends |
| Phụ thuộc | Backend có kênh push tới client (KT: WebSocket chat); uid backend tách khỏi PUID (`04` A5); JoinById (`02` §3.4) và visibility không phải `Inviteonly` (`02` §4) |
| Bất biến | (1) Payload `{"type":"invite_join_room","mode":…,"roomId":<lobbyId>,"v":1}`; parse fail-closed; content > 4096 bỏ; field > 256 từ chối. (2) Gate gửi theo thứ tự: roomId rỗng → đang connect → **hết ghế** (`maxMembers − liveMembers ≤ 0`) → target rỗng → tự mời mình → **phase ≠ LOBBY** → **cooldown 7 s/bạn** (chỉ tính khi gửi thành công). (3) Bên nhận dedupe theo `roomId|senderUid`; nhận → `JoinById`. (4) Có timeout cho "Continue/Join" (EOS có thể nhận join mà transport không thành, `04` E4). (5) **Hiển thị lời mời ở mọi scene** (lobby, menu, cả khi đang trận thì xếp hàng/toast) — KT chỉ hiện ở Main Menu và **bỏ** lời mời ở nơi khác (lệch, `04` Còn mở #6). (6) Gate riêng của game (vd PvP cần mở khoá level) nằm ở game, không ở port |
| Nguồn KT | `KT:Assets/Scripts/Net/Invite/InviteSession.cs:12-33` (payload), `:56-61` (giới hạn), `:144-174` (gate), `:193-197` (cooldown), `:212-213` (dedupe), `:380-389` (chỉ Main Menu), `:396-429` (frame WebSocket), `:436-449` (gate PvP) · `02` §3.5 |

## 4. Rejoin [GENERIC]

| Mục | Nội dung |
|---|---|
| Mục đích | Người chơi rớt/kill app giữa trận vào lại đúng trận đang chạy |
| Khi nào bật | Trận dài hơn vài phút hoặc mobile (background, mất sóng) |
| Phụ thuộc | Lưu `lobbyId` bền (save local); visibility khi đấu = `Joinviapresence` (ẩn search, JoinById vẫn được); ledger `former_members` đóng dấu epoch (`02` §9); reconnect loop (`03` §9) |
| Bất biến | (1) Lưu id khi join/create commit; **chỉ xoá khi rời chủ động hoặc bị kick**, kill/crash giữ lại (`04` E5). (2) Route theo attribute phòng đã replicate, không theo state cục bộ đang dựng (`04` D9). (3) Gate host `RejoinAdmission`: lobby phase → Allow; host local → Allow; PUID chưa có → **Unknown (retry, không reject)**; trong grace sau start → Allow; ledger thiếu/sai epoch → Allow; còn lại Allow nếu PUID ∈ ledger. Mọi bất định nghiêng về **Allow** — gate là lớp phụ, lớp chính là ẩn phòng + barrier theo ledger. (4) Host chủ động đóng peer bị gián đoạn sau grace ngắn để slot giải phóng nhanh (`04` C5). (5) Gắn gate vào **session owner thật** (KT chỉ chạy ở đường gym — `02` §12 #5) |
| Nguồn KT | `KT:Assets/Scripts/Net/RejoinAdmission.cs:40-76` · `02` §10 (dòng code lưu/xoá id, popup Continue) · `KT:Assets/Scripts/Net/RoomVisibility.cs` |

## 5. Random match [GENERIC]

| Mục | Nội dung |
|---|---|
| Mục đích | "Quick Play": vào phòng người lạ cùng mode, không có thì tự tạo |
| Khi nào bật | Có lượng người chơi đồng thời đủ; không cần MMR (cần MMR → matchmaking server game) |
| Phụ thuộc | Bucket id chung; room attr `mode`, `bundle` (build), `is_room_started`; `MaxLobbyMembers` đúng theo mode (`04` B7) |
| Bất biến | (1) Lọc **trước** khi join: hết ghế · đã start · khác mode (phòng không có mode = phòng harness → loại) · khác bundle (thiếu bundle thì cho qua để tương thích build cũ) · `private=="1"` (phòng riêng — dự án mới, KT chưa có; `02` §4 luật 4). (2) Join fail chỉ fallback **create** khi là matchmaking-miss (`not-found`, `NotFound`, `LobbyTooManyPlayers`, `LobbyLobbyAlreadyExists`); lỗi auth/huỷ/timeout phải báo lên. (3) Có watchdog như mọi lệnh lobby (`03` §2) |
| Nguồn KT | `KT:Assets/Scripts/Net/RandomMatchPolicy.cs:39-86` · `02` §3.3 |

## 6. PvP teams [GENERIC cơ chế, KT-SPECIFIC luật]

| Mục | Nội dung |
|---|---|
| Mục đích | Chia đội tất định không cần UI chọn đội: **join order → đội** |
| Khi nào bật | Mode đối kháng theo đội |
| Phụ thuộc | Host publish mapping `join_order` lúc start (cùng batch ledger, `02` §6); roster phân giải theo PUID |
| Bất biến | (1) KT 2v2: `join_order` 0–1 = Red (đội 0), 2–3 = Blue (đội 1); ngoài 0..3 (vd sentinel `999` = chưa map) ⇒ **không có đội**, không đoán. (2) Join order trùng hoặc ngoài khoảng ⇒ fail (chờ), không spawn chồng. (3) Start chỉ khi đủ người (`HasRequiredPlayers`: PvP cần đúng 4). (4) Mode PvP không bao giờ load map campaign. (5) Mọi luật đội (giao món, vé HUD, mute voice đội kia) đọc **cùng** hàm join order → đội; member join order không rõ không bị coi là đối thủ. (6) Spawn slot = `join_order % spawnCount` (`04` G4) |
| Nguồn KT | `KT:Assets/Scripts/Net/PvpMatchStartPolicy.cs:12-13,37-65,104-134` · `KT:Assets/Scripts/Net/Voice/VoiceTeamMute.cs:38-66` · `KT:Assets/Scripts/Gameplay/Orders/PvpOrderPolicy.cs:74-100` |

## 7. Solo loopback (Multipass + Tugboat) [UNITY; ý tưởng GENERIC]

| Mục | Nội dung |
|---|---|
| Mục đích | Chế độ 1 người/tutorial chạy **cùng code host-authoritative** nhưng không cần EOS (offline, không auth) |
| Khi nào bật | Game có solo/tutorial dùng chung gameplay mạng |
| Phụ thuộc | FishNet Multipass với thứ tự cố định `[0]=Tugboat` (127.0.0.1, maxClients=1), `[1]=FishyEOS`. Engine khác: transport "local" in-process |
| Bất biến | (1) Chọn transport: local khi `singleplay || tutorial || !inRoom`, EOS chỉ khi đang trong phòng thật. (2) Index là hằng chung giữa code và scene. (3) **Chọn lại client transport trước mọi lần dial** (kể cả watchdog) vì lựa chọn của Multipass là sticky (`04` C6). (4) Phân loại spawn theo MODE phòng, không theo "có phòng hay không" (`04` G1). (5) Per-connection API qua top-level transport (`04` C1) |
| Nguồn KT | `KT:Assets/Scripts/Net/TransportRoutingPolicy.cs:15-33` · `KT:Assets/Scenes/Boot.unity` (Multipass `_transports`, ~:447-463) |

## 8. Kick [GENERIC]

| Mục | Nội dung |
|---|---|
| Mục đích | Chủ phòng loại một member khỏi lobby |
| Khi nào bật | Mọi phòng có người lạ (random/code công khai) |
| Phụ thuộc | Client policy cho phép thao tác lobby của owner; EOS `KickMember`; nhận diện bằng PUID |
| Bất biến | (1) Chỉ owner, chỉ trong phase LOBBY, không tự kick mình. (2) Bên bị kick phát hiện khi mình biến mất khỏi member list trong **poll** (nguồn sự thật) → toast, **xoá room id đã lưu** (không mời "Continue"), dọn state. (3) Notify `AddNotifyLobbyMemberStatusReceived` (KICKED) chỉ là tối ưu độ trễ tùy chọn: nhận thì đọc lại snapshot ngay; không bao giờ thay polling (`02` §11, `03` §7). (4) Đưa kick lên seam lobby chung (KT phải ép kiểu `EosLobbyService`). (5) Muốn chặn quay lại bằng mã → cần ban-list do owner giữ (KT chưa có) |
| Nguồn KT | `KT:Assets/Scripts/Net/EosLobbyService.cs:1644-1680` · `KT:Assets/Scripts/Net/ProductLobbySession.cs:1886-1910` (owner kick), `:2405-2420` (kickee) · `KT:Assets/Scripts/UI/Screens/Lobby/LobbyScreen.cs:2472` · 2 thiết bị: CHƯA KIỂM CHỨNG (`04` Còn mở #7) |

## 9. Anti-cheat nhẹ trên host [GENERIC]

| Mục | Nội dung |
|---|---|
| Mục đích | Chặn spam/vật lý vô lý từ client sửa đổi **trong một trận**; giữ trận co-op không bị phá |
| Khi nào bật | Luôn — rẻ, và đồng thời chặn bug client (NaN, RPC trùng) |
| Phụ thuộc | Mô hình authority `01` §3: client gửi yêu cầu, host thực thi |
| Bất biến | Mọi ServerRpc: (1) **rate-limit** theo người gửi (KT `AcceptsActionRpc`, interact + emote); (2) **tầm với**: so vị trí server của player với mục tiêu ≤ max distance; mục tiêu phải là object đã đăng ký còn sống; (3) **NaN/Inf** reject; clamp origin/hướng về giá trị server biết (ném); (4) handler **không bao giờ ném** (`04` G5); (5) không tin field tự khai (tên, id, điểm). Giới hạn: host là máy người chơi — host sửa đổi thì không gì chặn được ⇒ xem §10 |
| Nguồn KT | `KT:Assets/Scripts/Gameplay/Player/PlayerController.cs:780-855` · `KT:Assets/Scripts/Gameplay/KitchenGameplayMath.cs:20-30,45-84,858-859` · EOS Anti-Cheat **không** dùng (cần trusted server / client protect — ngoài phạm vi) |

## 10. Ranked / phần thưởng — PHẢI ở server game [GENERIC, bắt buộc]

| Mục | Nội dung |
|---|---|
| Mục đích | Mọi thứ có giá trị lâu dài (tiền ảo, xếp hạng, sao mở khoá, phần thưởng) |
| Khi nào bật | Ngay khi kết quả trận đổi thành giá trị thật |
| Phụ thuộc | Backend game có tài khoản (uid), API idempotent; game cung cấp dữ liệu trận đủ để server kiểm |
| Bất biến | (1) **Host P2P không đáng tin**: kết quả do host/clients gửi chỉ là *claim*; server quyết định. (2) Server tự tính phần thưởng từ input tối thiểu (run id, số ngày, thời lượng) + giới hạn hợp lý (trần điểm/phút, tần suất, thời lượng trận tối thiểu); **không** nhận "số sao/số tiền" client tự khai. (3) Idempotent theo run/match id (claim 2 lần = 1 lần). (4) Ranked thật cần đối chiếu nhiều nguồn (mỗi peer gửi kết quả, lệch ⇒ không tính) hoặc dedicated server — kiến trúc host P2P không cho ranked chống gian lận mạnh |
| Nguồn KT | Mẫu đúng: `KT:Assets/Scripts/Network/LambdaAPI.Endpoints.cs:474-485` (phần thưởng chế độ UIA — server tính) · Mẫu **đừng chép**: `:331-333` (`AddScoreLevel(stars, level)` — client tự khai số sao) · `01` §3 |
