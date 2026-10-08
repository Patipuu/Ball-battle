# 07 — Testing playbook: 2+ người chơi trên 1 máy và trên thiết bị thật

> Mục đích: quy trình test multiplayer lặp lại được (1 máy + 2 thiết bị) và danh sách bẫy đã làm KT "pass giả".
> Baseline: KT commit `7d96acd5` (HEAD 2026-10-05).
> Đọc khi: trước khi tuyên bố một thay đổi netcode/lobby là xong. Đối tượng: dev, QA, agent tự verify.

Nhãn: **[GENERIC]** · **[KT-SPECIFIC]** (lệnh/menu riêng KT, dự án mới tự dựng tương đương) · **CHƯA KIỂM CHỨNG**.
Prerequisite: Portal + Dev Auth đã cấu hình theo `05-eos-portal-and-config.md` §1, §4.2.

## 0. Định nghĩa "test 2 người hợp lệ" [GENERIC]

Một phiên chỉ được tính khi **tất cả** đúng (kiểm trong log cả hai phía):

| # | Điều kiện | Vì sao |
|---|---|---|
| 1 | Hai PUID **khác nhau** | Cùng PUID = lobby 1 member, cả 2 nghĩ mình là owner (`04` H1) |
| 2 | Cùng lobby id, member count = 2, **đúng 1 owner** | Hai máy tự tạo 2 phòng riêng là lỗi phổ biến (`04` H3) |
| 3 | Transport client `Started` + host thấy connection | Lobby chạy trên attribute nên che mất link P2P chết (`04` E2) |
| 4 | Mỗi peer có player của mình spawn + round active | Lobby OK ≠ gameplay OK (`04` D1) |
| 5 | Đi qua **UI thật** (nút touch/HUD), không phím tắt/inject | Editor và device phải cùng code path (§5) |

## 1. Ma trận chọn rig

| Rig | Identity | Dùng cho | Không chứng minh được |
|---|---|---|---|
| A. Editor + MPPM clone, 2 tài khoản Epic | Dev Auth × 2 | Lobby, barrier, spawn, rematch, nhiều phiên host | NAT/relay thật, background mobile, quyền mic, touch device |
| B. Editor (DeviceID) + MPPM (Dev Auth) | Trộn provider, 1 tài khoản Epic | Như A, setup nhanh hơn | Như A |
| C. 2 process desktop development build | Dev Auth × 2 qua CLI | AutoPilot dài (nhiều level), không giữ Editor | Như A |
| D. Editor host + 1-2 thiết bị Android/iOS | Editor: DeviceID/Dev Auth; thiết bị: DeviceID | Relay/NAT thật, background, mic, touch | Cross-platform nếu chỉ một OS |
| E. 2 thiết bị / 2 emulator, build giống nhau | DeviceID (khác máy ⇒ khác PUID) | Gate cuối trước khi tuyên bố xong | — |

## 2. Quy trình 1 máy

### 2.1 Rig A — Editor + MPPM, 2 tài khoản Epic [GENERIC; menu KT-SPECIFIC]

| Bước | Làm gì | Kiểm |
|---|---|---|
| 1 | Chạy EOS Dev Auth Tool, chọn port (KT mặc định 8888) | Tool hiện `localhost:<port>` |
| 2 | Login **2 tài khoản Epic khác nhau**, đặt 2 tên credential (KT mặc định `Player1`, `Player2`) | Hai credential hiện Epic account id khác nhau |
| 3 | Lần đầu mỗi tài khoản: accept org + consent Basic Profile (`05` §1 bước 1, 10) | Trang consent hiện đúng tên tài khoản |
| 4 | **Dừng mọi player**. KT: menu `KT > Multiplayer > EOS Dev Auth` → bật, nhập port + 2 nhãn | Nhãn trùng/không hợp lệ bị từ chối trước khi lưu |
| 5 | Unity MPPM (KT `com.unity.multiplayer.playmode` 2.0.2): chọn scenario **2 Players**, mở scene `Boot` trước khi Play (clone không thừa hưởng scene rỗng) | Clone nhận arg `-name "Player 2"` → map sang nhãn 2; tên lạ ⇒ **fail closed** |
| 6 | Play. Login tài khoản game bình thường ở cả hai | Log: 2 PUID khác nhau |
| 7 | Máy 1 tạo phòng → máy 2 join (code/random) → Ready qua **nút UI thật** | §0 đủ 5 điều kiện |
| 8 | Chơi hết round → về lobby → **rematch** → hết round lần 2 | Bẫy T4/T5 (§4) |

