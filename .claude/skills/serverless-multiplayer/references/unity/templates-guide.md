# Unity templates guide

Mục đích: dùng code mẫu trong `assets/unity/` để dựng stack lobby/transport serverless (EOS Lobby + FishNet + FishyEOS) cho dự án Unity 6 mới, không cần mở repo gốc.

Chú giải nguồn: `KT:` là repo Kitchen Together Unity, commit baseline `7d96acd5`. Đường dẫn `KT:Assets/...` chỉ để đối chiếu, skill không phụ thuộc vào nó. Một số comment trong code còn nhắc "Godot": đó là dấu vết bản Godot gốc mà KT port sang, không liên quan đến dự án của bạn.

Trạng thái dùng trong bảng:
- **tests pass**: có test NUnit chạy thật (`dotnet test`, 113/113 pass), kèm compile.
- **compiled**: biên dịch 0 lỗi bằng Roslyn của Unity 6000.5.1f1, chưa có test nào chạy.
- **chưa runtime**: chưa chạy trong dự án mới. Với `Eos/` và `FishNetEos/`, thuật toán đã chạy trên thiết bị thật trong KT, bản template chỉ đổi kiểu/namespace/phụ thuộc.

Lệnh compile và log: `assets/unity/README.md`.

## 1. Namespace và assembly

| Thư mục | Namespace / asmdef | Ràng buộc |
|---|---|---|
| `Core/` | `TeamNet.Multiplayer.Core`, `noEngineReferences: true` | Thuần C#, không UnityEngine, test được bằng `dotnet test` |
| `Eos/` | `TeamNet.Multiplayer.Eos` | PEW EOS 6.1.0 + FishyEOS (đã vá) |
| `FishNetEos/` | `TeamNet.Multiplayer.FishNetEos` | FishNet 4.7.2 + FishyEOS |

Namespace là `TeamNet.Multiplayer.FishNetEos` / `.EditorTools` (không dùng `.FishNet` / `.Editor`) để tránh che namespace `FishNet` và `UnityEditor.Editor` — quyết định validation 2026-10-05.

## 2. Bảng file

### Core (tests pass, trừ ghi chú)

| File | Mục đích | Game phải cung cấp | Nguồn KT | Trạng thái |
|---|---|---|---|---|
| `GenerationGuard.cs` | Token thế hệ cho thao tác lobby async; kết quả trả về muộn (sau teardown, đổi scene, supersede) bị bỏ | Chỉ cần log (`Action<string>`) | `KT:Assets/Scripts/Net/GenerationGuard.cs` | tests pass |
| `LobbyConnectWatchdog.cs` | Đua thao tác EOS không hủy được với watchdog; thắng timeout thì giao operation còn chạy cho caller dọn "phòng mồ côi" | Task watchdog (vd `Task.Delay`) | `.../LobbyConnectWatchdog.cs` | tests pass |
| `LobbyProtocol.cs` | `LobbyKeys` (khóa giao thức), `ILobbyKeySchema`, `FormerMembers` codec (sổ cái PUID có đóng dấu epoch), `JoinOrderMapping` codec, `LobbyBudget` | Schema khóa riêng của game | `.../LobbyAttributes.cs` (tách phần codec) | tests pass |
| `ILobbyService.cs` | Seam lobby: `LobbyResult`, `LobbyConfig`, `LobbyMemberSnapshot`, `ILobbyService` | Không (đã có `EosLobbyService`) | `.../ILobbyService.cs` | tests pass (qua fake) |
| `EpochPhaseCoordinator.cs` | `MATCH_EPOCH`/`LOAD_EPOCH`/`ROOM_PHASE`/`CLIENT_PHASE`; barrier "mọi thành viên đã load" lọc theo sổ cái và liveness | Predicate `isParticipantLive` (chỉ host có) | `.../EpochPhaseCoordinator.cs` | tests pass |
| `RejoinAdmission.cs` | Host quyết định giữ/đuổi peer vừa connect giữa trận; mọi bất định nghiêng về Allow | Dữ liệu: puid, sổ cái, epoch, cờ started, grace | `.../RejoinAdmission.cs` | tests pass |
| `MatchReturnQuorum.cs` + `ClientReturnFallback.cs` | Barrier "mọi peer sẵn sàng về lobby" phía host (timeout đơn điệu) và fallback tự về phía client | Đồng hồ monotonic `Func<double>` (`Time.realtimeSinceStartupAsDouble`) | `.../MatchReturnQuorum.cs`, `ClientReturnFallback.cs` | tests pass |
| `PostMatchPartyPolicy.cs` | Sau trận: Wait/Keep/Leave; đọc rỗng của EOS là "chưa biết", không phải "đã rời" | `inLobby`, số member, version lần poll | `.../PostMatchPartyPolicy.cs` | compiled |
| `RoomCodeGenerator.cs` | Mã phòng 6 ký tự, bỏ ký tự dễ nhầm (0/O, 1/I/L) | Không | `.../RoomCodeGenerator.cs` | tests pass |
| `RandomMatchPolicy.cs` | Lọc ứng viên random match: đầy, đã bắt đầu, sai mode, sai bundle. Mode là tham số `wantedMode`, không có enum game | Mode label và bundle của game | `.../RandomMatchPolicy.cs` | tests pass |
| `PersistentPeerReuse.cs` | Giữ hay dựng lại peer FishNet giữa các trận; client chỉ reuse khi probe sống | `IP2PBoundary` (dùng `EosP2PBoundary`) | `.../PersistentPeerReuse.cs` | tests pass |
| `AppLifecycleRecovery.cs` | Sau resume app: probe chủ động rồi chọn ReuseAndResync / ClientRebuild / HostReturnToLobby | `IP2PBoundary`, delay `Func<TimeSpan,Task>` | `.../AppLifecycleRecovery.cs` | tests pass |
| `IP2PBoundary.cs` | Seam transport + enum `P2PProbeResult` (Alive/Dead/Unavailable) | Không | `.../IP2PBoundary.cs` | tests pass (qua fake) |
| `PlayerId.cs`, `RoomVisibility.cs` | PUID dạng string, độc lập native handle; `Advertised` / `HiddenJoinableById` | Không | `.../PlayerId.cs`, `RoomVisibility.cs` | compiled |
| `GameSideInterfaces.cs` | `IRoomHistoryStore` (lưu room id để rejoin), `IDiagnosticsSink` | Cài đặt trong game (PlayerPrefs/save, Debug.Log/telemetry) | `KT:Assets/Scripts/Save/*` (không đưa vào) | compiled |

