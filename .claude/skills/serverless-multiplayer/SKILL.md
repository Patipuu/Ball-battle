---
name: serverless-multiplayer
description: Kiến trúc multiplayer KHÔNG server game của team — máy một người chơi làm host, các máy khác nối qua Epic Online Services (EOS Connect định danh, EOS Lobby làm phòng/state, EOS P2P + relay làm đường truyền, EOS RTC voice). Stack Unity 6 FishNet + FishyEOS (đã vá) đã ship thật trên Android (Kitchen Together, co-op 2–4); có code mẫu đã gỡ phần game, 6 file patch FishyEOS (1 diff import-vs-upstream + 5 bản vá KT), bug catalog ~60 lỗi, giao thức phòng dùng chung mọi engine, thiết kế Cocos2d-x/Creator native (chưa kiểm chứng), chương quyết định EOS vs server riêng. Dùng khi user nói "làm multiplayer", "online co-op không tốn server", "lobby bạn bè", "phòng join bằng mã", "random match", "rejoin trận", "EOS", "Epic Online Services", "P2P host", "FishNet", "FishyEOS", "voice chat EOS", "port multiplayer sang Cocos", "có nên tự làm server thay EOS", hoặc khi debug lỗi kiểu "người chơi 2 không hiện", "bị đá khỏi phòng", "phòng đóng dù host còn".
---

# Serverless multiplayer (host P2P qua EOS)

**Vấn đề nó giải:** game co-op / PvP bạn bè 2–4 người cần chơi online mà team **không chạy server game realtime**.
Máy một người chơi làm host (authoritative), Epic cấp miễn phí định danh + phòng + relay NAT + voice.
Backend riêng của game chỉ lo tài khoản, bạn bè, lời mời, kinh tế, ranked.

**Nguồn gốc:** Kitchen Together (KT, Unity 6, ship Android). Baseline: KT commit `7d96acd5` (2026-10-05).
Mọi trích dẫn `KT:path:line` trỏ vào repo `Kitchen-Together-Unity` — dùng để tra sâu khi cần, **không bắt buộc** mở được.

> Đây không phải lý thuyết: ~60 lỗi trong `references/04-bug-catalog.md` đều đã xảy ra thật trên thiết bị.
> Hầu hết đến từ việc đối xử với EOS như hệ thống đồng bộ, tức thời. Nó không phải vậy.

## Cây quyết định nhanh

| Tình huống | Đường đi | Đọc |
|---|---|---|
| Unity, native mobile/PC, realtime | **Stack KT**: FishNet + FishyEOS (vá) + EOS Lobby/P2P/RTC | `unity/stack-and-setup.md` → `unity/templates-guide.md` |
| Cocos2d-x / Creator **native** Android/iOS, realtime | EOS C SDK + lớp netcode host-authoritative tự viết, cùng giao thức phòng | `cocos/native-eos-design.md` (**CHƯA KIỂM CHỨNG**) |
| Web / H5 / mini-game (Zalo, WeChat, TikTok…) | **Không có EOS P2P** → lobby + relay trên server team | `cocos/web-minigame-future.md` |
| Turn-based / nhịp chậm / ranked có thưởng | Cân nhắc server-authoritative trên server team | `00-decision-eos-vs-own-server.md` §4 |
| Đang phân vân "có nên bỏ EOS?" | Đọc chi phí thật từng phần | `00-decision-eos-vs-own-server.md` |

## 10 bất biến (vi phạm = lỗi đã từng xảy ra)

1. **Danh tính = PUID do EOS xác thực**, không bao giờ là attribute peer tự ghi. uid backend game là danh tính thứ hai (xã hội), tách riêng.
2. **Mọi await vào SDK đều có watchdog + đường lỗi hiện lên UI.** Callback không về là chuyện bình thường.
3. **Intent/operation có generation + chủ sở hữu**; cái mới vượt cái cũ, kết quả trễ tự huỷ (orphan-leave).
4. **Đọc rỗng / thiếu probe = chưa biết**, không phải "đã rời" / "đã chết". Chỉ kết luận sau nhiều lần đọc.
5. **Barrier chỉ đếm người trong ledger đóng dấu epoch**, không đếm membership thô của lobby (thành viên rớt mạng vẫn lơ lửng trong lobby).
6. **Spawn và hiển thị object cho peer chỉ khi `load_epoch == match_epoch` của chính peer đó.**
7. **Mọi API theo connection đi qua transport cấp cao nhất** (Unity: Multipass), connection id gắn ổn định với PUID.
8. **Stop do chính mình re-dial ≠ host đóng phòng.** Nuốt sự kiện thì phải phát "bỏ cuộc" sau khi hết ngân sách reconnect.
9. **Thời gian chung = tick/đồng hồ replicate**, không phải "lúc tôi nhận được tin".
10. **Handler RPC không bao giờ được ném exception** — một exception ở host = guest bị kick.

