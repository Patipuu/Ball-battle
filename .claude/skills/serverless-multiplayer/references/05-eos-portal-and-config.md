# 05 — EOS Dev Portal, file config và secret

> Mục đích: dựng một EOS product mới từ số 0 (Portal → config trong build → login DeviceID/Dev Auth) đủ để chạy topology ở `01-architecture.md`.
> Baseline: KT commit `7d96acd5` (HEAD 2026-10-05).
> Đọc khi: bắt đầu dự án mới, đổi môi trường (Dev/Stage/Live), hoặc gặp lỗi auth EOS. Đối tượng: dev setup + người giữ tài khoản Epic Dev Portal.

Nhãn: **[GENERIC]** mọi engine; **[KT-SPECIFIC]** cách KT làm; **CHƯA KIỂM CHỨNG** = chỉ từ docs/suy luận, KT chưa đo.
Cột "Thấy ở KT" = KT có bằng chứng (code/doc/log) cho bước đó; "Docs" = chỉ lấy từ docs Epic (tra 2026-10-05).

## 1. Checklist Dev Portal [GENERIC]

Docs gốc (tra 2026-10-05; trang render bằng JS nên đọc bằng trình duyệt):
- Product/sandbox/deployment: https://dev.epicgames.com/docs/dev-portal/product-management
- Client + client policy: https://dev.epicgames.com/docs/dev-portal/client-credentials
- Bảng client policy: https://dev.epicgames.com/docs/epic-online-services/eos-fundamentals/client-and-client-policy/client-policy-reference
- Epic Account Services (EAS) app: https://dev.epicgames.com/docs/epic-online-services/accounts-and-social/eos-epic-account-services/auth-interface/auth-guide/set-up-epic-account-services
- Dev Auth Tool: https://dev.epicgames.com/docs/epic-online-services/accounts-and-social/eos-epic-account-services/auth-interface/auth-guide/log-in-with-the-dev-auth-tool
- Device ID: https://dev.epicgames.com/docs/api-ref/functions/eos-connect-create-device-id

