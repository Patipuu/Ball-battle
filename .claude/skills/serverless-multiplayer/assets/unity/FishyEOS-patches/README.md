# Bộ vá FishyEOS (FishNet transport cho EOS P2P)

Bộ vá này chứa các sửa đổi Kitchen Together (KT) đã làm trên FishyEOS để chạy host-authoritative P2P qua EOS ổn định: reconnect, đổi NAT relay↔direct, rejoin sau crash, Multipass, Epic dev auth.

## Nguồn gốc (upstream)

- Repo: https://github.com/ETdoFresh/FishyEOS (layout `FishNet/Plugins/FishyEOS/`)
- Commit gốc đã chọn: `1be0b553fb083129a6bcebc7a9ffed8256c186a5` (2025-10-27, "Merge pull request #30 …"). Cây FishyEOS ở commit này giống hệt commit cha `588de88a` ("Fix Max Packet Size reference in GetMTU method"); `package.json` ghi version `0.0.6`.
- Cách chọn: so cây FishyEOS ở mọi commit upstream (82 commit, mọi nhánh) với cây lúc KT import (`c987f3f4`). Bỏ qua `.meta`, không phân biệt CR/LF, đếm số dòng khác. `1be0b553` cho diff nhỏ nhất (64 dòng); chỉ khác ở `ServerPeer.cs` và `EOS.cs`.
- License upstream: **MIT**, "Copyright (c) 2022 ETdoFresh" (file `license.txt` trong repo upstream). Khi phân phối lại bản vá hoặc `patched/`, phải giữ nguyên thông báo bản quyền MIT đó.
- File upstream **không** có trong bộ này: `package.json`, `Samples~/` (lobby mẫu) và mọi `.meta`. Unity tự sinh `.meta` khi copy vào dự án.

## Cấu trúc

| File | Nội dung |
|---|---|
| `00-import-vs-upstream.diff` | upstream `1be0b553` → bản KT import (`c987f3f4`) |
| `01-persistent-peer-stable-clientid.diff` | KT `8afdcf93` |
| `02-app-lifecycle-client-reconnect.diff` | KT `ad210e52` (chỉ phần FishyEOS) |
| `03-host-early-drop-detection.diff` | KT `e55547ef` |
| `04-guard-get-connection-address.diff` | KT `86d32739` |
| `05-per-player-dev-auth.diff` | KT `b511a1d2` (chỉ phần FishyEOS) |
| `patched/FishyEOS/` | Source đã vá hoàn chỉnh (KT HEAD `7d96acd5`): .cs + .asmdef + readme.md |

Mọi path trong diff đã chuẩn hoá về dạng `a/FishyEOS/...` / `b/FishyEOS/...`. Chỉ gồm .cs/.asmdef/.md, không có .meta. Danh sách commit lấy từ `git log -- Assets/FishNet/Plugins/FishyEOS`; sau `c987f3f4` không còn commit nào khác chạm FishyEOS.

## Cách dùng

**Cách 1 – thả vào dự án (khuyên dùng):** copy `patched/FishyEOS/` vào `Assets/FishNet/Plugins/FishyEOS/`. Cần FishNet và EOS Plugin for Unity 6.x (bản vá EOS.cs gọi `Init` theo API 6.x).

**Cách 2 – vá từ upstream** (để review, hoặc để rebase lên một upstream mới hơn):

```bash
git clone https://github.com/ETdoFresh/FishyEOS fishyeos-up
git -C fishyeos-up checkout 1be0b553fb083129a6bcebc7a9ffed8256c186a5
mkdir work && cp -R fishyeos-up/FishNet/Plugins/FishyEOS work/
cd work && git init -q .
for p in /path/to/FishyEOS-patches/0*.diff; do git apply --whitespace=nowarn "$p" || break; done
```

Phải apply đúng thứ tự `00 → 01 → 02 → 03 → 04 → 05`, vì 01–03 vá chồng lên cùng vùng code của `ServerPeer.cs`.

