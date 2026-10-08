# Unity — replication gameplay với FishNet (host-authoritative, host = người chơi)

Mục đích: các pattern đồng bộ gameplay đã chạy thật trên FishNet 4.7.2 + FishyEOS qua relay EOS (RTT 150–350 ms), rút từ mọi NetworkBehaviour của KT, kèm danh sách gotcha FishNet.
Baseline: KT commit 7d96acd5 (HEAD 2026-10-05)
Đối tượng: dev viết gameplay mạng trên stack ở `stack-and-setup.md`; reviewer kiểm code replication.

Nhãn: **[GENERIC]** · **[UNITY]** (FishNet/Unity) · **[KT-SPECIFIC]** · **CHƯA KIỂM CHỨNG**. Bug ID → `../04-bug-catalog.md`.

---

## 1. Mô hình authority [GENERIC]

- **Host là server + client** trong cùng process (FishNet host mode). Không có dedicated server.
- **Di chuyển người chơi: client-authoritative** (owner tự mô phỏng, đẩy transform lên). Lý do: relay EOS thêm 150–350 ms; host-auth movement sẽ trễ cảm nhận được.
- **Mọi thứ khác: host-authoritative.** Client chỉ gửi *ý định* (`[ServerRpc]`), server kiểm tra rồi ghi state (`SyncVar`/`SyncList`) hoặc phát event (`[ObserversRpc]`).
- **Vật lý chỉ chạy trên server.** Client giữ Rigidbody kinematic ở mọi state và tự vẽ từ state replicate (`KT:Assets/Scripts/Gameplay/Objects/KitchenObject.cs:46-47`).
- **Cosmetic tách khỏi gameplay**: hiệu ứng/âm thanh đi bằng ObserversRpc `BufferLast = false` (người vào sau không cần replay), state gameplay đi bằng SyncVar (người vào sau nhận cùng payload spawn).

## 2. Bảng NetworkBehaviour của KT

| NetworkBehaviour (KT) | Primitive / RPC | Pattern |
|---|---|---|
| `PlayerController` `KT:Assets/Scripts/Gameplay/Player/PlayerController.cs` | NetworkTransform client-auth trên prefab; `InteractServerRpc` `:780`, `ThrowServerRpc` `:836`, `ChopServerRpc` `:987`, `EmoteServerRpc` `:901` → `EmoteObserversRpc(ExcludeOwner)` `:917`; cue `DashCue*` `:1296/1306`; `[TargetRpc]` teleport/slip `:1023/1066` | P1, P2, P8 |
| `PlayerSkinSync` `KT:Assets/Scripts/Gameplay/Player/PlayerSkinSync.cs` | 3 `SyncVar<string>` `:19-22`; `ServerRpc(RequireOwnership = true)` `:107,121` | P6 |
| `KitchenObject` `KT:Assets/Scripts/Gameplay/Objects/KitchenObject.cs` | `SyncVar` ObjectType/State/HolderId/HolderSlot/RestPosition/RestRotation `:66-92`, `SyncList<string> Ingredients` `:70`; `ThrowObserversRpc` `:997`; `CarryDispenseHopObserversRpc` `:385` | P3, P4 |
| `KitchenObjectManager` `KT:Assets/Scripts/Gameplay/Objects/KitchenObjectManager.cs` | ~30 method `[Server]`, 1 ObserversRpc `:535`; dictionary chỉ có trên server (`_playerHeld`...) | P7 |
| `CounterBase` + `BaseCounter`, `CuttingCounter`, `StoveCounter` (+`OvenCounter`, `NoiComCounter`), `SinkCounter`, `FoodSpawnCounter`, `DeliverCounter`, `TrashCounter`, `ConveyerCounter` `KT:Assets/Scripts/Gameplay/Counters/` | `[Server] ServerInteract/ServerSetObject`; progress là `SyncVar` (vd `CuttingCounter.cs:29-31`, `StoveCounter.cs:20-22`, `SinkCounter.cs:27-30`); cue là ObserversRpc `BufferLast = false` | P7, G-08 |
| `RoundController` (partial + `.Uia.cs`) `KT:Assets/Scripts/Gameplay/Orders/RoundController.cs` | `SyncVar` TimeRemaining/MatchActive/CountdownSecondsLeft/epoch platform/kết quả `:129-192`; start gate coroutine `:326` | §5 |
| `OrderService` `KT:Assets/Scripts/Gameplay/Orders/OrderService.cs` | `SyncList<OrderState> Orders` `:59`, `_orderRevision` `:62`, `_orderIntegrity` `:64`; `RequestFullOrderSyncServerRpc(RequireOwnership=false)` `:787` → `ReceiveFullOrderSyncTargetRpc` `:819` | P5 |
| `ScoreService` `KT:Assets/Scripts/Gameplay/Orders/ScoreService.cs` | `SyncVar<int>` Blue/Red `:16-17`, ghi qua `[Server]` | P7 |
| `ConnectionProbe` `KT:Assets/Scripts/Net/ConnectionProbe.cs` | scene object; `SendProbeServerRpc(RequireOwnership=false)` `:68` → `AckProbeTargetRpc` `:75` | probe liveness (G-10) |
| `MatchReturnCoordinator` `KT:Assets/Scripts/Net/MatchReturnCoordinator.cs` | scene object; 2 ObserversRpc host→client, 1 ServerRpc ACK `:44-58`; chỉ chuyển message, logic quorum ở chỗ khác | message bus mỏng |