| # | Bước | Chi tiết | Thấy ở KT | Nguồn |
|---|---|---|---|---|
| 1 | Organization | Tạo/được mời vào organization trên Dev Portal. Mỗi tài khoản Epic dùng Dev Auth cũng phải là **thành viên org** (đã accept lời mời) | Có — tài khoản Epic thứ 2 phải accept org membership mới login được | `KT:docs/multiplayer-testing/eos-dev-auth.md:35` |
| 2 | Product | Tạo product → trang **Product Settings** quản lý product, sandbox, deployment, client | Có (config import có `ProductName/ProductId`) | Docs product-management; `KT:Assets/StreamingAssets/EOS/eos_product_config.json` (tên trường) |
| 3 | Sandbox | Product có sandbox riêng tư mặc định (thường dùng "Dev" cho phát triển, "Stage" cho QA) + Live. Mỗi sandbox là một không gian dữ liệu tách biệt: PUID/lobby của Dev **không** thấy được từ Live | KT dùng **1** sandbox (mảng `Environments.Sandboxes` dài 1) | Docs; KT config |
| 4 | Deployment | Tạo deployment trong tab Sandbox của Product Settings; deployment gắn với đúng 1 sandbox | KT dùng **1** deployment | Docs; KT config |
| 5 | Client policy | Tạo/chọn policy **trước** khi tạo client. Predefined: `GameClient`, `Peer2Peer` (client không tin cậy, host P2P, cần user đăng nhập), `TrustedServer`, `GameClient /w UnlockAchievements`. Production nên dùng **custom policy** quyền tối thiểu, tick **User required** | Không thấy tên policy KT dùng trong repo | Docs client-policy-reference |
| 6 | Quyền tối thiểu cho topology này | Connect (login DeviceID) · Lobbies: `connect`, `readLobby`, `findLobbies` (+ `findLobbiesByUserId` nếu cần) · P2P (NAT/relay) · Voice: `createLobbyConference` (voice phòng lobby, cần user). **Không** cấp action đòi trusted server (`createRoomToken`, voice `mute`/`kick`) cho client | — | Docs (tóm tắt qua search); danh sách action **CHƯA KIỂM CHỨNG** trên UI Portal hiện tại |
| 7 | Client | Tạo client gắn policy ở bước 5 → nhận Client ID + Client Secret. **Encryption Key** do dev tự sinh (vd `openssl rand -hex 32`): khoá 256-bit = 64 ký tự hex, SDK chỉ dùng cho **Player Data Storage / Title Storage**; P2P và Lobby **không** dùng — peer khác khoá vẫn nối được. Config PEW vẫn đòi một giá trị 64-hex hợp lệ | Có (`Clients[]` dài 1) | Doc comment SDK "Used by Player Data Storage and Title Storage ... 256-bit Encryption Key" `KT:Library/PackageCache/com.playeveryware.eos@*/Runtime/EOS_SDK/Generated/Platform/Options.cs:40`; `OPTIONS_ENCRYPTIONKEY_LENGTH = 64` (`.../Generated/Platform/PlatformInterface.cs:83`); PlayEveryWare `configure_plugin.md` |
| 8 | EAS application (chỉ khi dùng Dev Auth Tool hoặc Epic login) | Tạo application → 3 tab **Brand Settings** · **Permissions** · **Linked Clients**. Linked Clients: chọn client bước 7 → Save. Một client chỉ link được 1 application | Có — link client vào app, chỉ lưu Basic Profile; Brand Settings để trống vẫn chạy được cho test nội bộ | `KT:docs/multiplayer-testing/eos-dev-auth.md:37` |
| 9 | Permissions | **Basic Profile** luôn bắt buộc (không tắt được). Không bật Friends/Presence nếu không dùng — bạn bè/presence nằm ở backend game riêng (`01` §1) | KT chỉ lưu Basic Profile ở Portal | Docs; KT doc :37 |
| 10 | Consent lần đầu | Mỗi tài khoản Epic lần đầu login qua Dev Auth/Epic phải đồng ý Basic Profile trên trình duyệt | Có — consent có thể mở nhầm profile Chrome; timeout login 12 s của app có thể hết trong lúc bấm consent → chạy lại sau khi consent xong | `KT:docs/multiplayer-testing/eos-dev-auth.md:36` |
| 11 | Brand / review công khai | Chỉ cần khi mở Epic login cho người dùng thật | Chưa làm (KT không cần vì production dùng DeviceID) | KT doc :37 |

**Không cần** cho kiến trúc này: Sessions interface, matchmaking EOS, EOS Custom Invites, Friends/Presence EOS, Anti-Cheat EOS (xem `06-optional-modules.md` §9).

## 2. File config trong build (PlayEveryWare EOS plugin) [KT-SPECIFIC → mapping GENERIC]

KT dùng `com.playeveryware.eos` 6.1.0 (`KT:Packages/manifest.json:6`). Menu Editor của plugin: **EOS Plugin → EOS Configuration** (docs plugin). File nằm ở `Assets/StreamingAssets/EOS/` vì native plugin phải đọc trước khi Unity bootstrap xong.

| File | Vai trò |
|---|---|
| `eos_product_config.json` | Danh tính product + danh sách client/sandbox/deployment (nhiều môi trường) |
| `eos_<platform>_config.json` (`android`, `ios`, `windows`, `macos`, `linux`) | Bộ giá trị **đang dùng** cho nền tảng đó + cờ platform |
| `eos_steam_config.json` | Chỉ khi tích hợp Steam (KT không dùng) |

### 2.1 `eos_product_config.json` — tên trường (KHÔNG giá trị)

