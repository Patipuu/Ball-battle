# 00 — Quyết định: EOS làm trung gian hay server riêng?

> Mục đích: trả lời "vì sao dùng EOS, khi nào bỏ EOS" bằng chi phí thật, không cảm tính.
> Baseline: KT commit `7d96acd5` (HEAD 2026-10-05). KT = Kitchen Together (Unity 6, co-op 2–4, đã ship Android).
> Đọc khi: chọn kiến trúc cho game mới, hoặc cân nhắc thay EOS bằng hạ tầng của team.

## 1. "Không tốn server" thực ra nghĩa là gì

Không có **server game realtime**. Máy một người chơi làm host (server + client cùng process), các máy khác nối vào host qua mạng P2P của Epic.
Vẫn có server — nhưng của Epic, miễn phí với mình:

| # | Việc EOS đang làm trong KT | Interface EOS | Nguồn KT |
|---|---|---|---|
| 1 | Định danh ẩn danh theo thiết bị (PUID) | Connect (DeviceID) | `KT:Assets/Scenes/Boot.unity:388-401`, `KT:Assets/Scripts/Net/SessionOrchestrator.cs` (`InitEosAsync`) |
| 2 | Phòng: tạo, mã phòng, tìm ngẫu nhiên, trạng thái phòng/người chơi (attributes) | Lobby | `KT:Assets/Scripts/Net/EosLobbyService.cs` |
| 3 | Đường truyền gameplay host↔client, NAT punch-through, **relay khi không nối thẳng được** | P2P | `KT:Assets/FishNet/Plugins/FishyEOS/Core/*Peer.cs` |
| 4 | Voice chat trong phòng | RTC + RTCAudio | `KT:Assets/Scripts/Net/Voice/EosLobbyVoiceService.cs` |

Những thứ KT **không** dùng EOS (grep 0 kết quả: Presence, Friends, Sessions, Stats, Leaderboards, Achievements, CustomInvites, Ecom…): bạn bè, presence, chat, lời mời, ví, tiến trình — đều trên backend riêng của game (HTTP + WebSocket). Lời mời vào phòng chỉ là một message JSON chứa `roomId` đi qua backend đó (`KT:Assets/Scripts/Net/Invite/InviteSession.cs:9`).

## 2. Từng việc — tự làm thì tốn gì

| Việc | Tự làm trên server team | Độ khó | Chi phí thật |
|---|---|---|---|
| Định danh | Dùng luôn uid/token backend game | Thấp | ~0 |
| Lobby / matchmaking | 1 dịch vụ WebSocket giữ phòng trong RAM (+ Redis nếu nhiều node) | Thấp–vừa | Rẻ. **Lợi thêm:** push thay polling, thao tác nguyên tử, không rate-limit, kick/owner rõ ràng |
| P2P + NAT + relay | Cần relay cho mạng 4G (CGNAT làm nối thẳng hay thất bại). Ví dụ WebRTC data channel + coturn, hoặc relay UDP tự viết; **phải viết transport mới** cho netcode | **Cao** | Băng thông relay + latency (1 vùng VPS vs relay toàn cầu của Epic) + vận hành 24/7 |
| Voice | SFU (LiveKit/mediasoup) hoặc dịch vụ trả phí | Cao | Băng thông audio + vận hành |

**Băng thông relay — ước tính, CHƯA ĐO:** KT gửi pose người chơi mỗi tick (30 Hz, packed) + state theo sự kiện; ước 10–40 KB/s mỗi trận 4 người qua host. Đo thật bằng `FishNet StatisticsManager / NetworkTrafficStatistics` (`KT:Assets/FishNet/Runtime/Managing/Statistic/`) trên một trận đầy đủ trước khi tính tiền. Chỉ phần trận phải đi relay mới tốn (tỉ lệ relay trên mobile cũng phải đo).

## 3. Kết luận thẳng

1. **Giá trị thật của EOS = relay toàn cầu + voice miễn phí.** Đây là hai thứ đắt nhất nếu tự làm.
2. **Lobby EOS là phần tệ nhất.** Nó callback-based, eventually consistent, ghi bị rate-limit, KT không dùng notify nên phải polling; thành viên rời vẫn lơ lửng; lần đọc rỗng không có nghĩa phòng trống. Phần lớn ~21k dòng code mạng KT và phần lớn bug (xem `04-bug-catalog.md` nhóm B, D, E) sinh ra để chống chính những tính chất này.
3. "Không tốn server" có giá: phụ thuộc Epic (chính sách, outage), mỗi product phải cấu hình Dev Portal, ClientSecret buộc nằm trong bản build, hành vi hộp đen (ví dụ EOS tự đóng P2P sau ~30 s mất kết nối, không chỉnh được — `KT:plans/260701-2312-unity-remake-masterplan/reports/w1-step2-rejoin-latency-forkc-device-measurement-260704-report.md:44-49`), Gradle Android phải vá, SDK không có P2P cho Web/H5/mini-game.
4. Netcode Unity: FishNet + FishyEOS được chọn vì "netcode free mạnh nhất + có transport EOS"; NGO bị loại vì đường relay chính của nó là Unity Relay, không phải EOS (`KT:plans/reports/brainstorm-masterplan-design-260701-2312-unity-remake-serverless-eos-report.md:31-32`).