`OrderService` + `ScoreService` + `RoundController` dùng chung **một** NetworkObject ("MatchManager"): FishNet init mọi NB của một object trước khi gọi bất kỳ `OnStartServer` nào, nên lệnh `[Server]` chéo giữa chúng chắc chắn chạy được (`RoundController.cs:498-502`). Tách ra nhiều NetworkObject ⇒ có thể âm thầm không chạy (G-05).

## 3. Pattern

### P1. Movement client-auth bằng NetworkTransform [UNITY]

`KT:Assets/Prefabs/Player.prefab:215-255` (NetworkTransform trên root player):

| Field | Giá trị | Ý nghĩa |
|---|---|---|
| `_clientAuthoritative` | 1 (`:241`) | owner đẩy transform |
| `_sendToOwner` | 1 (`:242`) | |
| `_interval` | 1 (`:243`) | gửi mỗi tick (30 Hz) |
| `_interpolation` / `_extrapolation` | 2 / 2 (`:237-238`) | |
| `_packing` | Position 1, Rotation 1, Scale 0 (`:232-235`) | |
| `_synchronizeScale` | 1 (`:255`) | |
| NetworkObject `_enablePrediction` | 0 (`:174`) | không dùng prediction của FishNet |

Remote player: tắt CharacterController để không đánh nhau với NetworkTransform (`PlayerController.cs:314-317`). Hệ quả: **server không có collider cho người chơi remote** (G-09).

### P2. Hành động host-auth: ServerRpc có kiểm tra [GENERIC]

`PlayerController.cs:780-855`. Mỗi ServerRpc hành động:
1. **Rate-limit** theo thời gian server (`KitchenGameplayMath.AcceptsActionRpc`, tối thiểu 0,08s — `KT:Assets/Scripts/Gameplay/KitchenGameplayMath.cs:856-858`).
2. **Gửi identity, không gửi kết quả raycast**: client gửi `NetworkObject` mục tiêu; server kiểm mục tiêu có đăng ký (registry instance, không phải id→first-match) và **tầm với** so với pose server (`:801-808`). Không raycast lại trên pose server đã trễ.
3. **Chặn NaN/Inf + clamp**: `IsFiniteVector` (`KitchenGameplayMath.cs:18-23`); origin ném chỉ được lệch tối đa 4 m so với tay phía server (`ThrowOriginMaxLeadMetres`, `:15`) và trong bán kính thân; direction chuẩn hoá, rác ⇒ dùng `transform.forward` (`PlayerController.cs:837-854`).
4. Handler **không được ném exception** (G-03).

### P3. Đồ cầm trên tay không có NetworkTransform [GENERIC]