### Eos (compiled, chưa runtime)

| File | Mục đích | Game phải cung cấp | Nguồn KT |
|---|---|---|---|
| `EosLobbyService.cs` | `ILobbyService` trên EOS Lobby: create / join id / join random / join code / leave, deferred-leave, lease, kick, ghi/đọc thuộc tính. Giữ nguyên thuật toán lease/fence/watchdog | `GenerationGuard`, `ILobbyKeySchema` (tùy chọn), `ILobbyRtcHook` (tùy chọn), bucket id trong `LobbyConfig` | `KT:Assets/Scripts/Net/EosLobbyService.cs` |
| `EosLobbyOperationOwnership.cs` | Sổ ownership tĩnh: một PUID/lobby không được vào lại khi leave cũ chưa xong; lease cho mutation bù trừ | Không (internal) | `.../EosLobbyOperationOwnership.cs` |
| `LobbyLeaseTypes.cs` | `IDeferredLobbyLeaveService`, `ILobbyMembershipLeaseService` và các token. Chuyển từ `ILobbyService.cs` sang Eos, đổi `internal` thành `public` vì giờ session owner nằm assembly khác | Session owner gọi qua interface | `.../ILobbyService.cs` |
| `EosLifecycle.cs` | Reset tĩnh ở `SubsystemRegistration` + `RegisterActiveGuard`; xem mục 3.4 | Gọi `RegisterActiveGuard`, và Invalidate + `RevokeLifecycleCertificate` khi teardown | `KT:Assets/Scripts/Net/ProductLobbySession.cs` (`ResetStatics`), `SessionOrchestrator.cs` (`ResetEosLifecycleOwner`) |
| `EosBootstrap.cs` | Boot task Init EOS + đăng nhập Connect: chờ dependency (5s), tạo platform, poll Connect interface (15s), chờ 1 frame, resolve `DevelopmentEosAuth`, login với timeout realtime riêng (12s); `EnsureConnectedAsync(forceRefresh)` chỉ tái dùng PUID khi `GetLoginStatus == LoggedIn`, forceRefresh = logout best-effort (5s) rồi login lại; `RunWithReauthOnceAsync` thử lại đúng một lần với InvalidAuth/AuthExpired. Lỗi báo qua event `AuthFailed` để coordinator fail intent đang xếp hàng. Timing cấu hình qua `EosBootstrapConfig` (mặc định = KT) | `Func<AuthData>` (displayName), `dependenciesReady` (tùy chọn), nối `AuthFailed`/`Authenticated` vào room coordinator | `KT:Assets/Scripts/Net/SessionOrchestrator.cs` `InitEosAsync` (~384-488), `ProductLobbySession.cs` `EnsureEosConnectAsync` (~2516-2632) | compiled, chưa runtime (không có test: cần EOS thật) |
| `ILobbyRtcHook.cs` | Móc voice: thay các lệnh gọi trực tiếp tới module voice của KT | Cài đặt nếu có voice, không thì truyền `null` | mới (thay `Voice/*` của KT) |
| `DevelopmentEosAuth.cs` | Dev Auth Tool nhiều người chơi local; fail-closed khi endpoint/label sai, không bao giờ rơi về DeviceID. Chỉ build trong Editor và standalone dev build | Đặt `IsVirtualPlayerProvider`, tên arg, prefix EditorPrefs | `.../DevelopmentEosAuth.cs` |