Kèm theo, quyết định cấu hình đã đo trên 2 máy: tạo phòng **luôn** `Publicadvertised`; **bắt buộc** khoá trận đang chạy bằng `Joinviapresence` ngay sau khi begin được confirm (ẩn khỏi tìm kiếm nhưng JoinById vẫn được để rejoin), về lobby trả lại `Publicadvertised` (KT product không khoá → người lạ có mã vào được trận đang chạy); **không bao giờ dùng `Inviteonly`** (chặn rejoin). Phòng "private" = attribute `private=1` (ẩn khỏi random match, vẫn vào bằng mã/lời mời), không phải đổi permission (`02` §4).

## Bản đồ tài liệu (đọc theo nhu cầu)

| File | Đọc khi |
|---|---|
| `references/00-decision-eos-vs-own-server.md` | Chọn kiến trúc; 4 việc EOS làm; chi phí tự làm; kiến trúc lai; ranh giới tin cậy |
| `references/01-architecture.md` | Bắt đầu mọi dự án: topology, 5 tầng, authority, sơ đồ luồng boot→phòng→trận→rematch→rejoin |
| `references/02-room-protocol.md` | Implement lớp phòng (mọi engine): state machine, schema attribute, code/random/by-id/invite, visibility, start/load/return barrier, ledger, rejoin, kick, mục **ĐỪNG CHÉP** |
| `references/03-robustness-patterns.md` | Implement bất kỳ lời gọi EOS nào: generation guard, watchdog, lease/fence, deferred leave, polling, **bảng timeout**, reconnect, liveness, auth bootstrap |
| `references/04-bug-catalog.md` | Trước khi viết netcode; khi debug triệu chứng lạ (tra theo triệu chứng) |
| `references/05-eos-portal-and-config.md` | Dựng product EOS mới; config file; secret; DeviceID vs Dev Auth |
| `references/06-optional-modules.md` | Bật/tắt voice, chat, invite, rejoin, random, PvP team, solo, kick, anti-cheat, ranked |
| `references/07-testing-playbook.md` | Test 2 người trên 1 máy và 2 thiết bị; bẫy test; gate trước khi tuyên bố xong |
| `references/unity/stack-and-setup.md` | Cài stack Unity, wiring NetworkManager/Multipass, boot EOS, Android/iOS |
| `references/unity/gameplay-replication.md` | Viết NetworkBehaviour: pattern authority, spawn, start gate, 15 bẫy FishNet |
| `references/unity/templates-guide.md` | Dùng code mẫu: mỗi file làm gì, game phải cấp gì, thứ tự tích hợp, session owner |
| `references/cocos/native-eos-design.md` | Port sang Cocos native (thiết kế, 5 gate kiểm chứng) |
| `references/cocos/web-minigame-future.md` | Web/H5/mini-game (hướng đi, chưa thiết kế chi tiết) |
| `references/roadmap-to-team-sdk.md` | Skill tiến hoá thành SDK package của team thế nào |

## Tài sản dùng ngay (`assets/unity/`)

