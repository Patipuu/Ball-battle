# Unity — stack, cài đặt và wiring NetworkManager từ số 0

Mục đích: dựng stack FishNet + FishyEOS + PlayEveryWare EOS trên Unity 6 cho game co-op/PvP không server game (host = máy người chơi), đủ để wiring NetworkManager, boot EOS và dial mà không cần mở repo KT.
Baseline: KT commit 7d96acd5 (HEAD 2026-10-05)
Đối tượng: dev Unity dựng multiplayer lần đầu trên stack này; agent cài stack cho dự án mới.

Nhãn: **[GENERIC]** đúng với mọi engine/transport · **[UNITY]** riêng Unity/FishNet/FishyEOS/PEW · **[KT-SPECIFIC]** chỉ là lựa chọn của KT · **CHƯA KIỂM CHỨNG** = chưa đo trên thiết bị/nguồn.
`KT:path:line` = đường dẫn trong repo Kitchen Together Unity tại baseline. Bug ID (C1, D2...) trỏ `../04-bug-catalog.md`.

---

## 1. Phiên bản đã chạy thật [UNITY]

| Thành phần | Phiên bản | Nguồn |
|---|---|---|
| Unity Editor | 6000.5.1f1 | `KT:ProjectSettings/ProjectVersion.txt:1` |
| FishNet | 4.7.2 (vendor trong `Assets/FishNet/`, không qua UPM). Tải: release 4.7.2 tại https://github.com/FirstGearGames/FishNet/releases (hoặc Asset Store) — **phải đúng 4.7.2** | `KT:Assets/FishNet/package.json:3` |
| PlayEveryWare EOS plugin | 6.1.0, local tarball. Tải `com.playeveryware.eos-6.1.0.tgz` tại https://github.com/PlayEveryWare/eos_plugin_for_unity/releases | `KT:Packages/manifest.json:6` → `file:../LocalPackages/com.playeveryware.eos-6.1.0.tgz` |
| EOS SDK (nằm trong plugin) | 1.19.1.2 (CL53289219) | mô tả `package.json` của plugin trong `KT:Library/PackageCache/com.playeveryware.eos@*/package.json:11` |
| FishyEOS (transport FishNet ↔ EOS P2P) | upstream `ETdoFresh/FishyEOS@1be0b553` + 6 file patch: 1 diff import-vs-upstream (`00`) + 5 bản vá KT (`01`–`05`) | `KT:Assets/FishNet/Plugins/FishyEOS/`; `../../assets/unity/FishyEOS-patches/` |
| Tick rate | 30 Hz (mặc định FishNet, không có TimeManager trong scene) | `KT:Assets/FishNet/Runtime/Managing/Timing/TimeManager.cs:184` |

Đừng nâng cấp một mảnh đơn lẻ. FishyEOS đã bị vá theo đúng API FishNet 4.7.2 và PEW 6.1.0; nâng cấp phải diff lại bản vá.

## 2. Cài đặt

### 2.1 PlayEveryWare EOS 6.1.0 qua tarball [UNITY]

1. Tải tarball 6.1.0 từ https://github.com/PlayEveryWare/eos_plugin_for_unity/releases, đặt trong `LocalPackages/` (cạnh `Assets/`).
2. **Repack nếu cần**: tarball KT đang dùng có root `package/` và thêm 177 entry *thư mục* (`package/`, `package/Documentation~/`...) mà bản gốc (`.tgz.orig`, cũng root `package/`) không có. So sánh: `diff <(tar tzf X.tgz.orig | sort) <(tar tzf X.tgz | sort)`. Lý do chính xác phải repack (UPM từ chối/giải nén thiếu khi không có entry thư mục): **CHƯA KIỂM CHỨNG** — không có commit/doc ghi lại. Cách repack an toàn: giải nén, rồi `tar czf com.playeveryware.eos-6.1.0.tgz package` (tar tự ghi entry thư mục).
3. Thêm vào `Packages/manifest.json`:
   `"com.playeveryware.eos": "file:../LocalPackages/com.playeveryware.eos-6.1.0.tgz"`