### Wiring test (compiled, chưa chạy trên prefab thật)

| File | Mục đích | Game phải cung cấp | Nguồn KT | Trạng thái |
|---|---|---|---|---|
| `Tests/Wiring/NetworkManagerWiringTests.cs` + asmdef | Test EditMode abstract: nạp prefab NetworkManager và khẳng định `TransportManager.Transport` là Multipass; Multipass[0]=Tugboat, [1]=FishyEOS; FishyEOS `autoAuthenticate` tắt; `ObserverManager` default conditions chứa `MatchLoadObserverCondition`; không có FishNet `PlayerSpawner`. Đọc `_defaultConditions` (private `[SerializeField]`) bằng reflection, báo lỗi rõ nếu FishNet đổi tên | Một lớp con khai báo `NetworkManagerPrefabPath` (và tùy chọn `TugboatIndex`/`FishyEosIndex`) | Đối chiếu API với `KT:Assets/FishNet/Runtime/**`; KT không có test tương đương | compiled, chưa chạy trên prefab thật |

Chọn test thay vì Editor validator vì chạy được trong CI/`unity test` và gắn cổng với mọi lần đổi prefab; lớp trừu tượng để không fail khi dự án chưa chọn đường dẫn.

### FishNet (compiled, chưa runtime)

| File | Mục đích | Game phải cung cấp | Nguồn KT |
|---|---|---|---|
| `ConnectionProbe.cs` | Probe round-trip chủ động (ServerRpc + TargetRpc) bằng một NetworkObject scene trên mọi peer | Đặt 1 instance trong mỗi scene gameplay | `.../ConnectionProbe.cs` |
| `EosP2PBoundary.cs` | `IP2PBoundary` thật. `ProbeAsync` trả tri-state, `ProbeAliveAsync` ánh xạ bool theo `UnavailableProbePolicy` | `NetworkManager`, `FishyEOS` | `.../EosP2PBoundary.cs` |
| `AddressTransport.cs` | Resolve clientId sang PUID qua top-level transport (Multipass) | PUID local của EOS (cho client của host) | `KT:Assets/Scripts/Net/SessionOrchestrator.cs:1655-1677` |
| `MatchLoadGate.cs` | `IMatchLoadGate` + holder tĩnh `MatchLoadGate.Provider`; một nguồn sự thật cho observer condition và spawner | Cài đặt trong session owner | mới (thay `SessionOrchestrator.Instance` trực tiếp) |
| `MatchLoadObserverCondition.cs` | ObserverCondition dạng Timed: peer không thấy object nào đến khi báo đã load epoch hiện tại | Tạo asset, gắn vào ObserverManager và prefab cần chặn | `.../MatchLoadObserverCondition.cs` |
| `PlayerSpawnServiceTemplate.cs` | Khung spawner trong scene gameplay: poll barrier, báo động starvation 10s, solo chỉ phục vụ client của host, despawn khi rời scene | Prefab player, `ISpawnSlotResolver`, `MatchLoadGate.Provider` | `.../PlayerSpawnService.cs` (đã gỡ phần PvP, custom map, replay ghost, split-kitchen) |
| `ISpawnSlotResolver.cs` | Game trả vị trí spawn của một connection; trả false khi chưa biết thì spawner chờ | Cài đặt | mới |