| Thư mục | Nội dung | Trạng thái |
|---|---|---|
| `Core/` | Policy thuần C# (`TeamNet.Multiplayer.Core`): GenerationGuard, watchdog, epoch/ledger/join-order, rejoin admission, quorum, room code, random match, peer reuse, app-lifecycle | Biên dịch 0 lỗi; **113/113 test pass** (`Tests/Core`) |
| `Eos/` | `EosLobbyService` + operation ownership (lease/fence giữ nguyên thuật toán KT), Dev Auth (`TeamNet.Multiplayer.Eos`) | Biên dịch 0 lỗi; **chưa chạy runtime** ngoài KT |
| `FishNetEos/` | Probe, observer condition theo load epoch, khung spawn service, `AddressTransport`, P2P boundary tri-state | Biên dịch 0 lỗi; cần FishNet codegen khi import; chưa runtime |
| `Editor/` | `EOSAndroidGradlePatch` (bắt buộc cho Unity 6 + Gradle 9) | Biên dịch 0 lỗi |
| `FishyEOS-patches/` | Upstream `ETdoFresh/FishyEOS@1be0b553` + 6 file patch (1 diff import-vs-upstream `00` + 5 bản vá KT `01`–`05`) + bản đã vá (MIT) | Chuỗi patch áp sạch, kết quả trùng bản KT |
| `config-templates/` | Mẫu `eos_*_config.json` chỉ có placeholder | — |

## Quy trình áp dụng cho dự án mới

Gate 1–3 ở đây là mức tối thiểu cho mọi engine; Cocos có 5 gate chi tiết riêng (`cocos/native-eos-design.md`).

1. **Chọn đường** theo cây quyết định; ghi lại lý do (đặc biệt nếu cần ranked có thưởng → server game).
2. **Dựng EOS product** (`05`) — DeviceID cho production, Dev Auth Tool cho test. Không commit secret vào repo public.
3. **Gate 1 — định danh:** 2 máy login ra 2 PUID khác nhau (cùng máy: Dev Auth 2 tài khoản Epic; hai save game KHÔNG tạo hai PUID).
4. **Dựng stack** (Unity: `unity/stack-and-setup.md`; Cocos: `cocos/native-eos-design.md`) + áp bản vá FishyEOS.
5. **Lớp phòng** theo `02` + pattern `03`, dùng `assets/unity/Core` + `Eos` nếu Unity. **Một** session owner duy nhất.
6. **Gate 2 — phòng:** tạo/vào bằng mã, roster = 2, đúng một owner.
7. **Gameplay** theo `unity/gameplay-replication.md`: spawn theo load epoch, observer condition trên **mọi** NetworkManager.
8. **Gate 3 — trận:** 2 thiết bị thật, đổi vai host/guest, chơi hết trận, rematch, rejoin sau kill app, background (`07`).
9. **Module tùy chọn** (`06`) bật từng cái, mỗi cái có gate riêng.
10. **Telemetry 2 phía** ghép theo lobby id trước khi phát hành rộng — đó là công cụ tìm ra phần lớn lỗi của KT.

## Giới hạn — nói thẳng

- Code mẫu `Eos/` và `FishNetEos/` **mới biên dịch được, chưa chạy trong dự án mới**. Thuật toán gốc đã chạy trên thiết bị trong KT; bản tách chỉ đổi kiểu/namespace/phụ thuộc. Dự án Unity đầu tiên dùng skill là gate runtime — ghi kết quả ngược vào skill.
- Cocos: **chưa có dòng code nào chạy thật**; chỉ là thiết kế có trích docs chính thức.
- iOS: chưa kiểm chứng trên KT. Trang Dev Portal Epic chặn đọc tự động → vài bước client policy gắn nhãn CHƯA KIỂM CHỨNG.
- Không host migration: host rời = trận kết thúc.
- Di chuyển client-authoritative: chống gian lận nhẹ, đủ cho chơi với bạn; **thứ có giá trị phải do server game quyết**.
- KT còn lỗi đã biết (ví dụ phòng đang đấu vẫn quảng bá → người có mã phòng chen vào được) — liệt kê trong `02` §ĐỪNG CHÉP và `04` §Còn mở. Không chép.

## Cập nhật skill

Nguồn sự thật: repo `TeamMultiplayerSDK` (`skill/serverless-multiplayer/`, symlink ra `~/.claude/skills/`).
Khi KT hoặc dự án khác sửa lỗi mạng: thêm dòng vào `04-bug-catalog.md` (triệu chứng → nguyên nhân → bất biến → nguồn), cập nhật baseline, chạy lại `dotnet test` trong `assets/unity/Tests/Core`.
Không đưa vào skill: secret EOS, URL backend, IP server, PUID/lobby id/mã phòng thật.