| Trường | Ý nghĩa | Lấy từ Portal |
|---|---|---|
| `ProductName` | Tên product | Product Settings |
| `ProductId` | Id product | Product Settings |
| `ProductVersion` | Chuỗi phiên bản app gửi lên EOS (plugin bắt buộc khác rỗng) | Tự đặt |
| `Clients[].Name` | Nhãn client | Tự đặt |
| `Clients[].Value.ClientId` / `.ClientSecret` | Credential client | Clients |
| `Clients[].Value.EncryptionKey` | Khoá mã hoá Player/Title Data Storage (64 hex); không ảnh hưởng P2P/Lobby | Dev tự sinh (§1 bước 7) |
| `Environments.Sandboxes[].Name` / `.Value.Value` | Nhãn + Sandbox Id | Sandbox tab |
| `Environments.Deployments[].Name` / `.Value.SandboxId.Value` / `.Value.DeploymentId` | Deployment + sandbox cha | Sandbox tab |
| `imported` | Cờ nội bộ plugin (đã import) | — |
| `schemaVersion` | Phiên bản schema file | — |

### 2.2 `eos_<platform>_config.json` — tên trường

| Trường | Ý nghĩa | Giá trị KT (không nhạy cảm) |
|---|---|---|
| `deployment.SandboxId.Value`, `deployment.DeploymentId` | Môi trường platform này dùng | (id — không ghi) |
| `clientCredentials.ClientId/ClientSecret/EncryptionKey` | Credential platform này dùng | (secret — không ghi) |
| `isServer` | Platform là server tin cậy? Client game **phải `false`** | `false` |
| `platformOptionsFlags` | Cờ `EOS_PF_*` (tắt overlay…) | `None` |
| `authScopeOptionsFlags` | Scope Auth (chỉ tác dụng với Epic login/Dev Auth) | `BasicProfile, FriendsList, Presence` — xem lưu ý dưới |
| `integratedPlatformManagementFlags` | Tích hợp nền tảng (Steam/console) | `Disabled` |
| `tickBudgetInMilliseconds` | Ngân sách tick SDK (0 = mặc định) | `0` |
| `taskNetworkTimeoutSeconds` | Timeout tác vụ mạng (0 = mặc định) | `0` |
| `threadAffinity.{NetworkWork,StorageIo,WebSocketIo,P2PIo,HttpRequestIo,RTCIo,EmbeddedOverlayMainThread,EmbeddedOverlayWorkerThreads}` | Ghim thread (0 = mặc định) | `0` |
| `alwaysSendInputToOverlay`, `initialButtonDelayForOverlay`, `repeatButtonDelayForOverlay`, `toggleFriendsButtonCombination` | Overlay Epic (console/desktop) | mặc định |
| `GoogleLoginClientID`, `GoogleLoginNonce` (chỉ Android) | Login Google qua EOS | null |
| `schemaVersion` | — | `1.0` |

Lưu ý scope: config KT xin `FriendsList, Presence` nhưng Portal chỉ bật Basic Profile và code Dev Auth ghi đè `AuthScopeFlags.BasicProfile` (`KT:Assets/Scripts/Net/DevelopmentEosAuth.cs:90`). Dự án mới: để scope = `BasicProfile` cho khớp Portal (xin scope chưa được cấp có thể làm consent/login lỗi — CHƯA KIỂM CHỨNG).

### 2.3 Engine khác (Cocos native) [GENERIC]
Cùng bộ giá trị đưa vào `EOS_Platform_Options` (`ProductId`, `SandboxId`, `DeploymentId`, `ClientCredentials{ClientId, ClientSecret}`, `EncryptionKey`, `bIsServer=false`, `Flags`, `TickBudgetInMilliseconds`). Định dạng file tự chọn; quy tắc secret ở §3 giữ nguyên.

## 3. Secret handling [GENERIC]