## 3. Chỗ sửa / thêm so với KT

Tất cả nằm trong `Eos/` và `Core/`, chưa chạy trên thiết bị. Không chép các điểm KT còn lỗi đánh dấu "đừng chép".

1. **Phòng private (quyết định: ẩn khỏi random match, vẫn vào được bằng mã/id).** KT chưa có phòng private. Template luôn tạo phòng `Publicadvertised` (nên join-by-code hoạt động) và dùng khóa phòng giao thức `LobbyKeys.Private` (`"private"`, giá trị `"1"`/`"0"` qua `LobbyKeys.PrivateValue`). `LobbyConfig.IsPublic = false` nghĩa là **session owner phải ghi `private=1` trong batch thuộc tính đầu tiên** của phòng (service không tự ghi vì thuộc tính phòng đi qua `SetAttributesAsync`). `RandomMatchPolicy.IsJoinable` loại phòng có `private=1` (lý do `"private"`), phòng chưa từng ghi khóa vẫn joinable. `SetRoomVisibilityAsync(Advertised)` sau trận trả quyền về `Publicadvertised`, cờ `private` vẫn còn nên random match vẫn bỏ qua. Không còn ánh xạ `IsPublic` sang `Joinviapresence`. Hạn chế: giữa lúc tạo phòng và lúc ghi `private=1` có một khoảng ngắn phòng hiện ra như phòng public; ghi ngay sau create.
2. **`mod.Release()` trên mọi đường thoát của `SetRoomVisibilityAsync`.** KT không release `LobbyModification` ở bất kỳ đường nào. Giờ release khi `SetPermissionLevel` thất bại và sau `UpdateLobby`; cùng nguyên tắc cho nhánh khóa không biết scope trong `SetAttributesAsync`.
3. **Release handle native `LobbyDetails` / `LobbySearch`.** KT **rò** các handle này (đừng chép): `TryGetDetails` không bao giờ release, `LobbySearch` không release ở mọi đường, và `LobbyDetails` của candidate random match không được chọn cũng không release. Template release trong `finally`: sau mỗi lần đọc (`GetMembers`, `TryGetMaxMembers`, `TryReadRoomAttribute`, `TryReadMemberAttribute`); search release khi `Find` xong hoặc khi thiết lập search thất bại; candidate bị bỏ release ngay, candidate còn lại release khi mọi lần thử join (kể cả retry already-exists) đã xong; details của join-by-id/code release khi `tcs` hoàn tất. Thứ tự và thuật toán join không đổi. **Khuyến nghị cho `IMatchLoadGate`**: `ConditionMet` của observer chạy theo object x connection mỗi tick, nên `HasClientLoadedCurrentMatch` phải đọc snapshot member cache một lần mỗi frame (hoặc theo revision), không bao giờ gọi `GetMembers()` mỗi lần.
4. **Lifecycle reset truy cập được.** `Eos/EosLifecycle.cs`: có `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` gọi `Reset()`. Reset invalidate guard đã đăng ký rồi xóa sổ ownership tĩnh (membership đã commit, entry reservation, cleanup debt, sàn operation, certificate). Giống `ProductLobbySession.ResetStatics` + `SessionOrchestrator.ResetEosLifecycleOwner` của KT. `EosLobbyService.RevokeLifecycleCertificate()` giờ `public`.
5. **Hook voice không thể làm kẹt thao tác.** `ILobbyRtcHook.OnLobbyEntered` được gọi SAU khi kết quả create/join đã được set, trong `try/catch` (exception chỉ ghi log).

Hành vi khác KT cần biết:
- Khóa thuộc tính lạ trong `SetAttributesAsync` giờ **trả false** (log rồi release handle). KT ném `KeyNotFoundException` vì tra từ điển trực tiếp.
- Chẩn đoán random match (skip, đếm phòng, retry) **im lặng khi `log == null`**. KT luôn ghi ra console Unity. Truyền một log (vd `IDiagnosticsSink.AsLogAction()`) nếu muốn thấy chúng.
- `LobbyMemberSnapshot.AttributesReplicated` (name + character đã có) bị bỏ. Thay bằng: game tự định nghĩa "đã replicate" trên các khóa của mình, `snapshot.Get(key) != null` với từng khóa trong `MemberSnapshotKeys`, và poll `GetMembers()` theo chu kỳ có giới hạn (so sánh revision/hash snapshot giữa các lần poll) thay cho `RosterReplicationWaiter` (không port).