`KitchenObject.cs:30-47` (doc lớp — "do NOT reintroduce a NetworkTransform"). Player là "hiện tại" (owner dự đoán), transform stream từ server là "quá khứ" 150–350 ms ⇒ dán vật quá khứ vào tay hiện tại làm đồ trôi/giật. Thay bằng state:
- **Đang cầm**: `SyncVar<int> HolderId` = ObjectId của player (−1 = không ai) + `HolderSlot`; mỗi peer tự dán vật vào HoldPoint của player đó mỗi `LateUpdate`. Không có traffic vị trí.
- **Nằm yên** (trên counter/sàn): `RestPosition`/`RestRotation` server ghi một lần; peer snap một lần.
- **Một `State` duy nhất** quyết định toàn bộ vật lý (collider/kinematic) trên mọi peer (`:72-76`); không bật/tắt từng thuộc tính riêng.
- Thứ tự ghi: đổi `State` **trước** khi xoá `HolderId` để không có cửa sổ "Carried + HolderId=−1" (`KitchenObjectManager.cs:859-861`).

### P4. Ném = một event + owner dự đoán [GENERIC]

Server chạy Rigidbody thật cho gameplay (bắt, rơi) và phát **một** `ThrowObserversRpc(origin, initialVelocity)` (`KitchenObject.cs:971`, `:997-1010`). Mỗi client tự tích phân đường đạn. Owner đã bay từ lúc bấm (`OwnerPredictThrow`, `:979`) nên handler bỏ qua khi `IsServerInitialized || _localFlight`. Mượt ở mọi độ trễ, chỉ tốn một packet.

### P5. SyncList + revision/hash tự chữa, client đọc bản mirror [GENERIC]

`OrderService.cs:59-69`, `:725-735`, `:787-830`; tài liệu `KT:docs/order-sync/doc.md`.
- Server tăng `_orderRevision` một lần mỗi mutation và tính `_orderIntegrity` = hash ổn định của toàn bộ tập order.
- Client so revision/hash/count của bản mình đang hiển thị; lệch ⇒ `RequestFullOrderSyncServerRpc` (cooldown phía client **và** phía server theo `sender.ClientId`; bỏ qua host-local client). Server trả snapshot **chỉ cho người hỏi** bằng `TargetRpc`.
- Client **không sửa** SyncList của FishNet; nó giữ `_clientOrders` riêng, HUD đọc bản đó; server/host đọc `Orders` trực tiếp (G-08).
- TargetRpc có thể tới **trước** SyncVar ghi sau trong cùng frame server ⇒ không cho bản replicate có revision cũ hơn đè snapshot mới (`:731-733`).
- HUD reconcile theo `orderId` ổn định, không theo index (catalog D7).

### P6. Skin/cosmetic: SyncVar + giấu model tới khi có giá trị [GENERIC]

`PlayerSkinSync.cs:40-147`. Owner đọc lựa chọn local, `ServerRpc(RequireOwnership = true)` ghi SyncVar, và tự áp ngay (dự đoán). Remote player chưa có SkinId ⇒ **giấu model mặc định** (`:70-79`) để không lóe sai nhân vật; `OnChange` hiện lại khi giá trị tới. `OnStartClient` áp giá trị hiện tại thủ công vì OnChange có thể đã bắn trước khi đăng ký (`:81-89`). Server validate cosmetic theo catalog trước khi ghi (`:108-114`). Giấu model sinh ra bug G5: lookup chỉ tìm object active ⇒ NRE trong handler ⇒ bị kick (G-03).

### P7. Logic chỉ server: `[Server]` + state server-only [UNITY]

`KitchenObjectManager`, `CounterBase`, `ScoreService`, `RoundController.StartMatch`: method `[Server]`, dictionary chiếm chỗ chỉ tồn tại trên server. Trong ObserversRpc **không** đọc property do server gán (vd `CounterBase.CurrentObject`) — trên client nó null nên hiệu ứng chỉ hiện ở host; resolve bằng ObjectId truyền trong RPC (`KT:Assets/Scripts/Gameplay/Counters/CounterBase.cs:107-125`).

### P8. Server dịch chuyển owner bằng TargetRpc [UNITY]

Vì movement là client-auth, server không set transform người chơi. Nó gửi `[TargetRpc]` tới owner, owner tắt CharacterController, đặt vị trí, bật lại (`PlayerController.cs:1013-1033`: swap vị trí 2 người).

