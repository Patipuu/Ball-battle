# Cocos native + EOS: multiplayer host-authoritative qua EOS P2P

> **⚠ CHƯA KIỂM CHỨNG.** Chưa có dòng code Cocos nào chạy thật. Mọi tên API EOS bên dưới đã được đối chiếu với wrapper C# sinh 1:1 từ C header (và C header iOS đi kèm) của EOS SDK **1.19.1**. Hành vi runtime trên Cocos thì chưa đo.
>
> **Mục đích:** thiết kế cho Cocos2d-x / Cocos Creator native (Android/iOS), co-op/PvP realtime 2–4 người. Dùng **nguyên** giao thức phòng ở `02-room-protocol.md` và các bất biến ở `03`/`04`. Netcode host-authoritative tự viết, không dùng FishNet.
> **Baseline:** KT commit `7d96acd5` (HEAD 2026-10-05). **Người đọc:** kỹ sư Cocos dựng tầng mạng và reviewer.

**Quy ước trích dẫn.**
- `KT:` = repo Kitchen-Together-Unity. `FEOS:` = `KT:Assets/FishNet/Plugins/FishyEOS/` (bản đã vá).
- `PEW:` = `KT:Library/PackageCache/com.playeveryware.eos@ed5815c8bc3d/Runtime/EOS_SDK/` (wrapper C#; doc comment chép từ C header; tên C là `EOS_<Interface>_<Func>`). `PEWPKG:` = gốc của package đó. Trong đó có `Runtime/iOS/EOSSDK.framework/Headers/*.h` là C header thật, và `eos_version.h:8-12` ghi bản 1.19.1.
- `CC388:` = `/Volumes/FanxiangTBOS/SoftwareInstalled/Cocos/Creator/3.8.8/CocosCreator.app/Contents/Resources/resources/3d/engine/native/`.
- `SKILL:` = thư mục skill này. Docs Epic (`dev.epicgames.com`) render bằng JS nên fetch tự động trả về rỗng (đã thử 2026-10-05). Vì vậy nguồn chính là comment trong SDK. Chỗ nào chỉ dựa vào docs hoặc trí nhớ đều ghi CHƯA KIỂM CHỨNG.

---

## 1. Vòng đời EOS platform và hợp đồng thread

### 1.1 Hợp đồng tick/thread [GENERIC, có bằng chứng từ SDK]

| Sự thật | Nguồn |
|---|---|
| `EOS_Platform_Tick` "must be called frequently … usually once per-tick" | `PEW:Generated/Platform/PlatformInterface.cs:979-982` |
| Callback được phát **từ bên trong** `EOS_Platform_Tick`, **trên thread gọi Tick** | SDK nói rõ cho từng API: `PEW:Generated/PlayerDataStorage/PlayerDataStorageInterface.cs:391,441` ("All callbacks … come from the same thread that the SDK is ticked from"), `PEW:Generated/AntiCheatClient/OnMessageToServerCallback.cs:18`. Câu "mọi interface đều vậy" lấy từ docs Epic, CHƯA KIỂM CHỨNG trên trang docs vì không fetch được. FishyEOS chạy được chính nhờ giả định này (tick và callback cùng main thread Unity) |
| **Ngoại lệ SDK ghi rõ:** "may be called from a thread other than the one from which the SDK is ticking" | `PEW:Generated/RTCAudio/RTCAudioInterface.cs:166` (AudioBeforeRender), `:211` (AudioBeforeSend), `PEW:Generated/RTCData/RTCDataInterface.cs:47` (DataReceived) |
| SDK tự tạo thread nội bộ (NetworkWork, StorageIo, WebSocketIo, P2PIo, HttpRequestIo, RTCIo, CryptographyWork…); app chỉ chỉnh được affinity | `PEW:Generated/Platform/InitializeThreadAffinity.cs:13-58` |
| Gói P2P **không đến qua callback**: app tự rút bằng `EOS_P2P_GetNextReceivedPacketSize` + `EOS_P2P_ReceivePacket` | `PEW:Generated/P2P/P2PInterface.cs:515`; `PEW:Core/P2P/P2PInterface.cs:9-24` |

**Luật [HARD]:**
1. Chỉ **một** thread gọi mọi hàm EOS và `EOS_Platform_Tick`: game thread của Cocos. Với Creator 3.8: nghe `cc::events::BeforeTick`. Sự kiện này phát mỗi frame, ngay trước `_scheduler->update` (`CC388:cocos/engine/Engine.cpp:310`, khai báo ở `CC388:cocos/engine/EngineEvents.h:346`). Với cocos2d-x: `Director::getInstance()->getScheduler()->scheduleUpdate(target, INT_MIN, false)` — priority **số nhỏ nhất chạy trước** trong scheduler cocos2d-x, nên Tick EOS chạy trước logic game mỗi frame (CHƯA KIỂM CHỨNG trên bản 3.17/4.x cụ thể). Vòng đời app cocos2d-x: `AppDelegate::applicationDidEnterBackground()` → `SetApplicationStatus(BackgroundSuspended)`; `applicationWillEnterForeground()` → `SetApplicationStatus(Foreground)` **trước Tick kế tiếp**, rồi chạy `AppLifecycleRecovery` (§6).
2. Thứ tự trong một frame: `Tick` (callback lobby/connect/P2P-notify chạy ở bước này) → rút `ReceivePacket` cho tới khi gặp `NotFound` hoặc hết ngân sách gói/frame → logic mạng → gửi.
3. Callback chạy cùng thread nên **không cần hàng đợi xuyên thread** cho Connect/Lobby/P2P. Bản cũ của tài liệu này đề xuất `MutexQueue` dựa trên giả định sai. Chỉ khi bật RTC voice có xử lý audio buffer, hoặc dùng RTCData, mới cần hàng đợi lock-free, và chỉ cho đúng 3 notify ngoại lệ ở trên.
4. Không gọi `EOS_Platform_Tick` lồng trong callback. Không chặn game thread để chờ callback: callback chỉ tới ở lần Tick sau, nên chờ đồng bộ sẽ deadlock (giống bẫy H5 của Unity).
5. Với Creator, JS chạy trên cùng game thread. Vì vậy C++ resolve Promise ngay trong callback là an toàn; microtask chạy ở `mainLoopUpdate` trong cùng frame (`CC388:cocos/engine/Engine.cpp:310-318`).

### 1.2 Trình tự vòng đời

| Bước | Hàm C | Quy tắc | Nguồn |
|---|---|---|---|
| 0 (Android) | Java: nạp `libEOSSDK.so`, rồi gọi `com.epicgames.mobile.eossdk.EOSSDK.init(Activity)` | PEW gọi `EOSSDK.init(activity)` trước Initialize (`PEWPKG:Runtime/Android/Core/AndroidPlatformSpecifics.cs:67-85`). Hàm `init(android.app.Activity)` có trong `classes.jar` của `eos-sdk.aar` (kiểm bằng javap). Lớp Java này **không** tự gọi `System.loadLibrary`, nhưng có method `native auth(String)`. Vì vậy app phải tự `System.loadLibrary("EOSSDK")` trước (Unity làm việc này ngầm qua P/Invoke). Trên Cocos: **CHƯA KIỂM CHỨNG** | `PEWPKG:PlatformSpecificAssets~/EOS/Android/static-stdc++/aar/eos-sdk.aar` |
| 1 | `EOS_Initialize` | **Một lần mỗi process**; gọi lần 2 trả `EOS_AlreadyConfigured`. Phải có `EOS_Shutdown` đi kèm | `PEW:Generated/Platform/PlatformInterface.cs:140-155` |
| 1a (Android) | `EOS_Android_InitializeOptions` (ProductName, ProductVersion ≤64 byte ASCII, `Reserved=NULL`, `SystemInitializeOptions` → `{Reserved, OptionalInternalDirectory, OptionalExternalDirectory}`) | Hai thư mục "Can be null". PEW truyền struct rỗng (có TODO chưa set thư mục) và KT vẫn ship được, nên để null là chạy. Nếu muốn set thì dùng `Context.getFilesDir()` / `getExternalFilesDir(null)` (CHƯA KIỂM CHỨNG). Tên struct C suy ra từ comment `EOS_{System}_InitializeOptions` | `PEW:Generated/Android/Platform/AndroidInitializeOptions.cs:17-66`, `AndroidInitializeOptionsSystemInitializeOptions.cs:17-32`; `PEWPKG:Runtime/Android/Core/AndroidPlatformSpecifics.cs:89-104` |
| 2 | `EOS_Platform_Create(&Options)` | `ProductId, SandboxId, DeploymentId, ClientCredentials, bIsServer=false, Flags, TickBudgetInMilliseconds` (`05` §2.3). `EncryptionKey` **chỉ** dùng cho Player/Title Data Storage; không dùng storage thì để trống (`05` §3). Trả NULL ⇒ boot task báo fail và retry | `:192-201` |
| 3 | `EOS_Platform_Tick` mỗi frame | Xem §1.1 | `:979-982` |
| 4 (pause) | `EOS_Platform_SetApplicationStatus(EOS_AS_BackgroundSuspended)` | Gắn vào `BaseEngine::EngineStatusChange` `ON_PAUSE`/`ON_RESUME` (`CC388:cocos/engine/BaseEngine.h:38-49`, phát ở `Engine.cpp:352,372`) | `:907`; enum ở `PEW:Generated/Platform/ApplicationStatus.cs` |
| 5 (resume) | `SetApplicationStatus(EOS_AS_Foreground)` **trước lần Tick đầu tiên** | SDK: "must happen before Tick when foregrounding" | `PlatformInterface.cs:896` |
| 6 (mạng) | `EOS_Platform_SetNetworkStatus(EOS_NS_Online/Offline/Disabled)` | Lấy trạng thái từ `ConnectivityManager` (Android) / `NWPathMonitor` (iOS). Khi chuyển Offline→Online: **phải bind lại** `AddNotifyPeerConnectionRequest` và `…Established` | `:915-925`; `PEW:Generated/P2P/P2PInterface.cs:249,341` |
| 7 | `EOS_Platform_Release` → `EOS_Shutdown` | Release đúng 1 lần mỗi instance, làm trước Shutdown. Sau Shutdown cấm mọi lời gọi SDK. Trên mobile chỉ làm ở `ON_CLOSE`. Không Shutdown rồi Initialize lại trong cùng process (re-init sau Shutdown CHƯA KIỂM CHỨNG, nên coi như không hỗ trợ) | `:883-889`, `:988-998` |

Sau khi resume: chạy `AppLifecycleRecovery` (chờ settle 2s → probe → `ReuseAndResync` | `ClientRebuild` | `HostReturnToLobby`, xem `03` §10). Khi app ở nền, Cocos ngừng vòng tick nên EOS cũng ngừng tick, và link P2P có thể chết mà không báo (`04` E2/E3).

### 1.3 Build

| Mục | Nội dung |
|---|---|
| ABI Android | `eos-sdk.aar` đi kèm PEW (SDK 1.19.1) chỉ có `jni/arm64-v8a` và `jni/x86_64`. Vì vậy phải **bỏ armeabi-v7a** khỏi build Cocos (template Cocos cũ hay bật v7a). Kiểm lại với gói SDK sẽ pin |
| Android manifest/Gradle | aar khai báo `minSdkVersion 23`, quyền INTERNET, activity `EOSAuthHandlerActivity`, và dùng androidx Custom Tabs. Metadata của aar đòi bật core library desugaring trên app module (`SKILL:assets/unity/Editor/EOSAndroidGradlePatch.cs:12-14`). Danh sách dependency đầy đủ CHƯA KIỂM CHỨNG |
| Creator 3.8 | Native plugin (`cc_plugin.json` + CMake). Macro entry nhận **2 tham số**: `CC_PLUGIN_ENTRY(name, load_func)` (`CC388:cocos/plugins/Plugins.h:36-45`). Đăng ký binding sau khi script engine init xong: `cc::plugin::Listener(BusType::SCRIPT_ENGINE)` nhận `ScriptEngineEvent::POST_INIT` (`CC388:cocos/plugins/bus/BusTypes.h:29-34`), rồi gọi `se::ScriptEngine::getInstance()->addRegisterCallback(fn)` (`CC388:cocos/bindings/jswrapper/v8/ScriptEngine.h:102`). Trong `fn` dùng `sebind::class_<T>(...).install(ns)` (`CC388:cocos/bindings/sebind/class.inl`). CMake: `add_library(EOSSDK SHARED IMPORTED)` + `IMPORTED_LOCATION` theo ABI |
| cocos2d-x 4.x | Chỉ có CMake: khai báo imported shared lib như trên, `target_link_libraries(${APP_NAME} EOSSDK)`, copy `.so` vào `jniLibs/<abi>` |
| cocos2d-x 3.17 | Android mặc định dùng `ndk-build`: `include $(CLEAR_VARS)` + `LOCAL_MODULE := EOSSDK` + `LOCAL_SRC_FILES := …/$(TARGET_ARCH_ABI)/libEOSSDK.so` + `include $(PREBUILT_SHARED_LIBRARY)`, rồi thêm `LOCAL_SHARED_LIBRARIES += EOSSDK` vào module game (CHƯA KIỂM CHỨNG trên template 3.17) |
| iOS | PEW ship `EOSSDK.framework` (dynamic, có `Headers/`), **không** phải xcframework (`PEWPKG:Runtime/iOS/EOSSDK.framework`). Trong Xcode chọn Embed & Sign; include `<EOSSDK/eos_sdk.h>`. iOS min version, bitcode, và việc gói SDK chính thức có xcframework hay không: CHƯA KIỂM CHỨNG. iOS dùng `EOS_InitializeOptions` thường: `eos_ios.h` không có struct hệ thống tương đương Android, chỉ có option cho Auth |

---

## 2. Đăng nhập (Connect DeviceID) — khớp `01` §4.1, `03` §11, `05` §4.1

```
boot.task("eos")   # chạy trước Main Menu, retry được; dùng timer REALTIME của app (steady_clock), không dựa vào SDK
  platform up ≤15s → 1 tick → login ≤12s
  r = Connect_Login(EOS_ECT_DEVICEID_ACCESS_TOKEN, token=NULL, UserLoginInfo{DisplayName})
  if r == EOS_NotFound:                       # máy chưa có Device ID
      c = Connect_CreateDeviceId(DeviceModel) # ≤64 ký tự, có timeout riêng
      if c not in {Success, EOS_DuplicateNotAllowed}: fail(c)   # Duplicate = đã có sẵn ⇒ coi như OK
      r = Connect_Login(...)                  # login lại
  if r == EOS_InvalidUser:                    # có Device ID nhưng chưa có PUID
      r = Connect_CreateUser(r.ContinuanceToken)
  if r != Success: coordinator.onAuthFailed(r); return false
  puid = r.LocalUserId; coordinator.onAuthenticated(puid)
  AddNotifyAuthExpiration(→ Connect_Login lại với cùng credential)   # bắn ~1 phút trước khi hết hạn
  AddNotifyLoginStatusChanged(→ nếu rơi khỏi LoggedIn: báo coordinator, chặn intent mới)
```

| Điểm | Nguồn |
|---|---|
| `UserLoginInfo` **bắt buộc** khi login bằng Device ID (DisplayName ≤32 ký tự, không được xác thực, chỉ để hiển thị) | `PEW:Generated/Connect/LoginOptions.cs:19-25`, `UserLoginInfo.cs:14-21`, `ConnectInterface.cs:137` |
| `DuplicateNotAllowed` nghĩa là Device ID đã tồn tại; SDK bảo "proceed to calling Login directly". FishyEOS lại coi đây là lỗi (`FEOS:Util/Coroutines/AuthDataLogin.cs:94-104`), **đừng chép** | `PEW:Generated/Connect/ConnectInterface.cs:442` |
| `InvalidUser` → `CreateUser(continuanceToken)` | `FEOS:Util/Coroutines/ConnectLogin.cs:55-59` |
| AuthExpiration bắn "approximately 1 minute prior"; gọi lại Login để làm mới | `PEW:Generated/Connect/ConnectInterface.cs:145-146`, `:163` |
| PUID trong cache chỉ đáng tin khi `Connect_GetLoginStatus == EOS_LS_LoggedIn`. Nếu lobby op trả `InvalidAuth`/`AuthExpired` ⇒ force re-login đúng **một** lần rồi retry intent với generation mới | `ConnectInterface.cs:615`; `03` §11 |
| Không login trong cùng call tạo platform (sẽ mất callback, `04` A1). Mọi nhánh fail phải báo coordinator (`04` A2) | `03` §11 |
| Dev Auth (test trên 1 máy): `Auth_Login(Developer, "localhost:<port>", <nhãn>)` → `Connect_Login(EOS_ECT_EPIC, access token)`, **không** gửi `UserLoginInfo` (`04` A6) | `05` §4.2 |

---

## 3. Lobby: kết quả poll là nguồn sự thật

| Luật | Chi tiết |
|---|---|
| Schema | Dùng **đúng tên key của KT**, giống `LobbyKeys` trong Core (`SKILL:assets/unity/Core/LobbyProtocol.cs:24-37`: `mode, bundle, is_room_started, room_phase, match_epoch, join_order_mapping, code, former_members, private`; member: `user_id, client_phase, load_epoch`), thêm `ready_status`, `user_name` (`02` §2.1). Lý do: Core + 113 test là spec chạy được, và việc EOS có nhận dấu `.` trong tên key hay không vẫn CHƯA KIỂM CHỨNG (`02` §2.3). Key protocol **mới** thêm vào `LobbyKeys` cùng kiểu tên không prefix (vd `proto_major`); key của game dùng prefix `game_` (quy tắc chốt ở `02` §2.3). Tên key ≤64 ký tự (`LOBBYMODIFICATION_MAX_ATTRIBUTE_LENGTH` là độ dài **tên**), tối đa 64 attribute (`PEW:Generated/Lobby/LobbyInterface.cs:209-213`). Độ dài value: CHƯA KIỂM CHỨNG |
| Giới hạn khác | `MAX_LOBBY_MEMBERS` 64, `MAX_SEARCH_RESULTS` 200, `MAX_LOBBIES` 16 lobby/user (`LobbyInterface.cs:281-293`) |
| Poll | Mỗi 1s theo realtime (steady_clock, không dùng scheduler vì scheduler có thể bị pause). Mỗi lần poll: `EOS_Lobby_CopyLobbyDetailsHandle` (`LobbyInterface.cs:792`) → đọc attr/member (`LobbyDetails.cs:77,199,343,367`) → **đọc lại owner** bằng `EOS_LobbyDetails_GetLobbyOwner` (`:293`) → release handle. Snapshot mang version, tăng sau mỗi lần poll hoàn tất |
| Notify | `AddNotifyLobbyUpdateReceived` / `…MemberUpdateReceived` / `…MemberStatusReceived` (`LobbyInterface.cs:649,608,567`) **chỉ** dùng để kích hoạt đọc lại ngay. Không bao giờ áp payload của notify thẳng vào state. Đúng khuyến nghị ở `02` §11; KT không dùng notify nào |
| Đọc rỗng | Coi là unknown → Wait. Muốn kết luận "chỉ còn mình" cần 2 lần poll có version khác nhau (`03` §6, `PostMatchPartyPolicy`) |
| Ghi | 1 batch cho 1 `UpdateLobby`, qua `UpdateLobbyModification` (`LobbyInterface.cs:1779`, `:1740`). Kiểm kết quả **từng** `AddAttribute`/`AddMemberAttribute`, release modification ở mọi nhánh (`02` §12 #4). Sau mỗi lần ghi quan trọng: confirm snapshot 40×50ms. Callback báo lỗi vẫn phải confirm, rồi rollback hoặc vào terminal (`03` §5) |
| Mọi async | Có watchdog (join 25s, create/random 30s, leave budget 8s), GenerationGuard, single-flight intent, dựng fence leave trước khi join (`03` §1–4) |
| Visibility | Tạo phòng ở `Publicadvertised`. Khoá sang `Joinviapresence` sau khi open tuple được confirm; mở lại khi closed. **Không bao giờ dùng `Inviteonly`** (`02` §4, `LobbyModification.cs:256`) |

---

## 4. Transport P2P

### 4.1 Hằng số và kênh (đã kiểm với wrapper + C header)

| Mục | Giá trị | Nguồn |
|---|---|---|
| Kích thước gói tối đa | **1170 byte** (`EOS_P2P_MAX_PACKET_SIZE`). `SendPacket` gói lớn hơn ⇒ `EOS_LimitExceeded` | `PEW:Generated/P2P/P2PInterface.cs:82`, `:738`; `PEWPKG:Runtime/iOS/EOSSDK.framework/Headers/eos_p2p_types.h:14` |
| Socket name | ≤32 ký tự (`SOCKETID_SOCKETNAME_SIZE` = 33, tính cả NUL). Host và client dùng **chung một tên**, là hằng số của build (vd `"game"`); đổi tên khi đổi wire major | `P2PInterface.cs:118` |
| Số kết nối tối đa | 32 | `:78` |
| Reliability | `EOS_PR_UnreliableUnordered=0`, `ReliableUnordered=1`, `ReliableOrdered=2`. Gói ordered chỉ giữ thứ tự với các gói ordered khác | `PEW:Generated/P2P/PacketReliability.cs` |
| Kênh 0 | `ReliableOrdered`, `AllowDelayedDelivery=true`: handshake, RPC/action, event, spawn/despawn/ownership, state delta, mảnh snapshot, probe | Khớp FishyEOS (`FEOS:Core/CommonPeer.cs:89-101`) |
| Kênh 1 | `UnreliableUnordered`, `AllowDelayedDelivery=false`: pose 30 Hz | như trên |
| Relay | `EOS_P2P_SetRelayControl(EOS_RC_AllowRelays)` là **mặc định**, giữ nguyên. `ForceRelays` chỉ dùng cho build test muốn ép đi qua relay (tương thích với AllowRelays; cặp `NoRelays`×`ForceRelays` không kết nối được) | `PEW:Generated/P2P/RelayControl.cs:16-37`, `P2PInterface.cs:822` |
| Hàng đợi | `AddNotifyIncomingPacketQueueFull` → log 1 lần, rút gói ngay hoặc tăng `SetPacketQueueSize`. Nếu không làm gì, gói tới sau đó bị **vứt bỏ** | `P2PInterface.cs:157-178`, `:769` |
| `SendPacket == Success` | Chỉ nghĩa là gói đã vào hàng đợi, chưa chắc đã tới nơi | `:725` |
| Đóng kết nối | Gói đang chờ (**kể cả reliable**) bị xả bỏ. Vì vậy khi nhận Closed phải bỏ mọi trạng thái giao vận đang dở (mảnh, ack) | `:296` |
| Chẩn đoán NAT | Gọi `EOS_P2P_QueryNATType` một lần sau login, đưa kết quả vào heartbeat telemetry | `:634` |

### 4.2 Bảng peer phía host: áp toàn bộ bài học FishyEOS

| # | Luật | Bài học |
|---|---|---|
| H1 | **Bind ngay khi sở hữu phòng** (lúc commit create, hoặc khi được promote làm owner): `AddNotifyPeerConnectionRequest(socket)` + `…Established` + `…Interrupted` + `…Closed`. Phải xong **trước** khi ghi `is_room_started=1` hay phát tín hiệu co-load. SDK: request tới socket chưa bind "will be silently ignored" (`P2PInterface.cs:123-125`). Ở KT, máy nào có log `UNKNOWN SOCKET` thì guest bị đá 45,6% | C8 (KT chưa sửa; Cocos phải làm đúng từ đầu) |
| H2 | Accept policy: chỉ gọi `EOS_P2P_AcceptConnection` khi PUID bên kia đang là member của lobby (đọc từ snapshot) **và** (phòng đang LOBBY, hoặc `RejoinAdmission ≠ Reject`). PUID chưa có trong snapshot ⇒ đọc lại snapshot rồi mới quyết, không reject ngay | `02` §10 |
| H3 | `PeerId` ổn định theo PUID suốt phiên host (`map<puid, peerId>`; chỉ cấp id mới khi PUID chưa từng xuất hiện). Reconnect thật giữ nguyên id, nhờ đó vẫn giữ entity mình sở hữu | C3 (`FEOS:Core/ServerPeer.cs:199-202`) |
| H4 | Request trùng từ PUID **đã established** (do đổi relay↔direct) ⇒ **bỏ qua**: không cấp id mới, không đóng kết nối | C3 (`ServerPeer.cs:179-182`) |
| H5 | PUID đã accept nhưng **chưa** established (zombie) gửi request mới ⇒ evict (gỡ notify riêng của nó, `CloseConnection`) rồi accept request mới | C4 (`ServerPeer.cs:186-193`) |
| H6 | `Interrupted` ⇒ hẹn giờ grace **3s realtime** ⇒ hết giờ thì đóng chủ động, vô điều kiện, qua **một** hàm teardown dùng chung. Nếu nhận `Established(Reconnection)` trong lúc grace thì huỷ hẹn giờ | C5 (`ServerPeer.cs:91`, `03` §8). SDK: interrupted thì tự thử nối lại, thất bại sẽ báo Closed (`P2PInterface.cs:290-296`) |
| H7 | `Closed` ⇒ đi qua cùng hàm teardown: gọi `EOS_P2P_CloseConnection(local, remote, socket)` (bắt buộc để rejoin được), xoá khỏi tập established, giữ map PUID→peerId, đánh dấu peer "not loaded", huỷ các mảnh đang ráp dở | C7 |
| H8 | Mọi API theo từng kết nối (kick, stats, address) tra theo `PeerId` trong **một** bảng duy nhất; không tồn tại id space thứ hai | C1, invariant 7 |
| H9 | Khi dừng host: `EOS_P2P_CloseConnections(socket)` + `RemoveNotify*` cho mọi handle. Bảng peer reset ở cold-start / module init; không giữ static sống qua các phiên host (bẫy `_latestId`) | `04` Còn mở #8, H4 |

Phía client: đăng ký `AddNotifyPeerConnectionClosed/Interrupted/Established` cho socket. Dial = `AcceptConnection(host)` rồi gửi `Hello`. Khi `DisableAutoAcceptConnection=false`, `SendPacket` tự mở kết nối (`PEW:Generated/P2P/SendPacketOptions.cs:39-54`). Việc dial lại thuộc về reconnect loop ở §6, không dùng timer mù.

---

## 5. Netcode host-authoritative

### 5.1 Envelope và phân mảnh

```
Header (8 byte, little-endian):  u8 type | u8 flags | u16 seq | u32 tick     # tick = host tick lúc tạo message
Payload ≤ 1170 - 8 = 1162 byte
FRAG (kênh 0): u16 msgId | u8 part | u8 count | bytes     # count ≤ 64 (~74 KB), msgId tăng dần theo từng peer
```
- Message lớn hơn 1162 byte (snapshot, full resync) ⇒ cắt thành FRAG trên **kênh 0**. Kênh này là ReliableOrdered nên các part tới đúng thứ tự; ráp theo `(sender, msgId)`. Nếu `part` sai thứ tự, `count` vượt trần, hoặc quá 5s chưa đủ ⇒ bỏ buffer và yêu cầu resync. Không bao giờ phân mảnh trên kênh 1: pose phải vừa 1 gói (4 người × ~24 byte là dư sức).
- `seq` trên kênh 0: SDK đã lo thứ tự, nên `seq` chỉ dùng để chẩn đoán và loại gói trùng phòng thủ (SDK ghi "may be sent multiple times"; đầu nhận có tự khử trùng hay không thì CHƯA KIỂM CHỨNG). Trên kênh 1: **lấy bản mới nhất theo tick** cho từng entity, không từ chối vì hở seq. So sánh an toàn khi quay vòng số: `newer(a,b) = (int32_t)(a - b) > 0` (với u16 seq thì dùng `int16_t`).

### 5.2 Bảng message

| Type | Kênh | Hướng | Payload | Ghi chú |
|---|---|---|---|---|
| `Hello` | 0 | C→H | `u16 protoMajor, u16 protoMinor, str8 bundle, u32 matchEpochSeen` | Bắt buộc là message đầu tiên |
| `Welcome` / `Reject` | 0 | H→C | `u8 peerId, u8 slot, u32 hostTick, u32 matchEpoch` / `u8 reason` | `protoMajor` khác, hoặc `bundle` ≠ `bundle` của phòng ⇒ gửi `Reject` rồi `CloseConnection`. Room attr `proto_major` = protoMajor để random match lọc từ trước (cùng cách đang lọc `bundle`, `02` §3.3) |
| `Ping` / `Pong` | 0 | C↔H | `u32 clientMs` / `u32 clientMs, u32 hostTick, u16 hostSubTickUs` | Dùng cho đồng hồ (§5.4) và RTT |
| `ProbeReq` / `ProbeAck` | 0 | C→H / H→C | `u32 token` | Liveness (§6) |
| `Spawn` | 0 | H→C | `u32 entityId, u16 typeId, u8 ownerPeer (0xFF=host), u32 tick, bytes initState` | |
| `Despawn` | 0 | H→C | `u32 entityId, u32 tick, u8 reason` | |
| `OwnershipChange` | 0 | H→C | `u32 entityId, u8 newOwnerPeer, u32 tick` | Chỉ host được đổi owner |
| `State` | 0 | H→C | `u32 entityId, u32 revision, fields…` | Gửi delta. List quan trọng mang `revision + crc32` (P5, `unity/gameplay-replication.md`) |
| `ResyncReq` | 0 | C→H | `u32 entityOrListId, u32 haveRevision` | Có cooldown ở cả 2 phía |
| `SnapBegin` / FRAG… / `SnapEnd` | 0 | H→C | `u32 snapId, u32 tick, u16 entityCount` / — / `u32 crc32` | Xem §5.5 |
| `Pose` | 1 | C→H, H→C | `u32 tick, u8 n, [u32 entityId, f32×3 pos, u16 yawQ]` | Movement do client quyết (giống KT P1); host kiểm NaN/tốc độ rồi phát lại |
| `Action` / `ActionApplied` / `ActionRejected` | 0 | C→H / H→C | `u16 clientSeq, u32 tick, u16 actionId, u32 targetEntity, params` / `u8 peer, u16 clientSeq, result` / `…, reason` | Client gửi **identity** của mục tiêu, không gửi kết quả raycast (P2) |
| `Event` | 0 | H→C | `u16 eventId, u32 tick, params` | Hiệu ứng thuần hình ảnh; không phát lại cho người vào sau |
| `Leave` | 0 | 2 chiều | `u8 reason` | Phân biệt tự rời với host đóng phòng (invariant 8) |

### 5.3 Entity id
Host cấp `u32` tăng đơn điệu từ 1 trong mỗi phiên host (0 = không hợp lệ), không tái dùng trong phiên. Entity tĩnh của scene có id cố định theo thứ tự tác giả đặt: cùng build thì cùng id, còn khác bundle thì đã bị chặn ở Hello. Cách này tương đương sceneId của FishNet (`04` D8). Client **không** cấp id. Vật client dự đoán trước (vd vật ném) dùng id tạm cục bộ, rồi gán vào `Spawn` thật qua `clientSeq` của `Action`.

### 5.4 Đồng hồ chung (invariant 9)
- Host tick chạy 30 Hz, tính từ lúc start server. Mọi mốc gameplay (start, đếm ngược, tuổi order, cooldown) đều là **tick được replicate**, không phải "lúc tôi nhận được tin" (`04` D6, D7, G6). Khi start: host replicate `startTick = now + 45` (đi trước 1,5s).
- Client ước lượng `offset = hostTick + rtt/2 − localTick` từ các `Pong`: giữ 8 mẫu gần nhất, chọn mẫu có RTT nhỏ nhất, làm mượt; đồng hồ hiển thị không bao giờ chạy lùi. Render entity remote ở `estimatedHostTick − 2..3` tick (interpolation giống KT `_interpolation=2`).

### 5.5 Snapshot cho người vào giữa trận / rejoin
Sau khi peer qua observer gate (§5.6): host chụp snapshot ở tick T (mọi entity + state list + đồng hồ). Gửi `SnapBegin` → các FRAG → `SnapEnd(crc32)` trên kênh 0, rồi gửi các delta có tick > T trên **cùng** kênh 0. Kênh là ReliableOrdered nên delta không thể tới trước snapshot. Phía client: crc sai ⇒ gửi `ResyncReq` toàn phần; `Pose` trên kênh 1 có tick ≤ T thì bỏ. Peer rejoin giữ nguyên `peerId` (H3) nên entity mình sở hữu vẫn là của mình.

### 5.6 Observer gate chặn **mọi** replication (D2)
Trạng thái của từng peer phía host: `Connected → Welcomed → Loaded (load_epoch == match_epoch, đọc từ lobby snapshot) → Synced (đã gửi xong snapshot)`. Trước `Loaded`, host chỉ gửi message điều khiển (`Welcome/Reject/Pong/ProbeAck/Leave`); **không** gửi `Spawn/Despawn/State/Event/Pose/Ownership/Snapshot`. Khi `match_epoch` tăng (rematch), mọi peer remote lập tức rơi về `Welcomed` và gate đóng lại cho tới khi peer báo đã load epoch mới. Lý do: transport sống qua đổi scene, đúng như bug D2. Spawn player của peer cũng chỉ làm khi `Loaded` (D1). Spawn và visibility là **cùng một** gate (invariant 6). Alarm 1 lần: peer ở `Welcomed` ≥10s mà chưa `Loaded` ⇒ log error (`03` §12).

### 5.7 Handler: không ném exception, có kiểm tra, có trần (invariant 10, G5)
- Reader có kiểm biên và trả `false` khi lỗi, không ném exception, không abort. Message hỏng ⇒ bỏ và tăng bộ đếm, **không** kick (kick vì lỗi parse chính là bug G5). Chỉ `Hello` sai version mới dẫn tới đóng kết nối.
- Người gửi phải là PUID có trong bảng peer. Message từ socket khác hoặc từ PUID lạ ⇒ bỏ.
- Rate limit theo `(peer, type)`: `Action` cách nhau tối thiểu 80 ms (như `AcceptsActionRpc` của KT), `ResyncReq` có cooldown, `Pose` không quá 2× tần số tick. Vượt ⇒ bỏ.
- Validate: đúng owner của entity, trong tầm với tính theo pose phía host, số hữu hạn, clamp giá trị (P2).

---

## 6. Phiên, barrier, liveness: chép nguyên core

| Chủ đề | Luật cho Cocos | Spec |
|---|---|---|
| Tìm host | Host = lobby owner. Chờ owner hợp lệ tối đa 10s, không đoán vai. Đọc lại owner mỗi lần poll. Owner đổi trong lúc IN_MATCH ⇒ thoát kiểu terminal | `02` §5, §11 |
| Start barrier (owner) | **Đúng thứ tự `02` §6:** chặn nếu đang terminal hoặc có begin khác đang chạy → chuẩn hoá roster + kiểm exact ready roster → `next = match_epoch+1`, tính `join_order_mapping` → ghi **1 batch** `join_order_mapping` + `former_members="<next>\|puid,…"` → poll tới khi bản replicate khớp → kiểm lại → **begin** bằng 1 batch (`is_room_started=1, room_phase=IN_MATCH, match_epoch=next`; member host `client_phase=IN_MATCH, load_epoch=0`) → nếu ghi lỗi thì đọc lại: backend đã mở thì rollback về closed tuple, không xác định được thì vào terminal → confirm open tuple 40×50ms → kiểm lại lần cuối; drift ⇒ rollback + confirm closed, không confirm được ⇒ terminal teardown → commit roster vào bản process-local → **khoá `Joinviapresence`** (product path của KT chưa làm bước này, `02` §12 #1) → co-load. Không lấy thứ tự của gym path làm mẫu | `02` §6, §12 #10 |
| Load barrier | Co-load khi `is_room_started=="1" && room_phase=="IN_MATCH"` và epoch đó chưa finished ở máy mình. Chỉ báo `load_epoch` khi scene **thật sự** đã lên. Start gate: roster đã ghim (ledger) phải connected + loaded và ổn định 0,5s; chỉ bớt người khi việc rời được xác nhận ≥1s; hết timeout cũng không bao giờ tự start | `02` §7 |
| Ledger | Barrier chỉ đếm PUID có trong `former_members` cùng epoch và còn link sống | `02` §9 |
| Return | Đóng epoch nhưng giữ `match_epoch`; mở lại `Publicadvertised`; quorum phía host 5s, fallback phía client 8s | `02` §8 |
| Rejoin (lưu id) | Persist `lobbyId` khi create/join commit (cả host lẫn guest). **Chỉ** xoá khi chủ động leave hoặc bị kick; kill/crash thì giữ lại | `02` §10, `04` E5 |
| Rejoin (gate host) | `RejoinAdmission.Decide`: đang ở lobby → Allow; host local → Allow; PUID chưa resolve → Unknown (retry ≤10×0,1s rồi reject); trong grace 2s sau start → Allow; ledger thiếu hoặc sai epoch → Allow; còn lại Allow khi và chỉ khi PUID ∈ ledger. Bất định về ledger → Allow; bất định về danh tính (PUID không resolve) → Reject. Trên EOS P2P, PUID đi kèm connection request nên host luôn có PUID ngay | `02` §10, `Core/RejoinAdmission.cs` |
| Reconnect (client) | Kiểm mỗi 2s. Chỉ dial lại khi link thật sự đã đóng (`Closed` / không có kết nối) **hoặc** lần thử hiện tại kẹt ≥12s. Bỏ cuộc sau 90s ⇒ phát `ReconnectGaveUp` **sau khi** đã clear cờ. Cờ `reconnecting` đặt trong try/finally (C++: RAII guard). Chỉ có một điểm vào duy nhất để rebuild | `03` §9, `04` E1 |
| Tự dừng ≠ host rời | `CloseConnection` do chính lần dial lại gây ra không được xử lý như "host đóng phòng". Chỉ route về menu khi nhận `Leave(hostClosing)`, khi lobby đóng/mất owner, hoặc khi `ReconnectGaveUp` | invariant 8 |
| Probe | Round-trip ở tầng app bằng `ProbeReq/ProbeAck` trên kênh 0, kết quả **ba trạng thái** `Alive / Dead / Unavailable` (`Core/IP2PBoundary.cs:10-15`). Không có link hay probe để hỏi (menu, lobby, chưa Welcomed) = `Unavailable`, nghĩa là **không có bằng chứng đã chết**, không được xé link. Quá 2s = `Dead`. Watchdog foreground 4s chạy độc lập với lifecycle. Khi resume: chờ settle 2s → probe | `03` §10, `04` E2 |

---

## 7. Port policy từ Core sang Cocos

Logic thuần trong `SKILL:assets/unity/Core/*.cs` không phụ thuộc Unity. **113 test NUnit ở `SKILL:assets/unity/Tests/Core/` là spec chạy được** (`dotnet test`, `unity/templates-guide.md` §9). Port từng test case sang test runner phía Cocos (Creator: vitest/jest cho TS; cocos2d-x: Catch2/GoogleTest cho C++) **trước** khi viết implementation. Test đỏ nghĩa là port sai.

| Core (C#) | Vai trò | Module Cocos (Creator TS / C++) | Test cần port |
|---|---|---|---|
| `LobbyProtocol.cs` | Key + scope + codec cho ledger/join order | `net/lobby-protocol.ts` / `lobby_protocol.h` | `LobbyAttributesTests`, `FormerMembersTests` |
| `ILobbyService.cs`, `PlayerId.cs` | Seam lobby, snapshot member | `net/lobby-service.ts` (interface) + C++ `EosLobby` | dùng trong fake |
| `GenerationGuard.cs` | Chặn callback muộn | `net/generation-guard.ts` | `GenerationGuardTests` |
| `LobbyConnectWatchdog.cs` | Watchdog + xử lý kết quả muộn | `net/watchdog.ts` | `LobbyConnectWatchdogTests` |
| `EpochPhaseCoordinator.cs` | Begin/close epoch, barrier theo ledger | `net/epoch-coordinator.ts` | `EpochPhaseCoordinatorTests` |
| `RejoinAdmission.cs` | Gate rejoin phía host | `net/rejoin-admission.ts` (C++ accept policy gọi qua binding) | `RejoinAdmissionTests` |
| `MatchReturnQuorum.cs`, `ClientReturnFallback.cs` | Return 5s/8s | `net/match-return.ts` | `MatchReturnQuorumTests` |
| `PersistentPeerReuse.cs`, `IP2PBoundary.cs` | Quyết định reuse link theo probe ba trạng thái | `net/peer-reuse.ts` + C++ `P2PBoundary` | `PersistentPeerReuseTests` |
| `AppLifecycleRecovery.cs` | Resume → Reuse/Rebuild/HostReturn | `net/app-lifecycle.ts` | `AppLifecycleRecoveryTests` |
| `PostMatchPartyPolicy.cs` | Đọc rỗng = Wait | `net/post-match-party.ts` | (viết mới theo `03` §6) |
| `RandomMatchPolicy.cs` | Lọc random match + fallback tạo phòng | `net/random-match.ts` | `RandomMatchPolicyTests` |
| `RoomCodeGenerator.cs` | Mã 6 ký tự, 31 glyph | `net/room-code.ts` | `RoomCodeGeneratorTests` |
| `RoomVisibility.cs` | Publicadvertised / Joinviapresence | `net/room-visibility.ts` | — |
| `GameSideInterfaces.cs` | Lưu `last_room_id`, sink chẩn đoán | `net/room-history-store.ts` | — |
| `SKILL:assets/unity/Eos/EosLobbyOperationOwnership.cs` + `LobbyLeaseTypes.cs` (thuật toán lease/fence, pending-leave fence, mutation lease → leave, cleanup debt, lifecycle certificate) | Chống callback EOS trễ đè membership mới (`03` §4) | C++ `lobby_ownership.{h,cpp}` cạnh `EosLobby` (chạy trên game thread, cùng tick) | Chưa có test trong skill (KT test nằm trong bộ `ProductLobby*`, chưa port) → **viết test trước** theo các ca `03` §4: callback trễ sau leave, join lại cùng lobby khi leave chưa xong (chờ ≤8 s), mutation timeout chuyển thành leave |
| Single-flight intent (KT: `RoomIntentCoordinator` + `ProductLobbySession._connectInFlight`, mô tả `03` §3; pseudo-code `unity/templates-guide.md` §7) | Một intent tại một thời điểm, leave vượt intent cũ, phòng tới muộn bị release | `net/room-intent.ts` | Viết mới: bấm Create 2 lần → 1 phòng; Create rồi Back → phòng tới muộn bị leave; leave rồi create phải chờ leave xong |

Phân tầng: C++ giữ hot path (lời gọi EOS, tick, bảng peer ở §4.2, codec + phân mảnh ở §5.1, observer gate). TS giữ policy ở cold path và UI. cocos2d-x không có TS nên viết toàn bộ bảng trên bằng C++. Kết quả async EOS sang TS: C++ trả Promise và resolve ngay trong callback (cùng thread, §1.1); mọi Promise vẫn đi qua watchdog phía TS.

---

## 8. Rig test Cocos (bổ sung cho `07`)

| Rig | Cách có 2 PUID | Dùng cho |
|---|---|---|
| A. 2 process desktop (bản build native Windows/macOS của Creator, hoặc simulator) | EOS Dev Auth Tool với 2 **tài khoản Epic khác nhau**, credential `Developer` (`Id="localhost:<port>"`, `Token=<nhãn>`); mỗi process nhận nhãn qua CLI `-devAuthCredential=Player1/2`. Hoặc trộn: process 1 dùng DeviceID, process 2 dùng Dev Auth (khác provider nên khác PUID, `05` §4.2) | Lobby, barrier, spawn, rematch ≥2 trận trong cùng process, đổi host |
| B. 2 thiết bị Android (hoặc 1 Android + 1 desktop) | DeviceID (khác máy nên khác PUID) | Relay/NAT thật, chạy nền, kill rồi mở lại, cảm ứng |

- Code Dev Auth chỉ biên dịch trong build dev desktop; endpoint chỉ nhận `localhost`; cấu hình sai thì báo lỗi luôn (fail-closed, `05` §4.2). Mọi build desktop chạy cùng một OS user dùng chung **một** PUID DeviceID (`04` H1).
- Quy trình 2 thiết bị: làm nguyên văn `07` §3 (cùng build + hash, force-stop trước mỗi lượt, khởi động lệch nhịp và chờ roster=2, join bằng UI thật, đưa app xuống nền ~40s, tắt/bật mạng, kill rồi bấm Continue).
- Telemetry 2 phía: shipper chỉ biên dịch khi có define dev (tương đương `KT_DEV_TELEMETRY`). Gửi log + heartbeat 5s `{roomId, puidShort, rtt, natType, relayed?, scene, peerState}`; collector ghi JSONL, ghép 2 phía theo `roomId` (`07` §6, `KT:Assets/Scripts/Core/DevTelemetryShipper.cs:170-186`). Bảng peer log mọi quyết định kèm lý do: `ACCEPT/IGNORED/EVICT/CLOSED/GRACE` (như `FEOS:Core/ServerPeer.cs`).
- Mỗi lần test chỉ được tính khi thoả đủ 5 điều kiện ở `07` §0.

---

## 9. Thứ tự làm và 5 gate kiểm chứng

**Gate 1: Platform + login (2–3 ngày).** Boot task: `EOSSDK.init` (Android) → `EOS_Initialize` → `EOS_Platform_Create` → tick ở `BeforeTick` → chuỗi login ở §2 (NotFound → CreateDeviceId, Duplicate = OK → Login → InvalidUser → CreateUser). ✓ 2 thiết bị ra 2 PUID khác nhau; thread id log được trong mọi callback trùng với game thread; ngắt mạng lúc login ⇒ timeout 12s báo về coordinator, retry thì thành công; pause/resume gọi SetApplicationStatus đúng thứ tự.

**Gate 2: Lobby (2–3 ngày).** Create (`Publicadvertised`, max member theo mode, bucket `<game>-v1` (`02` §3.1); seed trong **batch đầu tiên**: `mode, bundle, proto_major, is_room_started=0, room_phase=LOBBY, match_epoch=0, private=0|1`) → ghi member attrs (`ready_status=0, client_phase=LOBBY, load_epoch=0, user_name`) → sinh và ghi `code`, search lại để chống trùng → máy B join bằng code. ✓ Roster có 2 member và **đúng 1 owner**; notify chỉ kích hoạt đọc lại; kick B ⇒ B phát hiện trong ≤1 lần poll và xoá `last_room_id`; bấm Create 2 lần liền chỉ ra 1 phòng.

**Gate 3: P2P (3–4 ngày).** Host bind theo §4.2 H1 ngay khi tạo phòng; B dial → `Hello/Welcome` → `Ping/Pong` đo RTT; gửi một message 4 KB qua FRAG; dùng một build test `ForceRelays` ở một phía để ép đi qua relay. ✓ RTT có log ở 2 phía; message 4 KB ráp đúng crc; bundle khác ⇒ `Reject`; tắt mạng B 10s ⇒ host thấy Interrupted → grace 3s → Closed, B nối lại vẫn giữ `peerId`.

**Gate 4: Start/load/spawn (3–4 ngày).** Start barrier đúng `02` §6 (ledger + join_order → confirm → begin → confirm/rollback → khoá `Joinviapresence`) → co-load → báo `load_epoch` → observer gate ở §5.6 → snapshot → spawn → `startTick` chung. ✓ Cả 2 player hiện, đếm ngược khớp nhau; cố ý làm ghi begin lỗi ⇒ rollback về LOBBY; người lạ có code không join được phòng đang IN_MATCH; **≥2 trận liên tiếp + rematch trong cùng process**, có đổi vai host.

**Gate 5: Rejoin/chạy nền (2–3 ngày).** B xuống nền 40s rồi quay lại (probe → reuse hoặc rebuild); B kill app → mở lại → Continue → `JoinById(last_room_id)` → `RejoinAdmission` → co-load thẳng → snapshot → có lại nhân vật cũ với cùng `peerId`; host kill app ⇒ B nhận `ReconnectGaveUp` hoặc thấy lobby mất ⇒ về menu, không treo. ✓ Không xuất hiện "phòng bị host đóng" khi host vẫn sống (E1); đo được thời gian rejoin sau kill (KT: ~9s để giải phóng slot, ~15s đầu-cuối).

---

## 10. Rủi ro

| Rủi ro | Giảm thiểu |
|---|---|
| SDK đổi giữa các bản | Pin bản SDK; mỗi lần nâng cấp, grep lại các tên API trong tài liệu này với header mới |
| Thiếu ABI v7a | Bỏ v7a, hoặc kiểm gói SDK chính thức xem có v7a không (§1.3) |
| Lobby bị rate-limit | Gom batch, retry tối đa 3 lần, không ghi trong vòng poll (`03` §7) |
| Độ trễ relay 150–350 ms | Movement do client quyết + interpolation; action do host quyết sau khi validate |
| Chi phí gọi qua JSB | Không gọi qua binding trong vòng lặp nóng; pose/gói xử lý hết trong C++ |
| Web/mini-game | Không có EOS, cần kiến trúc khác (`web-minigame-future.md`) |

## 11. Còn mở / CHƯA KIỂM CHỨNG

- Khẳng định chung "mọi callback EOS chạy trong Tick, trên thread gọi Tick": SDK chỉ nói rõ cho từng API riêng (§1.1).
- Trên Cocos có bắt buộc `System.loadLibrary("EOSSDK")` trước `EOSSDK.init` không; danh sách dependency Gradle đầy đủ của `eos-sdk.aar`; Android có cần set internal/external dir không.
- iOS: min version, và gói SDK chính thức phân phối dạng framework hay xcframework.
- Độ dài tối đa của **value** attribute lobby (ảnh hưởng tới `join_order_mapping`/`former_members` khi có 4 PUID).
- ReliableOrdered có thể giao trùng ở đầu nhận không (hiện đang khử trùng phòng thủ bằng `seq`).
- Relay EOS có điểm gần VN không: đo qua telemetry `QueryNATType` + RTT sau Gate 3.

## Tài liệu liên quan
`02-room-protocol.md` (port 1:1) · `03-robustness-patterns.md` · `04-bug-catalog.md` (top 10 bất biến) · `05-eos-portal-and-config.md` · `07-testing-playbook.md` · `unity/gameplay-replication.md` (P1–P8) · `unity/templates-guide.md` · `SKILL:assets/unity/FishyEOS-patches/README.md`.

**Status:** DESIGN (CHƯA KIỂM CHỨNG) · phải qua Gate 1–5 trên dự án thật.