## 4. Thay đổi thiết kế khi bóc khỏi KT

- **Khóa thuộc tính.** `LobbyKeys` chỉ còn khóa giao thức (`mode`, `bundle`, `is_room_started`, `room_phase`, `match_epoch`, `join_order_mapping`, `code`, `former_members`, `private`; member: `user_id`, `client_phase`, `load_epoch`). Khóa của game (tên, skin, level, ready, chat...) khai báo qua `ILobbyKeySchema`:
  - `TryGetScope(key, out scope)` cho biết khóa là Room hay Member. Khóa lạ thì ghi thất bại (fail closed), không đoán scope.
  - `MemberSnapshotKeys` là các khóa member cần chép vào `LobbyMemberSnapshot.Attributes` khi đọc roster.
- **`LobbyMemberSnapshot`** chỉ còn `Member`, `Uid`, `LoadEpoch`, `Attributes`. Trường roster của KT (tên, nhân vật, wearable, chat...) đi qua `Attributes`.
- **`LobbyConfig`** bỏ `GameMode`; sức chứa là tham số của `CreateAsync`, mode là `ModeLabel` string.
- **Voice.** KT gọi thẳng module voice trong `CreateAsync`/`TryJoin`. Giờ `ILobbyRtcHook` (null = không voice): quyết định có bật RTC room không, mic thủ công, bắt đầu mute, và nhận `OnLobbyEntered` (sau khi kết quả đã set, exception được nuốt) sau mỗi lần vào phòng để module voice tự lo recovery.
- **Log.** `UnityEngine.Debug` trong service đổi thành `Action<string> log`.
- **`DefaultBucketId`** đổi từ giá trị của KT thành `"default-lobby"`; hãy luôn truyền bucket riêng của game trong `LobbyConfig`.
- **Test seam** (`...ForTests` internal) còn trong `EosLobbyService` vì là một phần của thuật toán leave; chúng sinh 5 warning CS0649 vô hại.

## 5. Probe tri-state và ánh xạ legacy của KT

`EosP2PBoundary.ProbeAsync` trả `Alive`, `Dead` hoặc `Unavailable`. `Unavailable` nghĩa là không có `ConnectionProbe` trên peer này (probe chỉ có trong scene gameplay; ở Boot/Menu/Lobby/Results không có gì để hỏi).

KT legacy: probe thiếu được coi là **alive** ("không có bằng chứng chết"). Lý do đo trên thiết bị: coi "không có probe" là chết thì vòng reuse mỗi 4s dựng lại link khỏe suốt pha lobby, và cả trong cửa sổ rejoin (người rejoin chờ ở menu đến khi có scene trận), nên handshake (~2s) không bao giờ xong. Lobby chạy trên thuộc tính EOS chứ không trên link P2P nên không mất gì. Vào scene gameplay thì probe được spawn và sẽ bắt link chết.

`IP2PBoundary.ProbeAliveAsync` vẫn là bool để `PersistentPeerReuse` và `AppLifecycleRecovery` giữ nguyên thuật toán. `UnavailableProbePolicy.TreatAsAlive` (mặc định, giống KT) hoặc `TreatAsDead` (nghiêm ngặt) quyết định ánh xạ. Dự án có probe trong mọi scene dùng `TreatAsDead`.

## 6. Thứ tự tích hợp

1. Cài PEW EOS, FishNet, FishyEOS đã vá, wiring NetworkManager: `references/unity/stack-and-setup.md`. Áp 6 bản vá trong `assets/unity/FishyEOS-patches/`.
2. Copy `Core/`, `Eos/`, `FishNetEos/`; kiểm tra tên asmdef tham chiếu khớp.
   Copy thêm `Tests/Wiring/`, thêm lớp con khai báo đường dẫn prefab NetworkManager và chạy nó (EditMode) trước khi test thật; xem bảng "Wiring test".
3. Viết `ILobbyKeySchema` của game; đặt `LobbyConfig` (bucket, mode label, bundle).
4. Dựng session owner (mục 7) với `GenerationGuard`, `EosLobbyService`, `EpochPhaseCoordinator`, `EosP2PBoundary`.
   Boot pipeline gọi `EosBootstrap.InitAsync()` (retry khi false), nối `AuthFailed` vào coordinator để fail intent đang xếp hàng; trước mỗi lobby create/join gọi `EnsureConnectedAsync()`, và bọc thao tác trả InvalidAuth/AuthExpired bằng `RunWithReauthOnceAsync`.