4. Config EOS đặt ở `Assets/StreamingAssets/EOS/` (`eos_product_config.json` + `eos_<platform>_config.json`). Mẫu có placeholder: `../../assets/unity/config-templates/` (đọc README ở đó — client secret coi như công khai).

### 2.2 FishNet 4.7.2 [UNITY]

Tải FishNet **đúng** 4.7.2 (https://github.com/FirstGearGames/FishNet/releases hoặc Asset Store; bản khác ⇒ patch FishyEOS có thể lệch API) và import vào `Assets/FishNet/` (KT vendor nguyên cây, có `package.json` version 4.7.2). Không dùng PlayerSpawner của FishNet cho game có lobby/rematch (xem `gameplay-replication.md` §4).

### 2.3 FishyEOS vendor + vá [UNITY]

Copy FishyEOS vào `Assets/FishNet/Plugins/FishyEOS/` rồi áp 6 file patch (1 diff import-vs-upstream `00` + 5 bản vá KT `01`–`05`) theo `../../assets/unity/FishyEOS-patches/README.md`. Các lỗi bản vá xử lý: C2, C3, C4, C5, C7 và **A6** (patch `05`: credential Epic không gửi `UserLoginInfo` — chỉ cần khi dùng Dev Auth/Epic login) trong catalog. Một lỗi **không** được vá trong FishyEOS mà phải tránh ở code game: `ServerPeer._latestId` là `static` (`KT:Assets/FishNet/Plugins/FishyEOS/Core/ServerPeer.cs:18`) → xem §6 quy tắc 3.

### 2.4 Editor build fix Android [UNITY]

Copy `../../assets/unity/Editor/EOSAndroidGradlePatch.cs` vào một thư mục Editor. Không cần cấu hình. Chi tiết §8.

## 3. Wiring NetworkManager (Boot scene) [UNITY]

KT đặt **một** GameObject `NetworkManager` trong scene Boot (scene đầu tiên trong build), sống xuyên suốt nhờ DontDestroyOnLoad. Nguồn: `KT:Assets/Scenes/Boot.unity:333-493`. Tất cả component nằm trên **cùng** GameObject:

| Component | Field | Giá trị KT | Dòng | Ghi chú |
|---|---|---|---|---|
| NetworkManager | `_spawnablePrefabs` | asset `DefaultPrefabObjects` | `Boot.unity:415` | KT: `KT:Assets/DefaultPrefabObjects.asset` (51 prefab) |
| | `_refreshDefaultPrefabs` | 0 (tắt) | `:416` | Tắt auto-refresh: danh sách là artifact được commit, không tự sinh lúc runtime. Thêm prefab mạng mới ⇒ tự thêm vào asset và commit |
| | `_runInBackground` | 1 | `:417` | Host mobile không được đứng tick khi mất focus |
| | `_dontDestroyOnLoad` | 1 | `:418` | Session/transport sống qua đổi scene (rematch giữ phòng) |
| | `_persistence` | 0 = `DestroyNewest` | `:420` | NetworkManager thứ hai xuất hiện sau (vd trong level) sẽ tự huỷ (`KT:Assets/FishNet/Runtime/Managing/NetworkManager.cs:471`) |
| TransportManager | `Transport` | → Multipass | `:367` | **Không** trỏ thẳng FishyEOS |
| | `_maximumClientPacketSize` | 20480 | `:368` | |
| Multipass | `GlobalServerActions` | 1 | `:448` | **Dự án mới: giữ `1` (mặc định FishNet)** nhưng **không bao giờ** gọi `StartConnection(server)` không index (với `1` nó mở cả Tugboat lẫn FishyEOS; với `0` nó báo lỗi). Luôn gọi `Multipass.StartConnection(true, index)` — overload theo index không phụ thuộc cờ này (`KT:Assets/FishNet/Runtime/Transporting/Transports/Multipass/Multipass.cs:826-857`) |
| | `_transports[0]` | Tugboat | `:450` | Solo/tutorial qua loopback |
| | `_transports[1]` | FishyEOS | `:451` | Mọi trận online |
| Tugboat | `_port` | 7770 | `:470` | |
| | `_clientAddress` | `localhost` | `:472` | Runtime ép 127.0.0.1 / ::1 + maxClients=1 trước khi start solo: `KT:Assets/Scripts/Net/SessionOrchestrator.cs:548-556` |
| FishyEOS | `socketName` | `FishyEOS` | `:389` | Mọi peer phải cùng socket name |
| | `autoAuthenticate` | 0 | `:391` | Login do boot chain điều khiển (§5), không để transport tự login |
| | `authConnectData.loginCredentialType` | 3 = DeviceCode | `:393` | Mặc định của FishyEOS `AuthData` (`KT:Assets/FishNet/Plugins/FishyEOS/Util/AuthData.cs:16`) |
| | `authConnectData.externalCredentialType` | 10 = DeviceidAccessToken | `:394` | Đăng nhập Connect bằng DeviceId ẩn danh |
| | `automaticallyCreateDeviceId` / `...ConnectAccount` | 1 / 1 | `:398-399` | Lần đầu tự tạo DeviceId + tài khoản Connect |
| | `timeout` | 30 | `:401` | Timeout nội bộ FishyEOS; KT không dựa vào nó (§5) |
| | `remoteServerProductUserId` | rỗng | `:390` | Gán runtime trước mỗi dial (§7) |
| ObserverManager | `_defaultConditions` | `[MatchLoadObserverCondition]` | `:491-492` | Asset `KT:Assets/Settings/MatchLoadObserverCondition.asset`; code `KT:Assets/Scripts/Net/MatchLoadObserverCondition.cs`. Lý do: D2 |
| TimeManager | — | **không có** | (grep Boot.unity: 0 kết quả) | ⇒ tick 30 mặc định |

Thứ tự index Multipass là hợp đồng với code: `TugboatTransportIndex = 0`, `EosTransportIndex = 1` (`KT:Assets/Scripts/Net/TransportRoutingPolicy.cs:15,18`). KT kiểm khi khởi động và LogError nếu scene đảo thứ tự (`SessionOrchestrator.cs:358-377`). Template nên giữ check này.

### 3.1 Mọi bản sao NetworkManager phải wiring giống hệt [UNITY]

FishNet chỉ giữ NetworkManager đầu tiên (`DestroyNewest`), nên bản sao trong level chỉ chạy khi mở level **không qua Boot** (Play thẳng scene, test PlayMode, dev gym). Chính lúc đó mọi giả định về transport sai lặng lẽ.

**Bản sao lỗi trong KT — ĐỪNG CHÉP:**
- `KT:Assets/Prefabs/Level/LevelMatchShell.prefab:1456-1577`: NetworkManager có `TransportManager.Transport` → **FishyEOS trực tiếp** (`:1534`), **không có Multipass/Tugboat**, và còn gắn `FishNet.Component.Spawning.PlayerSpawner` (`:1554`). Hậu quả khi chạy thẳng level: solo đòi EOS, `Multipass` = null nên code fall back sang transport mặc định (`SessionOrchestrator.cs:360-364` chỉ Warning), PlayerSpawner spawn theo sự kiện connect song song với spawn service.
- `KT:Assets/Scenes/KitchenGym.unity:1594-1675`: cùng kiểu (TransportManager + FishyEOS + PlayerSpawner, không Multipass).

Quy tắc cho dự án mới: có đúng **một** prefab NetworkManager chuẩn (wiring như bảng trên), mọi scene/prefab cần NetworkManager đều đặt *instance của prefab đó*; thêm EditMode test đọc prefab/scene assert `Transport is Multipass`, `[0] is Tugboat`, `[1] is FishyEOS`, `_defaultConditions` chứa condition load. Bằng chứng ObserverManager phải có condition trên mọi bản sao: D2.

## 4. Thứ tự boot [UNITY]

```
Boot scene load
 └─ AppConfig.RunChainAsync (danh sách task tuần tự, có retry)      KT:Assets/Scripts/Core/AppConfig.cs:442-457
     1. Login backend riêng của game                                [KT-SPECIFIC]
     2. "Init EOS"  → SessionOrchestrator.InitEosAsync()            AppConfig.cs:451, 796-805
     3+. config, IAP...                                             [KT-SPECIFIC]
```

Vì sao EOS init nằm trong boot chain chứ không ở `Start()` của session [UNITY] (`SessionOrchestrator.cs:380-390`, `AppConfig.cs:787-792`): lệnh login FishyEOS đầu tiên cũng là thứ *tạo* EOS platform (`EOS.GetPlatformInterface()` AddComponent `EOSManager`, `KT:Assets/FishNet/Plugins/FishyEOS/Util/EOS.cs:35-46`); login gửi trong cùng nhịp đó không bao giờ có callback. Tách "bật platform" và "login" thành hai bước, có retry.

## 5. EOS init + login có giới hạn thời gian [GENERIC ý tưởng, UNITY code]

`KT:Assets/Scripts/Net/SessionOrchestrator.cs:391-489`. Mọi chờ đều dùng `Time.realtimeSinceStartup` (không bị `timeScale`/pause ảnh hưởng). Hằng số: `:138-140`.

1. **Chờ dependency ≤ 5s** (`DependencyWaitSeconds`, `:401-410`): room + transport đã resolve. Hết hạn ⇒ trả `false` (boot chain retry).
2. **Bật platform** (`EnsurePlatform`, `:413`, `:477-488`): bọc try/catch, exception = fail có lý do.
3. **Poll Connect interface ≤ 15s** (`PlatformTimeoutSeconds`, `:419-428`): `EOS.GetCachedConnectInterface() != null`.
4. **Chờ thêm 1 frame** (`:429-430`) để platform kịp tick trước khi login.
5. **Login, tự đo timeout 12s** (`LoginTimeoutSeconds`, `:436-450`): gọi `AuthConnectData.Connect(out login)` rồi poll `login.loginCallbackInfo`. Không `yield return Connect(...)` vì timeout của FishyEOS chạy trong coroutine trên chính EOSManager — không bắn được nếu login là thứ dựng manager đó.
6. Thành công ⇒ đọc `EOS.LocalProductUserId` làm danh tính người chơi (PUID). Mọi nhánh lỗi đi qua `FailEos` (`:470-475`): log + báo coordinator (intent đang chờ fail ngay, không treo) + trả `false`.

Dev/Editor: `DevelopmentEosAuth.TryResolve` (`:436`, `KT:Assets/Scripts/Net/DevelopmentEosAuth.cs:27`) cho phép đổi sang EOS Dev Auth Tool để có 2 PUID khác nhau trên một máy (bug H1); không bao giờ bật trên mobile/release.

## 6. Quy tắc Multipass [UNITY]

1. **Start server trên đúng một index**, không dùng vòng start toàn cục (mở cả hai listener): `_multipass.StartConnection(true, index)` (`SessionOrchestrator.cs:524-530`). Online host = index 1 (`StartHost`, `:493-501`); solo = index 0 sau khi ép loopback (`StartLocalHost`, `:507-514`).
2. **`SetClientTransport(index)` trước MỌI lần dial**, kể cả re-dial của watchdog reconnect (`StartClientOnTransport`, `:538-542`; watchdog `:899-950`). Client transport của Multipass là state sticky trên object DDOL: một lần chơi solo trước đó để nó trỏ Tugboat ⇒ dial EOS sau đó âm thầm đi 127.0.0.1.
3. **Mọi API per-connection (address/PUID, kick, stats) đi qua `NetworkManager.TransportManager.Transport`** (Multipass), không bao giờ qua sub-transport (`AddressTransport`, `:1664-1676`). FishNet clientId nằm trong id space của Multipass; FishyEOS giữ `_latestId` static qua cả process trong khi map PUID là per-instance ⇒ hai id space chỉ trùng ở **phiên host đầu tiên** của process. Hậu quả đã gặp: trận thứ 2+ peer không bao giờ spawn (commit 80b6c8f4, bug C1).
4. Đọc trạng thái client cũng qua Multipass (`_multipass.GetConnectionState(false)`, `:925-929`), không qua biến FishyEOS.

## 7. Client dial [UNITY]

Trước khi dial phải biết PUID của owner (host) phòng — lấy từ lobby EOS:

```csharp
_transport.RemoteProductUserId = session.OwnerPuid.Value;          // SessionOrchestrator.cs:787
StartClientOnTransport(TransportRoutingPolicy.EosTransportIndex);  // :788
_establishRoutine = StartCoroutine(EnsureClientEstablished());     // :791
```

Owner thì không dial: start server + client local trên index 1 (`:777-784`). **Thời điểm start server — quy tắc dự án mới [GENERIC]:** KT chạy đoạn này khi scene match load ⇒ guest có thể dial trước khi listener P2P bind ⇒ `UNKNOWN SOCKET`, guest kẹt (bug C8, KT chưa sửa). Dự án mới: host gọi `StartServerOnTransport(EOS)` + client local **ngay khi sở hữu phòng** (sau create/owner xác nhận, kể cả khi được promote) và giữ peer sống qua lobby↔match, trước mọi tín hiệu begin/co-load (`../01-architecture.md` §4.3). CHƯA KIỂM CHỨNG trên thiết bị (KT chưa ship bản sửa). Retry-until-established (`:893-950`) [GENERIC]: kiểm mỗi 2s; chỉ re-dial khi transport `Stopped` hoặc một attempt treo ≥ 12s; bỏ cuộc sau 90s; không bao giờ cắt một connect đang handshake (cắt sớm khiến host bỏ qua request mới của PUID còn sống ⇒ deadlock rejoin). Re-dial tự gây `Stopped` — handler disconnect phải bỏ qua nó (bug E1).

## 8. Android [UNITY]

1. **Gradle patch** (`../../assets/unity/Editor/EOSAndroidGradlePatch.cs`, gốc `KT:Assets/Editor/EOSAndroidGradlePatch.cs`): Unity 6 dùng AGP 8 / Gradle 9; androidlib của PEW 6.1.0 vẫn kiểu cũ. Patch chạy sau khi Unity sinh Gradle project: encode dấu cách trong URI `file:`; bật core library desugaring cho module launcher (`eos-sdk.aar` đòi); bỏ `package=` trong manifest; xoá `buildscript{}`/`jcenter()`; thêm `namespace`; thay placeholder `-1`/`NOBUILDTOOLS` bằng `unity.compileSdkVersion`/`unity.targetSdkVersion`. Không sửa bản trong Assets — `AndroidBuilder.PreBuild` của plugin copy đè mỗi lần build.
2. **androidlib** `KT:Assets/Plugins/Android/EOS/eos_dependencies.androidlib/` (cộng `KT:Assets/Plugins/Android/aar/eos-sdk.aar`). Thư mục này **do plugin PEW sinh**: `AndroidBuilder.PreBuild` copy từ `PlatformSpecificAssets~/EOS/Android/` của package vào `Assets/Plugins/Android/EOS/` mỗi lần build (ghi đè), và copy aar vào `Assets/Plugins/Android/aar` (`KT:Library/PackageCache/com.playeveryware.eos@*/Editor/Platforms/Android/AndroidBuilder.cs:42-48,294-311,371-394`). Không tự tạo/sửa tay. Permission trong `AndroidManifest.xml:6-10`: `INTERNET`, `WRITE_EXTERNAL_STORAGE`, `DOWNLOAD_WITHOUT_NOTIFICATION`, `RECORD_AUDIO` (voice), `ACCESS_WIFI_STATE`. Manifest này giống hệt bản mẫu trong package PEW (đã diff) và bị ghi đè mỗi build ⇒ không có voice mà muốn bỏ `RECORD_AUDIO` thì làm ở manifest chính bằng `tools:node="remove"`, không sửa androidlib (CHƯA KIỂM CHỨNG trong KT); có voice ⇒ xin quyền muộn và dùng manual audio input khi chưa có quyền (bug F1).
3. **`res/values/eos_values.xml`**: một string `eos_login_protocol_scheme` = `eos.` + ClientId viết thường (dùng cho redirect login trình duyệt). File được copy cùng androidlib (bản mẫu trong package mang client id mẫu), rồi `ConfigureEOSDependentLibrary` tự ghi lại `eos.<client_id_lowercase>` từ ClientId của config Android nếu lệch (`KT:Library/PackageCache/com.playeveryware.eos@*/Editor/Platforms/Android/AndroidBuilder.cs:347-368`). Đổi client ⇒ chỉ cần đổi config; build xong kiểm giá trị trong file khớp.
4. `build.gradle` của androidlib trong Assets KT hiện ghi `compileSdkVersion 36` / `targetSdkVersion 36` (`KT:Assets/Plugins/Android/EOS/eos_dependencies.androidlib/build.gradle`); patch vẫn cần cho các bước khác.

## 9. iOS — CHƯA KIỂM CHỨNG

- Có `KT:Assets/StreamingAssets/EOS/eos_ios_config.json` (cùng cấu trúc Android, không có 2 field Google login).
- **Không** tìm thấy cấu hình plugin EOS riêng cho iOS dưới `KT:Assets/Plugins/iOS/` (chỉ có Firebase và 2 file `.mm` của KT). Chưa có bằng chứng KT đã build + chạy EOS P2P trên iOS.
- Việc cần làm trước khi hứa iOS: đọc `Documentation~/iOS/README_iOS.md` trong package PEW 6.1.0; build thử; kiểm login DeviceId, lobby, P2P relay trên máy thật; kiểm URL scheme login tương tự `eos_values.xml`.

## 10. Checklist dựng dự án mới

- [ ] Unity 6000.5.1f1 (hoặc ghi rõ bản khác + test lại toàn bộ).
- [ ] FishNet 4.7.2 trong `Assets/FishNet/`; FishyEOS vendor + áp đủ 6 file patch (`00` import-vs-upstream + 5 bản vá KT `01`–`05`) theo README patches.
- [ ] PEW EOS 6.1.0 qua `LocalPackages/*.tgz` (root `package/`, có entry thư mục); `manifest.json` trỏ `file:../LocalPackages/...`.
- [ ] Config EOS từ template, giá trị thật **không commit** nếu repo công khai; client policy least-privilege.
- [ ] Một prefab NetworkManager chuẩn: DDOL, runInBackground, DefaultPrefabObjects (auto-refresh off), TransportManager→Multipass[0 Tugboat 7770, 1 FishyEOS], ObserverManager có match-load condition, FishyEOS `autoAuthenticate=0`.
- [ ] Không có NetworkManager nào khác wiring khác (test EditMode assert).
- [ ] Boot chain: EOS init là một task có retry; dependency 5s / platform 15s / +1 frame / login 12s, đồng hồ realtime.
- [ ] Code transport: start theo index, `SetClientTransport` trước mọi dial, address qua top-level transport.
- [ ] Host start EOS server ngay khi sở hữu phòng, trước begin/co-load (§7, bug C8).
- [ ] Client dial: `RemoteProductUserId = owner PUID` rồi watchdog retry có trần.
- [ ] Android: copy `EOSAndroidGradlePatch.cs`; kiểm permission androidlib (PEW sinh lại mỗi build); sau build kiểm `eos_values.xml` khớp ClientId.
- [ ] iOS: đánh dấu CHƯA KIỂM CHỨNG cho tới khi chạy trên máy thật.
- [ ] Test ≥ 2 trận liên tiếp trong cùng một process (bắt lỗi id space C1) với 2 PUID khác nhau (H1).