Nguồn: `KT:docs/multiplayer-testing/eos-dev-auth.md:3-9,25-31`, `KT:Assets/Scripts/Net/DevelopmentEosAuth.cs:118-133` (map `-name "Player N"`), `KT:Assets/Scripts/Editor/EosDevAuthWindow.cs:9-41`. Verify live 2026-09-04: 2 PUID khác nhau, 1 lobby 2 member 1 owner, cả hai vào level với 2 player spawn + round active; voice/reconnect chưa verify trong lần đó (`eos-dev-auth.md:35`).

### 2.2 Rig B — trộn DeviceID + Dev Auth (1 tài khoản Epic)
Như A nhưng bật **Main Editor uses DeviceID**: Editor chính login DeviceID ẩn danh, chỉ MPPM player dùng Dev Auth → khác provider ⇒ PUID khác (`KT:Assets/Scripts/Net/DevelopmentEosAuth.cs:110-116`; verify 2026-09-23, `KT:docs/multiplayer-testing/doc.md:36-38`). Lưu ý: PUID DeviceID của Editor trùng với mọi build desktop khác chạy cùng OS user.

### 2.3 Rig C — 2 process desktop + AutoPilot [KT-SPECIFIC, làm mẫu]

```
# build development player (Dev Auth bị bỏ qua ở build non-development)
Unity -batchmode -quit -projectPath "$PWD" -executeMethod <BuildDevPlayer> -logFile <tmp>/build.log
# process A host, process B client; instance riêng để tách save game
open -n <App> --args -ktInstance=a -ktAuto=host   -ktEosDevAuth=localhost:8888 -ktEosCredential=Player1 -logFile <tmp>/a.log
open -n <App> --args -ktInstance=b -ktAuto=client -ktEosDevAuth=localhost:8888 -ktEosCredential=Player2 -logFile <tmp>/b.log
```
Tiêu chí pass của AutoPilot: **cả hai** log in `[AutoPilot] RESULT PASS …`. KT còn có điều khiển Editor qua file cờ trong `$TMPDIR` (arm/play/stop/refresh/tests) cho MPPM. Nguồn: `KT:docs/multiplayer-testing/doc.md:21-42`. Dự án mới: dựng tương đương — cờ CLI `-autoHost/-autoJoin`, `-instance` tách save, `-devAuthEndpoint/-devAuthCredential`, kết quả PASS/FAIL một dòng grep được ở mỗi process.

## 3. Quy trình 2 thiết bị [GENERIC]

| Bước | Làm gì | Ghi chú |
|---|---|---|
| 1 | Cài **cùng một** build (cùng sandbox/deployment, cùng bundle) lên cả hai; ghi version + hash APK | Khác build ⇒ random match lọc theo bundle loại nhau (`06` §5) |
| 2 | Bật telemetry dev ở cả hai phía (§6) | Không có log hai phía = không phân xử được host hay guest sai |
| 3 | Force-stop app trên thiết bị trước mỗi lượt (`adb shell am force-stop <pkg>`) | Xoá peer/lobby cũ còn sống trong process |
| 4 | **Khởi động lệch nhịp**: host lên trước → thiết bị 1 vào → **chờ roster = 2** → mới mở thiết bị 2 | Mở cùng lúc ⇒ auto-join chạy trước khi phòng host tồn tại, mỗi máy tự tạo phòng (`04` H3) |
| 5 | Join bằng code qua UI thật (adb `input tap`/`input text` được; tọa độ theo display logic) | Thiết bị khoá màn hình ⇒ launch lặng lẽ fail: wake + dismiss keyguard trước |
| 6 | Chạy đủ round, đổi vai host ↔ guest và chạy lại | KT 2026-09-22: lỗi kick do RPC skin chỉ lộ khi có remote player thật (`KT:docs/multiplayer-testing/lobby-social-and-start.md:12-15,38-45`) |
| 7 | Kịch bản mobile: background guest ~40 s rồi quay lại; tắt/bật mạng; kill app rồi mở lại (rejoin) | Background dừng tick EOS, link P2P chết lặng (`KT:Assets/Scripts/Net/AppLifecycleRecovery.cs:17-22`); KT đo bg+40 s → reconnect ~12 s cùng clientId (`04` E3). Ngưỡng ">30 s mất P2P" CHƯA KIỂM CHỨNG như hằng số |
| 8 | Voice (nếu bật): cấp quyền mới / từ chối / đã có quyền / đổi thiết bị âm thanh lúc unmute đầu | Editor luôn "có quyền" — không phải bằng chứng (`KT:docs/voice-chat/doc.md:39-40,70-72`) |
| 9 | Thu bằng chứng: log hai phía + ảnh chụp kết quả hai phía | §7 |

