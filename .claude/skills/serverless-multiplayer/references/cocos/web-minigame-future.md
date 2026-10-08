# Web/H5/Mini-game architecture: phác thảo tương lai

> **⚠ CHƯA KIỂM CHỨNG & CHƯA THIẾT KẾ CHI TIẾT.**
>
> **Mục đích:** Làm rõ tại sao EOS P2P không khả thi trên web, đề xuất đường đi dài hạn.
>
> Baseline: Kitchen Together (KT) commit `7d96acd5` (HEAD 2026-10-05).

---

## Tại sao không EOS P2P trên Web

**EOS SDK không có binding JavaScript/WebGL.** P2P API của Epic chỉ có:
- C SDK (máy desktop/mobile).
- Unity plugin (FishNet qua FishyEOS).
- Unreal plugin.

EOS SDK **không có bản cho nền tảng Web** (chỉ native: Windows/macOS/Linux/Android/iOS/console); trình duyệt cũng không cho mở socket UDP thô nên không port được lớp P2P của EOS.

---

## Ràng buộc mạng của nền tảng mini-game (CHƯA KIỂM CHỨNG)

Tra docs công khai ngày 2026-10-05, chưa thử trên tài khoản dev thật. Phải đọc lại docs chính thức trước khi chốt thiết kế.

| Nền tảng | Kênh mạng realtime | Ràng buộc đã thấy trong docs | Nguồn |
|---|---|---|---|
| WeChat mini-game | `wx.connectSocket` (chỉ `wss://`). `wx.createUDPSocket` có từ base library 2.7.0 | Domain giao tiếp phải khai báo trước trong console (allow-list), chỉ nhận https/wss. Tối đa **5** WebSocket cùng lúc (từ 1.7.0; trước đó là 1) | [network guide](https://developers.weixin.qq.com/minigame/en/dev/guide/base-ability/network.html), [wx.connectSocket](https://developers.weixin.qq.com/minigame/en/dev/api/network/websocket/wx.connectSocket.html), [wx.createUDPSocket](https://developers.weixin.qq.com/minigame/en/dev/api/network/udp/wx.createUDPSocket.html) |
| TikTok mini-game | `TTMinis.game.connectSocket` (SDK ≥0.4.0) | Mọi request chỉ được tới domain đã đăng ký trong allow-list; domain ngoài danh sách bị chặn. URL WebSocket không ghi port. Số domain và số socket tối đa: chưa thấy | [Mini Games SDK overview](https://developers.tiktok.com/docs/en/mini-games-sdk-overview), [WebSocket](https://developers.tiktok.com/doc/mini-games-sdk-websocket), [troubleshooting](https://developers.tiktok.com/docs/en/mini-games-development-troubleshooting) |
| Zalo Mini App | WebSocket của webview | App chạy dưới origin `https://h5.zdn.vn` (và `zbrowser://h5.zdn.vn`), nên server phải whitelist/CORS cho origin đó. Chưa thấy docs nói về giới hạn socket hay UDP | [Zalo community: whitelist domain](https://miniapp.zaloplatforms.com/community/4855799520141856207/ho-tro-ve-whitelist-domain-o-zalo-mini-app) |

Hệ quả cho thiết kế:
1. Relay server cần **domain cố định có TLS hợp lệ** (`wss://`), khai báo vào allow-list của từng nền tảng. Không dùng được IP trần, port tuỳ ý, hay endpoint đổi theo phòng.
2. Gộp mọi kênh (lobby + gameplay) vào **1 WebSocket** mỗi client; đừng thiết kế mỗi phòng một socket.
3. UDP và WebRTC data channel thì **tuỳ nền tảng** (WeChat có UDP socket; các nền tảng khác chưa xác nhận). Baseline chỉ dựa vào WebSocket qua TCP/TLS. Head-of-line blocking làm pose giật khi mất gói, nên ưu tiên game có TTK dài hoặc không cần đồng bộ vị trí chặt.
4. Thay đổi domain/allow-list thường phải sửa trong console của nền tảng (và có thể phải duyệt lại); đưa bước này vào checklist release.

---

## Đề xuất: Lobby + Relay server tự vận hành

### Mô hình

```
Web client (TypeScript/Phaser/Cocos Creator Web)
    |
    ├─ WebSocket → Server game (Node.js / Python)
    |                ├─ Lobby management (room create/join, attributes)
    |                └─ Relay server (WebSocket hoặc WebRTC SFU)
    |
    ├─ WebRTC data channel (optional, low-latency)
    │   └─ coturn STUN/TURN relay (nếu dùng P2P browser)
```

### Giao thức phòng (tái dùng)

Giữ nguyên `02-room-protocol.md`:
- State machine: Idle → Ready → Entering → Lobby → Starting → Loading → Playing → Returning.
- Attribute schema: tên key KT/Core (`LobbyKeys` trong `assets/unity/Core/LobbyProtocol.cs`), key giao thức mới thêm vào `LobbyKeys` cùng kiểu tên (vd `proto_major`), key game prefix `game_` (`02` §2.3) (giống `native-eos-design.md` §3).
- Entry: CreateLobby, JoinByCode, JoinById, Random.
- Barrier: start gate, load gate (per-member `load_epoch`), return gate.

**Server role:** EOS Lobby → **server game WebSocket**, synchronous, push (không polling).

### Mode: Server-authoritative (khuyến nghị cho Web)

Khác với native (host-authoritative):
- **Host:** Server (không bị tấn công, restart không mất state).
- **Client:** Browser; input → server; render output.
- **Authority:** Server quyết định hành động, replication, spawn.

**Fit well:** Turn-based, ranked (easy cheat-prevention), tower defense, hoặc realtime nhưng TTK lâu (tránh client prediction).

### Bandwidth & region

Ước tính (CHƯA ĐO):
- Realtime action 30 Hz: pose (input) ~2 KB/s → server; state broadcast ~5 KB/s → clients (hoặc WebRTC relay). **Ước tính, CHƯA ĐO.**
- 4 người × 4 người = 16 connections; server-auth không khuyếch đại như P2P.
- **Region:** Dùng VPS gần user (VN → SG/HK VPS <100ms là OK cho realtime casual). **Ước tính, CHƯA ĐO** — đo RTT thật từ mạng di động VN.

---

## Scope không bao gồm (hiện tại)

- ❌ RTC voice trên web (yêu cầu SFU / LiveKit / mediasoup — chi phí riêng).
- ❌ P2P browser (WebRTC data channel + coturn) — phức tạp, relay tốt hơn cho casual.
- ❌ Hybrid web+native — mô hình khác, chưa define.
- ❌ Backend service design chi tiết — team phải cung cấp.

---

## Validation gates (khi dự án Web bắt đầu)

1. **Server Lobby đơn giản:** Node.js + in-memory store; 2 browser create/join/ready.
2. **Relay latency:** WebSocket broadcast; 4 browser ping-pong, RTT <150ms.
3. **Authority:** Server kiểm input; chỉ server broadcast accept/reject.
4. **Persistence:** Game logic lưu result trên DB (không tin client submission).

---

**Kết luận:** Web ≠ native. Cơ chế khác (server-auth), infra khác (VPS relay), scope khác (chưa design). Dự án này tập trung native → Cocos `native-eos-design.md`. Web là backlog sau.