| Sự thật | Hệ quả |
|---|---|
| Client Secret **buộc** nằm trong build client (P2P không có server trung gian để giữ) | Coi như **công khai**: ai giải nén APK/IPA cũng đọc được |
| Bảo vệ thật nằm ở **client policy** | Custom policy quyền tối thiểu (§1 bước 5-6), `User required` bật; không cấp quyền trusted-server |
| Encryption Key chỉ mã hoá Player/Title Data Storage | Không dùng storage → không phải tài sản bảo vệ; dùng → cũng coi là lộ |
| Lộ credential | Xoay vòng: tạo client mới trên Portal → build mới → xoá client cũ khi bản cũ hết dùng |

Quy tắc repo:
1. **Không commit** file config chứa giá trị vào repo public. KT đang **track** toàn bộ `Assets/StreamingAssets/EOS/*.json` (repo private) — chấp nhận được cho repo private, **đừng chép** sang repo public/skill/SDK.
2. Cách tách khỏi git: commit `eos_*_config.template.json` (tên trường, giá trị rỗng) + `.gitignore` file thật; CI/máy build sinh file thật từ biến môi trường/secret store trước khi build. Hoặc `git update-index --skip-worktree` cho file đã track (chỉ tạm, dễ quên).
3. Không bao giờ in Client Secret/Encryption Key ra log hay telemetry; PUID/lobby id trong log dev là dữ liệu cá nhân nhẹ — không dán vào docs public.
4. Dev Auth: chỉ lưu **nhãn credential** của tool, không bao giờ mật khẩu/token Epic (`KT:docs/multiplayer-testing/eos-dev-auth.md:28`).

## 4. Auth

### 4.1 Production: Connect DeviceID [GENERIC]

| Mục | Quy tắc |
|---|---|
| Luồng | `Connect.Login(DeviceidAccessToken)` → chưa có device id thì `CreateDeviceId` → login lại → `InvalidUser` thì `CreateUser(continuanceToken)` → PUID. Chi tiết + timeout: `01` §4.1, `03` §11 |
| Bản chất | Pseudo-account theo **thiết bị / OS user**, không gắn tài khoản người dùng. Không cần EAS application, không consent |
| Hệ quả | 2 tài khoản game trên cùng máy/OS user = **cùng PUID** (bẫy test H1, `04`). Xoá app/đổi máy ⇒ PUID mới (theo docs: id gắn profile máy; với Android xoá data — CHƯA KIỂM CHỨNG) |
| Danh tính bền | uid backend game, publish riêng trong member attr cho hành động xã hội (`04` A5). PUID chỉ là danh tính transport/roster |
| KT wiring | FishyEOS trên `Boot.unity`: `loginCredentialType: 3` (= `DeviceCode` — FishyEOS dùng giá trị này để rẽ vào nhánh DeviceID), `externalCredentialType: 10` (`DeviceidAccessToken`), `automaticallyCreateDeviceId: 1`, `automaticallyCreateConnectAccount: 1`, `autoAuthenticate: 0` (app tự login trong boot), `timeout: 30` — `KT:Assets/Scenes/Boot.unity:388-401`, `KT:Assets/FishNet/Plugins/FishyEOS/Util/Coroutines/AuthDataLogin.cs:36-38` |

### 4.2 Test: Developer Authentication Tool [GENERIC + KT-SPECIFIC]