**Đã kiểm chứng (2026-10-05):** apply chuỗi trên lên bản sao upstream `1be0b553` (đã lọc .cs/.asmdef/.md, bỏ Samples~/package.json): cả 6 patch apply sạch, và `diff -r` giữa kết quả với `patched/FishyEOS` **rỗng**. Mới kiểm chứng ở mức text. Hành vi runtime đã được verify trên thiết bị trong KT, theo commit message của từng commit (editor host + Android).

## 00 – Bản import so với upstream

KT import FishyEOS trong commit `c987f3f4` và sửa ngay 2 file:

1. **`Util/EOS.cs` – tương thích EOS Plugin 6.x.** Ở cả 2 chỗ khởi tạo (nhánh build thiết bị, `!UNITY_EDITOR && !UNITY_STANDALONE_WIN`), lệnh `EOSManager.Instance?.Init(_eosManager, EOSPackageInfo.ConfigFileName)` đổi thành `Init(_eosManager)`. Lý do: plugin 6.x đã bỏ `EOSPackageInfo.ConfigFileName` và `Init()` tự tìm file config. Không sửa thì build thiết bị không compile được.
2. **`Core/ServerPeer.cs` – đường rejoin:**
   - `OnPeerConnectionClosed` gọi thêm `P2P.CloseConnection(local, remote, socket)`. Nếu không đóng, EOS giữ session nội bộ; khi cùng PUID quay lại, EOS "ignores incoming invitation and resends existing session" và rejoin không bao giờ established. Đây là **đường rejoin chuẩn** (catalog **C7**).
   - Thêm `EvictStaleConnections` vào `OnPeerConnectionRequest`: đóng mọi connection cũ của PUID trước khi accept. **Patch 01 đã gỡ phần này** vì nó bắn cả khi peer đang sống đổi NAT (xem C3).

Lưu ý: guard "người gửi lạ" trong `ServerPeer.IterateIncoming` (`if (!hasFoundId) return;`) **không phải bản vá KT**. Nó có sẵn ở upstream từ commit `7568606` (2023-06-24) nên không xuất hiện trong diff 00. Guard này `return` khỏi cả vòng lặp, nên các gói còn lại trong frame đó sẽ đọc ở tick sau. KT giữ nguyên hành vi này.

## Bảng bản vá

| # | Triệu chứng (khi chưa vá) | Cơ chế sửa | Bất biến | KT commit | Catalog |
|---|---|---|---|---|---|
| 00 | Build thiết bị không compile với EOS Plugin 6.x; peer rơi không vào lại được | `Init(_eosManager)`; `CloseConnection` trong `OnPeerConnectionClosed` | Peer đóng ⇒ session EOS P2P cũng đóng | `c987f3f4` | C7 |
| 01 | Client "không thấy nhân vật mình" giữa trận dù mạng ổn | Map `PUID → connection id` ổn định (`_clientIdByRemoteUser`). PUID đã có trong `_clients` gửi request trùng (NAT relay↔direct) thì **bỏ qua**. Gỡ `EvictStaleConnections`. Map bị xoá khi `StopConnection` | Một PUID giữ một connection id trong suốt phiên host; peer chỉ rời qua `OnPeerConnectionClosed` | `8afdcf93` | C3 |
| 02 | Client reconnect bị host từ chối vì slot cũ (zombie) còn giữ | `_establishedRemoteUsers`: chỉ bảo vệ peer **đã established**. Kết nối đã accept nhưng chưa established thì `EvictClientConnection` (gỡ notify, `CloseConnection`) rồi nhận request mới. Accept lỗi thì rollback handle establish-notify. Thêm log ACCEPT/EVICT/CLOSED | Chỉ peer established mới được bảo vệ khỏi request trùng (C3 vẫn giữ); zombie không bao giờ chặn reconnect | `ad210e52` | C4, E3 |
| 03 | Rejoin sau crash/hard-kill phải chờ ~30s | Mỗi peer established đăng ký `AddNotifyPeerConnectionInterrupted`. Interrupt (~6s) → grace 3s (`WaitForSecondsRealtime`) → đóng chủ động **vô điều kiện**. Teardown gom vào `TearDownEstablishedClient`, dùng chung với Closed. `StopConnection` dọn handle và coroutine | Teardown đi qua một đường duy nhất và gỡ mọi handle; slot được giải phóng trong ~9s | `e55547ef` | C5 |
| 04 | Host spawn chính mình bị NRE, cả 2 người không spawn (khi bọc trong Multipass) | `GetConnectionAddress`: `Connection` là struct nên `FirstOrDefault` trả default với `RemoteUserId == null`. Lookup miss giờ trả `""`, không ném exception | Code trong coroutine spawn không được ném exception | `86d32739` | C2 |
| 05 | Epic dev auth: Connect lỗi `UserLoginInfo.DisplayName ... must not be set for this credential type` | `ConnectLogin`: credential `Epic`/`EpicIdToken` thì không set `UserLoginInfo`; provider khác (DeviceID…) giữ nguyên | Không gửi `UserLoginInfo` với credential Epic | `b511a1d2` | A6 |