## 4. Spawn

### 4.1 Spawn service thay PlayerSpawner [GENERIC ý tưởng, UNITY code]

`KT:Assets/Scripts/Net/PlayerSpawnService.cs`. `PlayerSpawner` của FishNet spawn theo sự kiện **connect**; nhưng session/transport sống qua đổi scene (rematch), client đã connect không connect lại ⇒ vào trận lần 2 bếp trống trong khi timer vẫn chạy (`:17-22`). Thay bằng:
- Service **nằm trong scene gameplay**, server-only, chờ `ServerManager.Started` rồi **poll mỗi 0,5s** (`:129-136`).
- Mỗi connection chỉ được spawn khi **load barrier** báo peer đã load đúng trận hiện tại (`session.HasClientLoadedCurrentMatch(clientId)`), không phải khi connect (`:189-200`; bug D1). Despawn vẫn theo sự kiện disconnect (`:244-253`).
- **Alarm starvation 10s** (`BarrierStarvationSeconds`, `:91`, `:256-268`): client đã connect mà bị barrier chặn quá 10s ⇒ một `LogError` duy nhất (lỗi id space C1 trước đây im lặng).
- Mảng spawn point non-empty nhưng toàn null (map đang rebuild) ⇒ **chờ**, không spawn ở gốc toạ độ (`:141-154`; G3).
- Solo/multi phân loại theo **mode phòng**, không theo "có phòng hay không" (`:156-178`; G1). [KT-SPECIFIC]: solo trên map chia đôi spawn 2 nhân vật cho một connection.

### 4.2 Observer condition cùng scope với barrier [UNITY]

`KT:Assets/Scripts/Net/MatchLoadObserverCondition.cs:19-49`: remote peer **không thấy NetworkObject nào** tới khi báo đã load trận hiện tại; host-local client luôn true; solo luôn true. Kiểu `Timed` vì cờ "đã load" tới qua attribute lobby EOS, không kích rebuild observer. Thiếu nó: host stream object scene mới khi client còn đang load ⇒ `SceneId not found in SceneObjects`, client mất đồ vĩnh viễn (D2). Đặt làm `_defaultConditions` trên **mọi** NetworkManager.

### 4.3 Slot theo join order [GENERIC]

Slot spawn = `join_order` mà host publish (host 0, người vào sau 1...), không dùng cursor tăng dần; không resolve được ⇒ chờ + alarm (`PlayerSpawnService.cs:283-331`, `KT:Assets/Scripts/Net/CoopSpawnPolicy.cs`; G4).

## 5. Cổng bắt đầu trận [GENERIC]

`RoundController.StartWhenPartyConnected` (`RoundController.cs:326-427`): server giữ trận chưa bắt đầu tới khi **mọi thành viên đã ghim** (danh sách PUID chụp lúc start) vừa có connection FishNet vừa báo đã load; quorum phải ổn định 0,5s (`FishNetSettleSeconds`); một lần đọc roster thiếu không coi là rời phòng trừ khi xác nhận ≥1s (`RosterShrinkConfirmationSeconds`); hết 20s chỉ log + thử tiếp, **không bao giờ** tự cho phép chơi (`PartyConnectTimeoutSeconds`, `:313-324`). Logic quyết định nằm trong class thuần `MatchStartBarrierProgress` (`KT:Assets/Scripts/Gameplay/Orders/MatchStartReadinessPolicy.cs:47`) để test EditMode.

Đếm ngược: server ghi `CountdownSecondsLeft` mỗi frame; client vẽ thẳng từ giá trị đó, không tự tính theo đồng hồ tick ước lượng (đồng hồ của client mới connect còn đang trượt ⇒ đếm sớm rồi nhảy lùi) (`RoundController.cs:132-139`; D6).

## 6. Load scene [UNITY]