| Mục | Quy tắc |
|---|---|
| Khi nào | Cần ≥2 PUID khác nhau trên **một** máy/OS user (Editor + MPPM, 2 process desktop) |
| Tool | Chạy EOS Dev Auth Tool (trong EOS SDK zip) trên máy, chọn port (KT mặc định 8888), login từng tài khoản Epic, đặt **tên credential** cho mỗi cái |
| Code | `Auth.Login` với `LoginCredentialType.Developer`, `Id = "localhost:<port>"`, `Token = <tên credential>`, scope `BasicProfile`; sau đó `Connect.Login` với `ExternalCredentialType.Epic` (access token) — **không** `EpicIdToken` (`KT:Assets/Scripts/Net/DevelopmentEosAuth.cs:85-92`) |
| 2 identity | (a) 2 **tài khoản Epic khác nhau** trong tool (2 nhãn trỏ cùng 1 account ⇒ cùng PUID); hoặc (b) **trộn**: máy chính DeviceID + player 2 Dev Auth — khác provider ⇒ PUID khác, chỉ cần 1 tài khoản Epic (`DevelopmentEosAuth.cs:110-116`, verify 2026-09-23 `KT:docs/multiplayer-testing/doc.md:36-38`) |
| Fail-closed | Bật Dev Auth mà endpoint/nhãn sai hoặc tool tắt ⇒ **báo lỗi**, không lặng lẽ rơi về DeviceID (`DevelopmentEosAuth.cs:62-79`, `KT:Assets/Scripts/Editor/EosDevAuthWindow.cs:37-41`) |
| Chặn rò vào production | Code Dev Auth chỉ biên dịch trong `UNITY_EDITOR || (DEVELOPMENT_BUILD && UNITY_STANDALONE)`; endpoint chỉ nhận `localhost`/`127.0.0.1` (`DevelopmentEosAuth.cs:31,66-73`). Mobile/release bỏ qua hoàn toàn |
| Prerequisite Portal | §1 bước 1, 8, 9, 10 |

Quy trình test cụ thể: `07-testing-playbook.md` §2.

## 5. Lỗi thường gặp

| Triệu chứng / thông điệp | Nguyên nhân | Cách xử lý | Nguồn |
|---|---|---|---|
| Auth trả `EOS_InvalidRequest` / HTTP 400: `The Client is not configured correctly. Please make sure it is associated with an Application.` | Client chưa link vào EAS application (chỉ xảy ra với Dev Auth/Epic login) | Portal: application → Linked Clients → chọn client → Save; bật Basic Profile | `KT:docs/multiplayer-testing/eos-dev-auth.md:38` |
| Connect lỗi `UserLoginInfo.DisplayName ... must not be set for this credential type` sau khi Epic Auth OK | Gửi `UserLoginInfo` cùng credential `Epic`/`EpicIdToken` (FishyEOS gốc luôn gửi) | Bỏ `UserLoginInfo` cho credential Epic; giữ cho provider khác (DeviceID cần DisplayName) | `04` A6; `KT:Assets/FishNet/Plugins/FishyEOS/Util/Coroutines/ConnectLogin.cs` |
| Dev Auth login thất bại dù tool chạy | Tài khoản Epic chưa accept org / chưa consent Basic Profile; nhãn sai chính tả | Accept lời mời org; login lại để consent; nhãn khớp chính xác (phân biệt hoa thường) | `eos-dev-auth.md:35-36` |
| Consent xong mà game vẫn timeout | Timeout boot (KT 12 s) hết trong lúc bấm consent | Chạy lại phiên sau khi consent | `eos-dev-auth.md:36` |
| 2 client "cùng vào phòng" nhưng lobby 1 member, cả hai là owner | Cùng PUID (DeviceID chung máy) | Dev Auth 2 identity (§4.2) | `04` H1 |
| Login treo không callback | Login gọi cùng call khởi tạo platform; timeout SDK chạy bằng `Time.time` | Boot task riêng + timeout realtime của app | `04` A1, `03` §11 |
| Lobby/P2P/voice bị từ chối quyền (`InvalidAuth`/`AccessDenied`-loại) | Client policy thiếu action | Thêm action vào custom policy (§1 bước 6) | Docs; CHƯA KIỂM CHỨNG thông điệp chính xác |
| Không tìm thấy phòng của máy kia | Hai build khác **sandbox/deployment** (Dev vs Stage) hoặc khác bucket | So `deployment` + bucket trong log hai phía | [GENERIC] suy luận từ mô hình sandbox; CHƯA KIỂM CHỨNG ở KT |