## 4. Ba kiến trúc và khi nào chọn

| Kiến trúc | Thành phần | Chọn khi |
|---|---|---|
| **A. Full EOS (KT hiện tại)** | EOS Connect + Lobby + P2P + RTC; backend game chỉ lo tài khoản/xã hội/kinh tế | Mặc định cho game realtime native (Unity, Cocos native), ít người/trận, cần voice, chưa có hạ tầng realtime |
| **B. Lai (khuyến nghị dài hạn)** | Lobby/matchmaking/presence/invite trên server team; EOS chỉ còn P2P/relay + RTC; host vẫn là máy người chơi | Khi đã có dịch vụ WebSocket ổn định và muốn bỏ độ phức tạp lobby EOS; game có ranked/phần thưởng cần server làm trọng tài cho kết quả |
| **C. Full tự host** | Lobby + relay (WebRTC/coturn hoặc server authoritative) + voice tự vận hành | Web/H5/mini-game (EOS P2P không có); thị trường mà Epic không phục vụ ổn; game turn-based / nhịp chậm (băng thông nhỏ, server authoritative dễ, chống gian lận tốt); hoặc khi CCU đủ lớn để tự chủ |

Tiêu chí chuyển A → B: (1) có service WebSocket production của team đã chạy ổn, (2) team chấp nhận viết lại lớp lobby của engine (không còn lease/fence EOS nhưng cần giao thức tương đương `02-room-protocol.md`), (3) có bằng chứng thiết bị rằng P2P EOS vẫn nối được khi lobby không còn là EOS (host PUID truyền qua server team).
Tiêu chí chọn C ngay: nền tảng không có EOS P2P, hoặc mọi trận đều cần server làm trọng tài.

## 5. Thiết kế lai B (mức interface — CHƯA KIỂM CHỨNG)

Giữ nguyên giao thức phòng (`02-room-protocol.md`), chỉ thay "kho trạng thái phòng":

```text
ILobbyBackend (đề xuất trong KT:docs/multiplayer-modules/architecture.md §Contract backend)
  CreateAsync / JoinByIdAsync / JoinByCodeAsync / RandomAsync / LeaveAsync
  SetRoomAttrs(host-only) / SetMemberAttrs(self-only) / Kick(host-only)
  Snapshot (immutable, có revision) + event Changed (push)
Server team cấp: roomId, code, owner, members[{playerId, eosPuid, attrs}], revision
EOS còn lại:  Connect login → PUID; client dial owner.eosPuid qua P2P; RTC room theo roomId
```

Điểm phải giữ khi chuyển:
- Danh tính trong barrier vẫn là **PUID đã xác thực**, server team phải ánh xạ `playerId ↔ eosPuid` do chính client đã login EOS gửi lên (không tin trường tự khai của người khác).
- Epoch, ledger người tham gia, load barrier, rejoin admission giữ nguyên ngữ nghĩa — chỉ đổi chỗ lưu.
- Server có push + atomic nên **bỏ được**: polling roster, lease/fence chống callback trễ, "đọc rỗng = chưa biết". Đừng bỏ trước khi server thật sự cung cấp đảm bảo đó.
- Voice EOS RTC đang gắn với lobby EOS (`EnableRTCRoom` lúc create) → cần RTC room độc lập (EOS RTC standalone room) — CHƯA KIỂM CHỨNG, phải thử trước.

## 6. Ranh giới tin cậy (áp dụng cả 3 kiến trúc)

- Host là máy người chơi → **không đáng tin** cho thứ có giá trị. Ranked, phần thưởng đối kháng, kinh tế: quyết định trên server game (nộp kết quả + kiểm tra hợp lý, hoặc server authoritative cho loại game đó).
- Trên máy chỉ chống gian lận nhẹ: host kiểm tra tầm với / tần suất / NaN cho mọi ServerRpc (`KT:Assets/Scripts/Gameplay/Player/PlayerController.cs:780-855`). Di chuyển trong KT là client-authoritative → client sửa đổi có thể dịch chuyển; chấp nhận được cho chơi với bạn bè.