## 4. Bẫy test (đã làm KT pass giả)

| # | Bẫy | Biểu hiện | Cách tránh | Nguồn |
|---|---|---|---|---|
| T1 | 2 tài khoản game, 1 OS user ⇒ chung DeviceID PUID | Lobby 1 member, 2 owner | Dev Auth 2 identity; kiểm §0 #1-2 | `04` H1 |
| T2 | 2 nhãn Dev Auth trỏ cùng tài khoản Epic | PUID trùng dù đã "bật Dev Auth" | 2 tài khoản Epic khác nhau hoặc rig B | `eos-dev-auth.md:30` |
| T3 | Đổi setting Dev Auth khi player đang chạy | Identity đổi giữa phiên | Dừng mọi player; setting snapshot 1 lần/Play | `DevelopmentEosAuth.cs:26-27,163-164` |
| T4 | Chỉ test trận đầu sau khi mở app | Lỗi static per-process (id map, latch) chỉ lộ ở trận 2+ | Luôn ≥2 trận liên tiếp trong cùng process, có rematch và đổi host | `04` C1, H4 |
| T5 | Editor tắt domain reload | Latch static mang sang lần Play sau (voice chết, cooldown kẹt) | Mọi static có reset `SubsystemRegistration` | `04` F3; `KT:Assets/Scripts/Net/Invite/InviteSession.cs:202-209` |
| T6 | State EOS kẹt sau nhiều lần Play | Thiết bị vào lobby nhưng transport kẹt `Starting`, không hết khi restart Play/app/reboot | **Thoát hẳn và mở lại Editor** (platform EOS sống theo process Editor) | `04` H2 |
| T7 | Không force-stop app giữa các lượt | Peer reuse cũ: lobby thấy nhau, host không thấy client FishNet | Force-stop app + restart host | memory `unity-mcp-editor-workflow` |
| T8 | Mở 2 thiết bị cùng lúc | Hai phòng riêng | Lệch nhịp §3 bước 4 | `04` H3 |
| T9 | Background mobile | Link chết không có callback, FishNet vẫn `Started` | Kịch bản §3 bước 7 bắt buộc trên thiết bị | `KT:Assets/Scripts/Net/AppLifecycleRecovery.cs:17-22` |
| T10 | Verify bằng bàn phím trên Editor | Pass trên Editor, thiết bị thiếu/sai nút | Bấm nút UI thật (script gọi `onClick` của nút thật) | `04` H7 |
| T11 | Tin test xanh cho lỗi init-order FishNet | Editor **tự thêm `NetworkObject`** khi add component mạng ⇒ test không tái hiện | Verify bằng telemetry thiết bị | `04` H6 |
| T12 | Script tạo NetworkObject trong scene không có sceneId | Object bị despawn/không đăng ký | Tạo sceneId rồi save scene; đếm object đăng ký | `04` D8 |
| T13 | Log một phía | Đổ lỗi NAT/mạng sai | Telemetry 2 phía, ghép timeline theo lobby id + timestamp | `04` H8 |
| T14 | Sửa code path không chạy | "Đã sửa" nhiều lần, người test vẫn thấy lỗi | Đọc giá trị serialized của prefab/scene thật sự render trước khi sửa; triệu chứng lặp ⇒ đọc object sống | `04` H9 |
| T15 | Suite async chặn main thread | Full suite treo | Test async không `.Wait()` trên main thread; treo lâu CPU thấp ⇒ nghi deadlock | `04` H5 |
| T16 | Consent Epic lần đầu trong lúc boot | Timeout login, tưởng Dev Auth hỏng | Consent xong chạy lại | `05` §5 |
| T17 | Hai build khác sandbox/bundle | Không thấy phòng nhau | So deployment + bundle trong log | `05` §5 |