5. Cài `IMatchLoadGate` và gán `MatchLoadGate.Provider`. Tạo asset `MatchLoadObserverCondition`, thêm vào ObserverManager.
6. Thêm `ConnectionProbe` vào mỗi scene gameplay; thêm `PlayerSpawnServiceTemplate` + `ISpawnSlotResolver`.
7. Nối `PersistentPeerReuse`, `AppLifecycleRecovery`, `MatchReturnQuorum`, `ClientReturnFallback`, `RejoinAdmission`.
8. Teardown/reset: gọi `EosLifecycle.RegisterActiveGuard(guard)` khi dựng session owner (để reset subsystem invalidate đúng guard). Khi hủy owner: `guard.Invalidate(reason)` rồi `lobby.RevokeLifecycleCertificate()`. `EosLifecycle` tự reset ledger tĩnh ở `SubsystemRegistration` (Play Mode tắt domain reload).
9. Chạy `dotnet test` cho Core (mục 9), rồi test thật bằng 2 thiết bị, ≥2 trận liên tiếp trong cùng một process.

## 7. Session owner (không đưa code, chỉ pseudo-code)

KT có hai lớp: `ProductLobbySession` (2.6k dòng, sở hữu lobby/roster/epoch) và `SessionOrchestrator` (2k dòng, sở hữu transport). Cả hai dính save, telemetry, UI, mode của KT nên không đưa vào. Nguyên tắc cần giữ: **một owner duy nhất** cho vòng đời phòng; mọi thao tác lobby đi qua `GenerationGuard`; UI và gameplay chỉ phát intent.

Quy tắc đọc trạng thái phòng (áp dụng cho mọi đoạn pseudo-code dưới đây): **poll là nguồn sự thật**. `AddNotify` của EOS (lobby update / member status) chỉ dùng để kích một lần đọc lại ngay, không bao giờ mang dữ liệu trong payload của nó. Mỗi lần poll đọc lại toàn bộ trạng thái cần quyết định, **gồm owner hiện tại** (`GetLobbyOwner`), không cache owner từ lần trước: owner đổi (host rời, migrate) phải được thấy ở lần đọc kế tiếp, vì host/client role và đích dial đều suy ra từ owner.

### Khi nào start host (CHƯA KIỂM CHỨNG trên thiết bị)

Quy tắc: host start EOS server (listener đã bind) **ngay khi sở hữu phòng**, tức ngay sau khi `CreateAsync` thành công và owner được xác nhận là mình, và **giữ nguyên qua lobby <-> match**. Không start theo `HostBeginMatch`, không đợi co-load hay tín hiệu begin.

Lý do (bug C8 "UNKNOWN SOCKET"): client dial theo owner PUID ngay khi vào phòng (và dial lại khi rejoin). Nếu host chỉ start lúc begin match thì lúc đó client đã gửi packet P2P tới một socket chưa listen; EOS từ chối với "UNKNOWN SOCKET" và client kẹt ở retry. KT hiện start host quá muộn nên dính lỗi này; template đi trước KT. Hệ quả cho code: `EnsureHostStarted()` idempotent, gọi ở `CreateRoom` và ở nhánh "tôi vừa trở thành owner" (host migrate), không gọi ở `HostBeginMatch`/`EndMatch`; teardown chỉ ở `LeaveRoom` hoặc `HostReturnToLobby` của lifecycle.

Pseudo-code, chưa biên dịch, mang tính phác thảo:

```text
class SessionOwner                    // 1 instance, sống qua các scene
  eos        = new EosBootstrap(() => new AuthData{displayName=me}, dependenciesReady, config, log)   // boot: InitAsync(); trước create/join: EnsureConnectedAsync()
  guard      = new GenerationGuard(log)
  lobby      = new EosLobbyService(guard, log, gameKeySchema, rtcHookOrNull)
  epoch      = new EpochPhaseCoordinator(lobby, log, isParticipantLive: host ? isClientLive : null)
  p2p        = new EosP2PBoundary(networkManager, fishyEos)
  reuse      = new PersistentPeerReuse(p2p, log)
  lifecycle  = new AppLifecycleRecovery(p2p, Task.Delay, log)

  CreateRoom(cfg):
    token = guard.BeginOperation("create")
    r = await LobbyConnectWatchdog.RunAsync(lobby.CreateAsync(me, max, cfg), Task.Delay(30s), onTimeout: orphan => cleanupWhenLate(orphan))
    if (!r.Ok || guard.IsStale(token, "create")) return fail
    roomHistory.SetLastRoomId(r.LobbyId)
    lobby.SetAttributesAsync(me, [code=RoomCodeGenerator.New(), mode, bundle, private=LobbyKeys.PrivateValue(!cfg.IsPublic)])   // ngay sau create
    EnsureHostStarted()                               // NGAY khi sở hữu phòng (xem "Khi nào start host" bên dưới), không đợi begin match

  EnsureHostStarted():                               // idempotent; host-only
    if (reuse.DecideHostReuse().Reuse == false) transport.StartHost()   // listener EOS bind xong trước mọi tín hiệu begin/co-load

  JoinRoom(by id | code | random):
    r = await watchdog(lobby.Join...Async(...))      // random thất bại kiểu RandomMatchPolicy.ShouldCreateAfterJoinFailure -> CreateRoom
    if (EpochPhaseCoordinator.IsMatchStarted(lobby)) route = rejoin-to-match else route = lobby-screen
    d = await reuse.DecideClientReuseAsync(lobby.OwnerPuid, 3s); if (!d.Reuse) transport.Dial(lobby.OwnerPuid)

  HostBeginMatch():                                  // host-only, thứ tự PRODUCT (references/02-room-protocol.md §6), KHÔNG dùng thứ tự gym
    guard.PrepareSceneTransition()
    next = epoch.GetCurrentMatchEpoch() + 1
    order = JoinOrderMapping.Compute(existing, readyMemberUids)           // host đứng đầu, người cũ giữ thứ tự
    await lobby.SetAttributesAsync(me, [join_order_mapping = Serialize(order),
                                        former_members = FormerMembers.Serialize(next, readyPuids)])   // 1 batch
    poll until replicated mapping == order            // fail -> ở lại lobby, epoch chưa mở
    recheck: context còn hiệu lực, epoch chưa đổi, đúng roster ready   // drift -> dừng, chưa begin
    ok = await epoch.BeginMatchAsync(me)              // epoch++, IN_MATCH, host LOAD_EPOCH=0 (1 batch)
    if (!ok) { re-read; if backend đã mở -> rollback epoch.ReturnToLobbyAsync + confirm closed tuple; else terminal }
    confirm open tuple (started=1, IN_MATCH, epoch==next) với timeout   // timeout -> terminal
    recheck lần cuối; drift -> rollback (ReturnToLobbyAsync + confirm 0/LOBBY/epoch==next), rollback không confirm -> terminal teardown
    lobby.SetRoomVisibilityAsync(me, HiddenJoinableById)   // BẮT BUỘC sau khi confirm open tuple: Joinviapresence, phòng đang trận không bị search thấy (join-by-id/rejoin vẫn vào được).
                                                          // KT product path chưa khóa, template đi trước KT; RandomMatchPolicy vẫn loại phòng started như lớp thứ hai. CHƯA KIỂM CHỨNG trên thiết bị.
                                                          // Khóa thất bại: log + retry có hạn, KHÔNG rollback trận (khóa chỉ là che khỏi search); phục hồi ở EndMatch.
    pin roster đã commit (process-local, sống qua scene unload); loadGameplayScene()   // KHÔNG StartHost ở đây: host đã chạy từ CreateRoom và giữ qua lobby<->match

  // IMatchLoadGate.IsBarrierActive: khóa theo membership lobby/ledger, KHÔNG theo số member.
  // IMatchLoadGate.HasClientLoadedCurrentMatch: đọc snapshot cache 1 lần/frame, không gọi GetMembers() mỗi lần.
  OnGameplaySceneArrived():                          // chứng minh scene thật sự có mặt, không phải lúc gọi load
    guard.CompleteSceneTransition()
    await epoch.MarkLocalMatchLoadedAsync(me)        // MatchLoadGate: HasClientLoadedCurrentMatch(clientId) = member(puid).LoadEpoch == epoch

  OnServerPeerConnected(conn):                       // host
    v = RejoinAdmission.Decide(puid(conn), ledger, epochNow, started, isHostConn, withinStartGrace)
    Reject -> disconnect; Unknown -> retry tick sau; Allow-mới -> FormerMembers.Append

  OnAppResume():
    switch await lifecycle.OnResumeAsync(isHost, host, settle, probeTimeout):
      ReuseAndResync / ClientRebuild / HostReturnToLobby

  EndMatch():
    q = new MatchReturnQuorum(realtimeClock); q.BeginSession(peerIds); ... if q.TryCommit(now, out why) => epoch.ReturnToLobbyAsync + SetRoomVisibilityAsync(Advertised)   // Advertised = Publicadvertised; cờ private giữ nguyên
    client: ClientReturnFallback.Arm(); Disarm khi host trả về

  LeaveRoom(): await lobby.LeaveAsync(me, id); roomHistory.ClearLastRoomId(); guard.BeginTeardown()
```