Chi tiết:

- **01:** request trùng của peer đang sống là chuyện bình thường khi EOS chuyển relay↔direct. Mint id mới hoặc force-close ở đây sẽ làm mồ côi NetworkObject mà peer đó sở hữu. KT verify: rejoin sau hard-kill dùng lại id 1 (trước đó 1→2).
- **02:** khoảng trước established cố ý **không** được bảo vệ. Lúc đó peer chưa sở hữu object nào; tệ nhất là phải re-dial. Đo trong KT: bg + 40s mất mạng → reconnect ~12s, cùng clientId.
- **03:** đóng vô điều kiện là thiết kế có chủ đích. Interrupt kéo dài tới mức này thì watchdog liveness phía client đã tự reconnect rồi. Nếu Closed đến trong lúc grace thì proactive close thành no-op. Thời gian reconnect đầu-cuối vẫn bị giới hạn ~15s bởi việc EOS SDK re-init phía client.

## Cảnh báo: `ServerPeer._latestId` là `static` (catalog C1, H4)

`private static int _latestId = 1;` dùng chung cho cả process, trong khi map `PUID → id` (`_clientIdByRemoteUser`) là của từng instance. Bộ vá **không** đổi điều này. Với Multipass (`[0]=Tugboat loopback, [1]=FishyEOS`), FishNet clientId nằm trong **id space của Multipass**. Gọi thẳng API per-connection của FishyEOS chỉ khớp ở phiên host đầu tiên của process; từ trận thứ 2 trở đi, PUID của peer đọc ra rỗng, barrier không mở và P2 không bao giờ spawn ("chỉ thấy host trong map").

KT sửa ở commit `80b6c8f4`, **nằm ngoài FishyEOS** nên không có trong bộ vá này: mọi lookup address/kick/stats theo connection đều đi qua top-level transport (`NetworkManager.TransportManager.Transport`), vì Multipass dịch id sang sub-transport sở hữu nó. Kèm theo là alarm: client đã connect mà bị chặn spawn >10s thì LogError. Dự án dùng bộ vá này **phải** làm theo quy tắc đó. Test bắt buộc chạy ≥2 trận liên tiếp trong cùng một process; nếu Editor tắt domain reload thì phải nhớ static không tự reset.

## Chưa sửa trong FishyEOS (liên quan)

- **C8 – `UNKNOWN SOCKET`:** host bind listener connection-request **sau** khi guest đã dial thì EOS vứt lời mời và không gửi lại. Cách sửa là bind listener trước khi báo guest co-load. Việc này nằm ở tầng điều phối của game, không phải FishyEOS, và KT chưa sửa.