## 5. Test qua UI touch thật [GENERIC]

- Editor phải chạy **cùng** lớp input/nút touch như thiết bị (input hybrid: touch panel + bàn phím merge; thiết bị không bàn phím ⇒ thuần touch).
- Script verify tự động: tìm nút UI thật và gọi `onClick.Invoke()`; không bơm trực tiếp vào interface input — trừ test logic thuần.
- Đối chiếu luật ẩn/hiện nút với nguồn sự thật thiết kế (KT: bản Godot), kiểm trên thiết bị trước khi tuyên bố.
Nguồn: memory `test-via-touch-ui-not-keyboard` (quy tắc cứng 2026-07-21).

## 6. Telemetry dev hai phía [GENERIC pattern; KT-SPECIFIC hiện thực]

| Thành phần | Pattern |
|---|---|
| Client shipper | Chỉ biên dịch sau một define dev riêng (KT `KT_DEV_TELEMETRY`), release **không** define. Gửi: manifest phiên (model máy, version, PUID), mọi dòng log Unity (hook threaded), heartbeat 5 s (RTT, scene, kiểu NAT, roomId) |
| Endpoint + token | Ở **một** file config duy nhất; không hardcode rải rác; token đổi được |
| Collector | HTTP không dependency: `GET /health`; `POST /ingest` có header token; body `{"records":[{type, sessionId, ts, …}]}`; giới hạn body (KT 1 MB); `sessionId` regex chặt; record xấu bị **drop**, không 500; ghi append JSONL `logs/<YYMMDD>/<sessionId>.jsonl` + `sessions-index.jsonl` cho `session_start/update` |
| Đọc | Tra `sessions-index` theo tài khoản/thiết bị → mở file phiên → ghép timeline 2 phía theo dòng join lobby + timestamp |
| Gỡ / dời | Đánh dấu mọi chỗ bằng một tag grep được (KT `DEV-TELEMETRY-REMOVE`) |
| Vận hành | Có kế hoạch retention/nén (KT ~1 GB/ngày, chưa xoay vòng); không để PUID/IP người chơi lọt vào docs public |

Nguồn: `KT:tools/dev-telemetry/collector.mjs:14-20,25-31,60-92`, `KT:Assets/Scripts/Core/DevTelemetryShipper.cs:1-3,98-106`, memory `dev-telemetry-pipeline`. Địa chỉ host collector của KT **không** ghi ở đây.

## 7. Gate trước khi tuyên bố "xong" [GENERIC]

| # | Bắt buộc | Bằng chứng ghi lại |
|---|---|---|
| 1 | Unit/EditMode test liên quan xanh (chạy hẹp trước, rộng sau) | File kết quả + số pass/fail |
| 2 | Rig A hoặc B: §0 đủ 5 điều kiện, ≥2 trận + rematch | Log 2 phía (PUID, lobby, owner, spawn) |
| 3 | Rig D/E trên **thiết bị thật** cho mọi thay đổi transport, lifecycle, voice, input | Version + hash build, log 2 phía, ảnh kết quả 2 phía |
| 4 | Đổi vai host ↔ guest | Như trên |
| 5 | Ghi rõ cái **chưa** verify (voice 2 chiều, cross-platform iOS↔Android, reconnect…) | Mục "Chưa kiểm chứng" trong báo cáo |

Không tuyên bố đã sửa khi chỉ có test Editor cho lỗi người test thấy trên thiết bị; nêu rõ bề mặt đã kiểm (Editor Game View / prefab asset / build thiết bị).