- KT **không** dùng SceneManager của FishNet. Mỗi peer tự load bằng Unity `SceneManager.LoadSceneAsync(..., Single)` (`KT:Assets/Scripts/Core/SceneRouter.cs:272`); NetworkObject đặt sẵn trong scene khớp nhau qua **sceneId** (G-11).
- Hook đến scene gameplay `SceneRouter.OnSceneLoaded` (`SceneRouter.cs:76-83`): (1) `EnsureMatchConnectionForScene()` — dựng kết nối FishNet cho đúng luồng (co-load lobby, hoặc host solo), (2) `MarkMatchLoaded()` — publish epoch đã load (mở barrier spawn/observer), (3) dọn UI shell. Đánh dấu "đã load" ở *nơi chứng minh scene đã tồn tại*, không ở chỗ gọi load.

## 7. Solo = cùng code qua loopback [GENERIC]

Solo/tutorial chạy host mode trên Tugboat 127.0.0.1, maxClients 1 (`KT:Assets/Scripts/Net/SessionOrchestrator.cs:507-556`). Gameplay không biết mình đang solo hay online: chỉ cần server + local client. Không có nhánh "offline" riêng để rò bug.

## 8. Gotcha FishNet (đã gặp hoặc đã thấy trong nguồn)

| # | Gotcha | Hậu quả | Cách làm | Nguồn |
|---|---|---|---|---|
| G-01 [UNITY] | Codegen bắt SyncType (`SyncVar`/`SyncList`) là `readonly` (hoặc `[AllowMutableSyncType]`), khởi tạo `= new()` | Lỗi codegen khi build | `public readonly SyncVar<T> X = new();` không bao giờ gán lại field | `KT:Assets/FishNet/CodeGenerating/Processing/SyncTypeProcessor.cs:264-283` |
| G-02 [UNITY] | Prefab mạng spawn runtime phải có trong `DefaultPrefabObjects`; KT tắt auto-refresh | Spawn thất bại trên client | Thêm prefab vào asset và commit; scene object không cần (dùng sceneId) | `KT:Assets/Scenes/Boot.unity:415-416`; `ConnectionProbe.cs:16-18` |
| G-03 [UNITY] | Exception trong lúc server xử lý packet của client (kể cả thân ServerRpc) ⇒ server **kick** connection (`KickReason.MalformedData`) | Guest bị đá ngay khi vào trận, toast "room closed" (ca model đang giấu ⇒ lookup NRE) | Handler RPC validate + try/catch, lookup gồm cả object inactive | `KT:Assets/FishNet/Runtime/Managing/Server/ServerManager.cs:868-870`; catalog G5 (37e1c692) |
| G-04 [UNITY] | Host mode: `OnChange` có thể bắn **2 lần** (asServer + asClient) cho một lần ghi | Animation chạy lại giữa chừng, hiệu ứng nhân đôi | Handler idempotent; visual bỏ nhánh `asServer && !IsClientStarted` | `KitchenObject.cs:396-398`; `PlayerSkinSync.cs:127-131` |
| G-05 [UNITY] | Ghi SyncVar **cùng giá trị** không bắn `OnChange` (vd enum mặc định 0 = trạng thái đầu) | Hiệu ứng chuyển trạng thái không chạy | Phát ObserversRpc kèm tham số cho event, hoặc chọn giá trị mặc định không trùng state thật | `KitchenObject.cs:372-389` |
| G-06 [UNITY] | `[Server]` trên NB chưa server-initialized = **không chạy**, chỉ log Warning (mặc định `Logging = Warning`) | Logic quan trọng âm thầm không chạy (vd StartGeneration) | Gom các NB gọi chéo `[Server]` vào cùng NetworkObject; tự check `IsServerInitialized` ở entry | `KT:Assets/FishNet/Runtime/Object/NetworkBehaviour/Attributes.cs:108-118`; `RoundController.cs:498-502` |
| G-07 [UNITY] | Thứ tự tới của SyncVar/RPC không đảm bảo như thứ tự ghi trên server (TargetRpc có thể tới trước SyncVar cùng frame; SyncVar có thể chưa tới lúc `OnStartClient`) | Snapshot mới bị bản cũ đè; HUD seed từ đồng hồ 0 | Mang revision trong dữ liệu; `OnStartClient` áp giá trị hiện tại; đồ chưa có type ⇒ bỏ qua tới khi tới | `OrderService.cs:731-733`; `KitchenObject.cs:1089`; `PlayerSkinSync.cs:81-89` |
| G-08 [UNITY] | Client **không được** sửa SyncList | Delta sau fail guard index/count | Client giữ mirror riêng | `OrderService.cs:66-69` |
| G-09 [UNITY] | Server không có collider cho player remote (CharacterController remote tắt; movement client-auth) | Raycast/physics phía server với người chơi khác sai | Kiểm tầm bằng khoảng cách tới identity; KT chỉ thêm capsule kinematic riêng cho chế độ cần va chạm | `PlayerController.cs:314-317`, `:1246-1250` |
| G-10 [UNITY] | `Started`/`IsStarted` vẫn `true` trên link P2P đã chết (app mobile bị suspend) | Dùng lại link chết sau resume | Probe khứ hồi chủ động (`ConnectionProbe`); thiếu bằng chứng = unknown, không phải dead | `ConnectionProbe.cs:10-15`; `KT:Assets/Scripts/Net/PersistentPeerReuse.cs:29`; catalog E2/E3 |
| G-11 [UNITY] | NetworkObject trong scene phải có **sceneId**; object tạo bằng tay/script import không tự có | FishNet deactivate object thiếu id (tutorial lên 4/32 object, không counter nào) | Sau khi tạo object bằng script chạy `NetworkObject.CreateSceneId` (reflection, như tool reserialize của FishNet) rồi save scene; test đếm | commit 61e31903 (`KT:Assets/Scenes/LevelTutorial.unity`); `KT:Assets/Scripts/Editor/Content/ReserializeLevelSceneNetworkObjects.cs:25-50` |
| G-12 [UNITY] | Editor **tự thêm `NetworkObject`** khi add/validate một NetworkBehaviour (`Reset`/`OnValidate` → `TryAddNetworkObject`) | Test tái hiện lỗi init-order luôn xanh trên code hỏng | Không tin test EditMode cho lỗi init-order; verify bằng telemetry/thiết bị | `KT:Assets/FishNet/Runtime/Object/NetworkBehaviour/NetworkBehaviour.cs:182-216`; catalog H6 |
| G-13 [UNITY] | `NetworkBehaviour.IsServerStarted` đọc trong `OnEnable` trước khi network init ⇒ NRE | NRE ở 59,8% session production | Hỏi `NetworkObject` trước | catalog E6 (1b3789a1) |
| G-14 [UNITY] | NetworkManager thứ hai trong scene sau bị huỷ (`DestroyNewest`) — chỉ chạy khi mở scene không qua Boot | Wiring khác nhau giữa các bản sao chỉ lộ khi test trực tiếp level | Một prefab NetworkManager chuẩn | `stack-and-setup.md` §3.1 |
| G-15 [GENERIC] | Static không reset khi tắt domain reload / state tĩnh per-process | Bug chỉ xuất hiện ở trận 2+ hoặc lần Play 2+ | `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` reset mọi static (vd `PlayerController.cs:1236`); test ≥2 trận liên tiếp | catalog F3, H4 |

## 9. Checklist review code replication

- [ ] Movement client-auth; mọi hành động khác là ServerRpc có rate-limit + kiểm identity/tầm với + chặn NaN.
- [ ] Không handler RPC nào có thể ném exception.
- [ ] Không NetworkTransform cho đồ được cầm/ném; dùng HolderId + RestPosition + event ném.
- [ ] State list quan trọng có revision + hash + snapshot targeted; client không sửa SyncList.
- [ ] Cosmetic: SyncVar + `OnStartClient` áp giá trị hiện tại; giấu model mặc định tới khi có giá trị.
- [ ] OnChange idempotent (host mode bắn 2 lần; cùng giá trị không bắn).
- [ ] Không dùng PlayerSpawner khi session sống qua scene; spawn theo load barrier + alarm.
- [ ] ObserverManager có match-load condition trên mọi NetworkManager.
- [ ] Mọi scene NetworkObject có sceneId; mọi prefab spawn có trong DefaultPrefabObjects.
- [ ] Start gate đợi quorum đã ghim; đếm ngược đọc từ giá trị server.