Chi tiết thật (race, thứ tự, các nhánh lỗi) xem bản KT: `KT:Assets/Scripts/Net/ProductLobbySession.cs`, `KT:Assets/Scripts/Net/SessionOrchestrator.cs`, `KT:Assets/Scripts/Net/RoomIntentCoordinator.cs`, `KT:Assets/Scripts/Net/RosterReplicationWaiter.cs`. Giải thích bất biến: `references/02-room-protocol.md`, `references/03-robustness-patterns.md`.

## 8. Test KT không port và lý do

Đã port: `GenerationGuardTests`, `FormerMembersTests`, `EpochPhaseCoordinatorTests`, `RejoinAdmissionTests`, `MatchReturnQuorumTests` (gồm `ClientReturnFallback`), `PersistentPeerReuseTests`, `AppLifecycleRecoveryTests`, `RoomCodeGeneratorTests`, `LobbyAttributesTests` (phần codec, scope, thêm test cho schema và snapshot), cùng `LobbyConnectWatchdogTests` và test `RandomMatchPolicy` (từ `KT:Assets/Scripts/Tests/EditMode/LobbyConnectWatchdogTests.cs`, `CoopSpawnAndRandomMatchPolicyTests.cs`; hai test hằng số timeout của `ProductLobbySession` bị bỏ). Thay đổi khi port: `NetSeamFakes.cs` (614 dòng, dính lease/leave/KT) được thay bằng `Tests/Core/CoreFakes.cs` gọn (`FakeLobbyService`, `FakeP2PBoundary`).

Không port:

| Test KT | Lý do |
|---|---|
| `ProductLobbyMembershipTests`, `ProductLobbyCapacityTests`, `ProductLobbyCustomMapAgreementTests` | Dựng trên `ProductLobbySession`; chúng cũng là nơi duy nhất KT test các thuật toán lease/fence/deferred-leave của `EosLobbyService` qua hook `...ForTests`. Hệ quả: **`Eos/` không có test trong skill**, chỉ có compile |
| `RoomIntentTests`, `RosterServiceTests`, `RosterReplicationWaiterTests`, `SeamContractTests` | Gắn vào roster/intent/seam riêng của KT, không có trong template |
| `LobbyMatchPolicyTests`, `MatchExitPolicyTests`, `UiaDayTransitionProtocolTests`, `InviteSessionTests`, `LobbyChatChannelTests` | Chính sách và tính năng riêng của KT (mode, thoát trận, ngày UIA, mời bạn, chat lobby) |
| `DevelopmentEosAuthTests` | Dùng `Save` của KT; có thể viết lại nhanh quanh `TryApply`/`ResolveEditorCredential` |
| Trong `LobbyAttributesTests`: test wearable | Dùng `RosterMember` của KT |

`PostMatchPartyPolicy`, `PlayerId`, `RoomVisibility` không có test được port (KT cũng không có test riêng cho nó). Nên bổ sung test cho `PostMatchPartyPolicy` trước khi dựa vào.

## 9. Chạy test Core

```bash
cd assets/unity/Tests/Core
dotnet test TeamNet.Multiplayer.Core.Tests.csproj --artifacts-path <scratch>/artifacts
# Passed! Failed: 0, Passed: 113, Skipped: 0, Total: 113  (net10.0)
```

Trong Unity: asmdef `TeamNet.Multiplayer.Core.Tests` (Editor, cần Test Framework). `.csproj` đặt `TargetFramework` là `net10.0`; đổi sang `net8.0` nếu máy chỉ có runtime 8.

## 10. Không nằm trong template (game tự làm)

Roster/slot theo join order, replication gameplay, chat lobby, UGC map, mời bạn, voice (chỉ có hook), UI. Replication gameplay: `references/unity/gameplay-replication.md`.
