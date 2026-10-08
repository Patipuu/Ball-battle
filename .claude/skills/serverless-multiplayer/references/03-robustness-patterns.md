# 03 — Robustness patterns: sống sót với EOS Lobby bất đồng bộ
> Mục đích: giải thích engine-agnostic các pattern độ tin cậy mà KT dùng để chịu được EOS Lobby (callback, eventually consistent, UpdateLobby bị rate-limit, chỉ polling) + EOS P2P, để port sang Unity 6 / Cocos native.
> Baseline: KT commit `7d96acd5` (HEAD 2026-10-05); mọi `file:line` đọc trực tiếp từ source.
> Đối tượng: dev net/gameplay của project mới đang dựng host-on-device + EOS Lobby làm room store.

**Quy ước.** `KT:Net/X.cs:N` = `Assets/Scripts/Net/X.cs` dòng N; `KT:FishyEOS/...` = `Assets/FishNet/Plugins/FishyEOS/...`.
`[GENERIC]` = áp dụng cho mọi backend lobby callback-based; `[KT-SPECIFIC]` = chi tiết/hằng số riêng KT, game khác phải đo lại.
Pseudo-code là ngôn ngữ trung tính, không phải C#.

## Bản đồ nhanh

| # | Pattern | Diệt lớp bug nào | Nhãn |
|---|---|---|---|
| 1 | GenerationGuard | callback muộn ghi đè state mới | GENERIC |
| 2 | Watchdog + orphan-leave | await treo vĩnh viễn; phòng ma trên backend | GENERIC |
| 3 | Single-flight intent | 2 phòng cùng lúc, `LobbyAlreadyExists` | GENERIC |
| 4 | Ownership / lease / fence | leave cũ đè join mới; ghi epoch cũ làm phòng kẹt IN_MATCH | GENERIC (cơ chế) / KT-SPECIFIC (độ chi tiết) |
| 5 | Write discipline | "callback OK" nhưng snapshot chưa đổi / callback lỗi nhưng backend đã đổi | GENERIC |
| 6 | Read discipline | đọc rỗng → tưởng mọi người đã rời | GENERIC |
| 7 | Polling cadence | rate-limit, UI giật | KT-SPECIFIC |
| 8 | Timeout & retry table | — | KT-SPECIFIC |
| 9 | Reconnect loop | tự re-dial bị hiểu là host đóng phòng | GENERIC |
| 10 | Liveness probe | `Started=true` trên link chết | GENERIC |
| 11 | Auth bootstrap | login mất callback, intent treo | GENERIC + EOS quirks |
| 12 | Diagnostics | lỗi im lặng | GENERIC |

---

## 1. GenerationGuard `[GENERIC]`

**Vấn đề.** Callback EOS có thể về sau khi user đã bấm Leave, đổi scene, hoặc bắt đầu op khác. Áp kết quả đó = state sai (join phòng đã bỏ, UI nhảy lùi).

**Cơ chế.** Mỗi op lấy token `gen = BeginOperation()` (bump counter). Khi kết quả về, `IsStale(gen)` → drop nếu:

| Điều kiện drop | Nguồn |
|---|---|
| `gen != current` (op mới hơn đã chen) | `KT:Net/GenerationGuard.cs:68` |
| lifecycle đã `Invalidate` (terminal, không mở lại được) | `:73`, `Invalidate` `:101` |
| teardown đang chạy (`BeginTeardown`; tự clear ở `BeginOperation` kế tiếp) | `:78`, `:95`, `:48` |
| đang scene transition (`PrepareSceneTransition` cũng bump gen) | `:83`, `:113` |

**Invariants.**
- Mọi op lobby đi qua guard (wired vào `EosLobbyService`, `KT:Net/GenerationGuard.cs:17`).
- `Invalidate` là một chiều: `BeginOperation` trên guard đã invalid trả gen cũ và log từ chối (`:43`).
- Kết quả stale **nhưng thành công** (create/join đã thực sự xảy ra trên backend) không được chỉ "bỏ qua" — phải orphan-leave (xem §2, §3).

```
gen = guard.BeginOperation("join")
res = await lobby.Join(id)
if guard.IsStale(gen):
    if res.ok: orphanLeave(res.lobbyId, onlyIf = noNewerMembership(gen))
    return
apply(res)
```

## 2. Watchdog — mọi async EOS phải có cận trên `[GENERIC]`

**Vấn đề.** `EosLobbyService` hoàn thành `TaskCompletionSource` *chỉ* từ callback EOS. Mất mạng / platform không tick / EOS chưa config → callback không bao giờ đến, task không complete, không throw, UI kẹt "Creating room…" (`KT:Net/LobbyConnectWatchdog.cs:10-14`).

**Cơ chế.** Race `operation` vs `timer` (`LobbyConnectWatchdog.RunAsync`, `KT:Net/LobbyConnectWatchdog.cs:49`):
- Op xong trước (kể cả cùng tick) → thắng, huỷ timer (`:62-64`).
- Timer thắng → trả `Fail("timeout")`, giao task còn chạy cho `onTimeout` (`:75-76`).
- Caller (`ProductLobbySession.WithWatchdogAsync`, `KT:Net/ProductLobbySession.cs:551`) trong `onTimeout`: bump `_opGeneration` (`:572`) rồi `ObserveLateJoinTaskAsync` — nếu late success → `RollbackJoinIfSafeAsync` chỉ khi không có membership mới hơn gen đã timeout (`:592-610`).

**Invariants.**
- **Luật: không có `await` EOS trần.** Host/join-random từng await trần vì "đã có auth budget" — budget đó không tồn tại (`KT:Net/ProductLobbySession.cs:45-52`).
- Op EOS không huỷ được → timeout ≠ thất bại thật; luôn quan sát kết quả muộn.
- Orphan-leave không bao giờ được kick membership mới hơn (so gen).
- Cancel của caller trong lúc chờ phải báo là `cancelled`, không phải `timeout` (`:577-581`).

```
timer = delay(T)
winner = await any(op, timer)
if op.done: cancel(timer); return op.result
gen.bump()
observeLate(op, timedOutGen) -> if ok && !newerMembership: leave(op.lobbyId)
return Fail("timeout")
```

## 3. Single-flight intent `[GENERIC]`

**Vấn đề.** Bấm 2 lần / auto-host + UI cùng drive → 2 lobby, user thành member của phòng không ai track, mọi create sau đó fail `LobbyLobbyAlreadyExists` (`KT:Net/RoomIntentCoordinator.cs:173-177`).

**Cơ chế** (`RoomIntentCoordinator`):

| Luật | Nguồn |
|---|---|
| Một intent in-flight; intent thứ 2 bị **ignore** (không queue) | `Busy` `KT:Net/RoomIntentCoordinator.cs:179`, `Dispatch` `:189-193` |
| Mỗi intent mang `generation` riêng; `Superseded(gen)` | `Run` `:221-226`, `:229` |
| `LeaveRoom` bump generation → leave **vượt** intent đang chạy | `:151-165` |
| Kết quả về muộn của intent bị vượt → `Abandon` = leave phòng đó, giữ task vào `_leaveInFlight` | `:331-340` |
| Intent kế tiếp `DrainLeaveAsync` trước (EOS từ chối create khi còn là member) | `:237`, `:270-284` |
| Intent khi auth chưa xong → queue 1 cái, replay lúc `OnAuthenticated`; auth fail → `JoinFailed` | `:101-127`, `:210-216` |
| Exception trong fire-and-forget phải thành `JoinFailed`, không nuốt | `:257-262` |
| Product shell active → refuse Create/JoinRandom ở coordinator gym (một owner duy nhất) | `:198-203` |

Product path có gate tương đương: `ProductLobbySession` từ chối connect khi `_connectInFlight` (`KT:Net/ProductLobbySession.cs:363`) và **chờ leave idle ≤10s** trước connect (`:352`, `WaitForLeaveIdleAsync` `:524`).
`LobbySessionIntent` chỉ là one-shot shell state (Host/JoinById/…) được reset ở `SubsystemRegistration` để intent cũ không auto-connect phòng chết (`KT:Net/LobbySessionIntent.cs:185-196`) — không phải bộ single-flight. *(sửa hint scout)*

```
dispatch(intent):
  if authError: fail(authError); return
  if busy(): log("ignored"); return
  if !authed: queued = intent; return
  gen = ++intentGen; inFlight = run(intent, gen)
run(intent, gen):
  await drainLeave()
  if gen != intentGen: return
  r = await lobbyOp(intent)
  if gen != intentGen: if r.ok: leaveInFlight = leave(r.id); return
  commit(r)
```

## 4. Operation ownership / lease / fence `[GENERIC cơ chế, KT-SPECIFIC chi tiết]`

**Vấn đề.** Leave/cleanup EOS là uncancellable và có thể về muộn. Nếu service bị thay (scene reload, domain reload tắt) mà service mới join lại *cùng PUID + cùng lobby*, leave cũ landing sau sẽ đá membership mới. Tương tự, write "begin match" của epoch cũ landing sau Leave để lại phòng **kẹt IN_MATCH** cho người còn lại (`KT:Net/ProductLobbySession.cs:673-675`).

**Cơ chế.** Registry **static** keyed `(PUID, lobbyId)`, sống qua việc thay service (`KT:Net/EosLobbyOperationOwnership.cs:7-12`). Mọi op có `operation` id tăng đơn điệu (`NextOperation` `:173`).

| Khái niệm | Ý nghĩa | Nguồn |
|---|---|---|
| `TryCommitMembership` | Chỉ commit nếu op ≥ floor, không có reservation mới hơn, không PendingLeave, không mutation lease | `:213-235` |
| Entry reservation | Join đặt chỗ trước; `ResetLifecycle` resolve `false` mọi reservation | `:241`, `:1283-1310` |
| PendingLeave fence | Join mới cùng key chờ leave cũ xong, **tối đa 8s** (`PendingLeaveEntryWaitSeconds`) rồi mới đi tiếp | `KT:Net/EosLobbyService.cs:30`, `:985-1009` |
| Leave recovery | Callback leave âm *không* phải bằng chứng đã rời → giữ fence, retry ≤3 lần, backoff `min(1000, 250·attempt)` ms | `:34`, `:569-718`, `:798-804` |
| Membership mutation lease | Write gắn với membership đã capture; nếu quá hạn (15s) → **transfer sang leave** của đúng membership đó | `KT:Net/EosLobbyOperationOwnership.cs:365-396`, `:451`; `KT:Net/ProductLobbySession.cs:1664-1700` |
| Cleanup lease + cleanup debt | Cleanup orphan có debt 3 attempt; entry mới bị chặn khi còn debt (`CanIssueEntry`) | `:668-702`, `:865`, `:896` |
| Lifecycle certificate | Mỗi `EosLobbyService` nhận cert theo epoch; cert bị revoke khi owner release; `ResetLifecycle` tăng epoch + nâng operation floor | `:141-171`, `:1283`; `KT:Net/SessionOrchestrator.cs:55-69`; `KT:Net/ProductLobbySession.cs:206-211` |

**Deferred leave** (`ProductLobbySession` leave path): Leave thắng intent ngay, nhưng nếu `BeginMatch` đang in-flight thì teardown backend **chờ begin drain** trong cùng deadline 8s; quá hạn → `TryReserveLeave` (dựng fence trước), vào terminal failure, worker submit đúng leave cũ sau khi begin drain hoặc sau cửa sổ thứ hai 160×50ms (`KT:Net/ProductLobbySession.cs:681-711`, `:836-849`, `DeferredBeginDrainAttempts` `:61`).

**Invariants.**
- Fence trước, submit sau. Không bao giờ "fire leave rồi quên".
- Leave/mutation đã được admit **sống qua** `ResetLifecycle` (chỉ capture chưa admit mới bị vô hiệu) (`KT:Net/EosLobbyOperationOwnership.cs:1303-1307`).
- Reset tĩnh ở `SubsystemRegistration` (Unity tắt domain reload giữ static giữa các lần Play). Port Cocos: reset ở app cold-start / module init.

```
leave(key, membershipOp):
  pending = fence.reserve(key, op=next())        # trước mọi await
  await beginMatchIdle or deadline
  submitLeaveWorker(pending)                      # chạy cả khi UI đã đi
join(key):
  r = reserve(key, op=next())
  await olderLeaves(key) until 8s
  res = await eos.join(); if !commit(key, r.op): orphanLeave(res)
```

## 5. Write discipline `[GENERIC]`

**Vấn đề.** `UpdateLobby` callback `Success` chỉ nói "backend nhận", không nói snapshot người khác đọc đã đổi; ngược lại callback lỗi/throw **không** chứng minh backend chưa đổi.

**Cơ chế.**
- Kiểm từng bước build modification: `UpdateLobbyModification`, mỗi `AddAttribute`/`AddMemberAttribute` ≠ Success → release handle, trả `false` (`KT:Net/EosLobbyService.cs:1685-1738`). Lưu ý: hàm này **không tự có timeout** — caller phải bọc (§2).
- Sau write quan trọng: **poll snapshot replicated** đến khi khớp predicate — `ConfirmMatchSnapshotAsync` lấy mẫu t=0 rồi sau mỗi 50ms, 40 lần (`KT:Net/ProductLobbySession.cs:60,63,1583-1600`); custom-map agreement cũng 40×50ms (`:1135-1141`).
- Callback reject / throw → **vẫn confirm snapshot**: nếu thấy đã OPEN thì rollback (`RequireClosedEpochOrFatalAsync`), không xác định được → terminal failure (`:1414-1468`).
- Rollback phải confirm lại snapshot CLOSED; thất bại → fatal teardown (`:1526-1575`).
- Host: theo thứ tự product path `02` §6 (ledger + join_order 1 batch → confirm → begin → confirm open tuple / rollback), **rồi mới** lock `Joinviapresence` (bắt buộc, `02` §4 luật 2); về lobby trả `Publicadvertised`. Không khoá trước begin — begin fail sẽ để phòng bị ẩn mà vẫn ở lobby. **Không bao giờ** dùng `Inviteonly` (chặn rejoin). Không chạy lại flow mỗi tick vì rate-limit (retry có trần; KT gym `MaxStartAttempts=3`, `KT:Net/SessionOrchestrator.cs:1506-1510` — thứ tự gym thì đừng chép, `02` §6).

```
ok = await write(attrs)            # bounded
state = await confirm(pred, 40, 50ms)
if !ok and state == Confirmed: rollback(); confirm(closed) or fatal()
if ok and state == TimedOut: rollback-or-fatal
if state == Stale: cleanupCapturedMembership()
```

## 6. Read discipline `[GENERIC]`

**Vấn đề.** `CopyInfo`/member list ngay sau join hoặc lúc scene load hay về rỗng/partial. Kết luận "chỉ còn mình tôi" từ một lần đọc rỗng → tự rời party, tự convert solo, chặn start.

**Cơ chế.**
- Rỗng = **unknown → Wait**, không phải "mọi người đã đi" (`KT:Net/PostMatchPartyPolicy.cs:5,12-16`).
- Kết luận alone cần **2 poll version khác nhau** cùng báo 1 member (`:22-25`); version tăng mỗi poll authoritative hoàn tất (`RosterSnapshotVersion`, `KT:Net/ProductLobbySession.cs:144-148`).
- Count 0 trong lobby: bỏ qua Ready, khoá nút Ready; không convert solo (`KT:Net/LobbyMatchPolicy.cs:106-135`).
- Roster replication có retry ≤5 (`MaxRosterRetry`, `KT:Net/RosterReplicationWaiter.cs:45`).

```
observe(inLobby, selfPresent, n, ver):
  if !inLobby: return Leave
  if !selfPresent or n < 1: lone = null; return Wait
  if n > 1: lone = null; return Keep
  if lone != null and lone != ver: return Leave
  lone = ver; return Wait
```

## 7. Polling cadence `[KT-SPECIFIC]`

**Polling snapshot là baseline và nguồn sự thật** — KT chỉ ship polling (không có `AddNotify` nào), đã chạy thật. Mỗi tick chỉ **đọc** cache; write chỉ khi state đổi. Mỗi poll đọc lại cả `GetLobbyOwner` (KT cache owner một lần = bug, `02` §11).

`AddNotifyLobbyUpdateReceived` / `AddNotifyLobbyMemberUpdateReceived` / `AddNotifyLobbyMemberStatusReceived` là **tối ưu độ trễ tùy chọn [GENERIC]**: callback chỉ kích hoạt một lần đọc lại snapshot ngay, không mang state riêng; **không bao giờ** là nguồn duy nhất, **không** bỏ hay giãn polling vì đã có notify (độ tin cậy của notify qua background/reconnect: CHƯA KIỂM CHỨNG, KT chưa dùng).

| Vòng poll | Chu kỳ | Clock | Nguồn |
|---|---|---|---|
| Product roster poll (`RosterPoll`) | 1s | realtime | `KT:Net/ProductLobbySession.cs:2353-2355` |
| SessionOrchestrator roster tick (`RosterTick`) | 1.5s | scaled | `KT:Net/SessionOrchestrator.cs:1890-1893` |
| Match epoch tick (`EpochTick`) | 1.5s | scaled | `:1387-1390` |
| Match-return quorum tick (`MatchReturnTick`) | 0.5s | scaled | `:1788-1790` |
| Liveness/reuse probe (`ReuseTick`, probe timeout 2s) | 4s | scaled | `:1239-1253` |
| Spawn barrier poll | 0.5s | scaled | `KT:Net/PlayerSpawnService.cs:131` |
| Snapshot confirm sau write | 50ms ×40 | — | `KT:Net/ProductLobbySession.cs:60,63` |
| Gym auto-join retry | 3s ×10 | scaled | `KT:Net/SessionOrchestrator.cs:638-648` |

**Cảnh báo rate-limit.** `UpdateLobby` bị throttle phía EOS: không bao giờ write trong vòng poll vô điều kiện, gom nhiều attr vào **một** modification, và không retry flow begin-match mỗi tick (`KT:Net/SessionOrchestrator.cs:1507-1509`). Coroutine dùng `WaitForSeconds` (scaled) sẽ dừng khi `timeScale=0`; loop nào phải chạy khi pause thì dùng realtime.

## 8. Timeout & retry table `[KT-SPECIFIC — mốc của KT, game khác đo lại]`

| Mốc | Giá trị | Ý nghĩa | Nguồn |
|---|---|---|---|
| Join callback | 30s | Join EOS không callback | `KT:Net/EosLobbyService.cs:31`, dùng `:1178` |
| Cleanup callback | 15s | Leave orphan/cleanup | `:32`, dùng `:1412` |
| Leave callback | 15s | Mỗi attempt leave backend | `:33`, `:725` |
| Leave recovery attempts | 3, backoff `min(1000,250·n)`ms | Leave bị reject | `:34`, `:798-804` |
| PendingLeave entry fence | 8s | Join mới chờ leave cũ cùng key | `:30`, `:995` |
| Cleanup debt attempts | 3 | Retry cleanup orphan | `KT:Net/EosLobbyOperationOwnership.cs:702` |
| JoinById watchdog | 25s | Join phòng đã biết | `KT:Net/ProductLobbySession.cs:44`, `:513` |
| Host / JoinRandom watchdog | 30s | Create tốn nhiều round-trip hơn | `:53`, `:516-519` |
| Product leave end-to-end | 8s | Một budget chung cho drain connect + leave | `:59`, `:655` |
| Wait-leave-idle trước connect | 10s | Connect chờ leave cũ | `:524` |
| Match-state confirm | 40 × 50ms (≈2s) | Confirm snapshot sau write | `:60`, `:63` |
| Deferred begin drain | 160 × 50ms (≈8s) | Cửa sổ thứ hai trước khi submit leave đã reserve | `:61`, `:843-849` |
| Captured mutation | 15s | Quá hạn → transfer sang leave | `:62`, `:1681-1690` |
| EOS logout | 5s | Logout khi reauth | `:2626` |
| EOS dependency wait | 5s | Chờ transport/coordinator resolve | `KT:Net/SessionOrchestrator.cs:138` |
| EOS platform up | 15s | Chờ Connect interface | `:139` |
| EOS login | 12s | Login không callback | `:140` |
| Lobby owner PUID resolve | 10s | Owner replicate để dial | `:694` |
| Re-dial check / stall / give-up | 2s / 12s / 90s | Reconnect loop §9 | `:908-913` |
| Reuse / resume probe | 2s (resume settle 2s) | Liveness §10 | `:1253`, `:1346` |
| Rejoin start grace | 2.0s | Ledger chưa đáng tin ngay sau begin | `:1515` |
| Begin-match attempts | 3 | Flow host start | `:1510` |
| Roster retry | 5 | Replication muộn | `KT:Net/RosterReplicationWaiter.cs:45` |
| Match connect | 20s | Lobby→match connect status | `KT:Net/LobbyMatchPolicy.cs:14` |
| Chat send | 10s | Chat qua member attr | `KT:Net/LobbyChatChannel.cs:35` |
| Spawn-barrier starvation alarm | 10s | §12 | `KT:Net/PlayerSpawnService.cs:91` |
| Host return quorum | 5s | Host mở return | `KT:Net/MatchReturnQuorum.cs:21` |
| Client return fallback | 8s (> quorum 5s) | Client tự về nếu host kẹt | `KT:Net/ClientReturnFallback.cs:20` |
| P2P interrupted → proactive close | 3s (realtime) | Giải phóng slot cho reconnect | `KT:FishyEOS/Core/ServerPeer.cs:91`, `:349` |

**Luật thứ tự.** Fallback phía client > timeout authoritative phía host (8s > 5s); watchdog create > join (30s > 25s); leave fence (8s) < leave callback (15s) nên join mới không chờ hết một attempt leave.

## 9. Reconnect loop `[GENERIC]`

**Vấn đề.** (a) `StartConnection` một phát có thể timeout trong cửa sổ suppress re-invite của EOS. (b) Re-dial theo timer mù xé kết nối đang handshake; host thấy PUID còn sống và IGNORE request mới → deadlock rejoin (`KT:Net/SessionOrchestrator.cs:893-898`). (c) Commit `1b3789a1`: chính `StopConnection` của re-dial phát `Stopped`, handler hiểu là "host đóng phòng" → bỏ match.

**Cơ chế** (`EnsureClientEstablished`, `KT:Net/SessionOrchestrator.cs:899-961`):
- Mỗi 2s kiểm; chỉ re-dial khi transport thực sự `Stopped` **hoặc** attempt hiện tại stall ≥12s; give-up sau 90s.
- `_reconnecting = true` trong `try/finally` (`:905-949`) — throw giữa teardown không được để cờ kẹt `true` (cờ này gate cả reconnect lẫn host-loss handling).
- Handler `Stopped` chỉ xử lý khi `!exitInFlight && !sceneRoute && !wasServer && !serverStarted && !reconnectInFlight` (`LobbyMatchPolicy.ShouldHandleClientStopped`, `KT:Net/LobbyMatchPolicy.cs:744-750`; seam gọi từ `KT:UI/Screens/GameHUD/MatchLeaveNotifier.cs:141-147`, file thật `Assets/Scripts/UI/...`). *(sửa hint: rule tên `ShouldHandleClientStopped`, seam `ShouldHandleStopped` ở MatchLeaveNotifier)*
- Event bị nuốt phải được **trả lời sau**: hết budget → `ClientReconnectGaveUp` (bắn *sau* finally, vì khi cờ còn true chính gate sẽ nuốt nó) (`KT:Net/SessionOrchestrator.cs:951-960`) → `MatchLeaveNotifier.OnClientReconnectGaveUp` route home (`:158-166`).
- Một entry duy nhất cho rebuild (`TriggerClientReconnect`, `:1274-1282`): huỷ dial đang chạy, không stack attempt, gate `ShouldTriggerRebuild` (`KT:Net/LobbyMatchPolicy.cs:725-731`).

```
reconnecting = true
try:
  t0 = lastDial = now()
  while !client.started and now()-t0 < 90:
    sleep(2)
    if transport.state == Stopped or now()-lastDial >= 12:
      client.stop(); nextFrame(); client.dial(host); lastDial = now()
finally: reconnecting = false
if !client.started: emit(ReconnectGaveUp)     # trả lời Stopped đã bị nuốt
onStopped(): if reconnecting or selfExit or wasServer: return; routeHome()
```

## 10. Liveness `[GENERIC]`

**Vấn đề.** Mobile background → EOS ngừng tick, link P2P chết im lặng, không có close callback, FishNet vẫn `Started=true` (`KT:Net/AppLifecycleRecovery.cs:17-20`).

**Cơ chế.**
- Probe round-trip tầng app: client gửi token qua ServerRpc, server TargetRpc ack; hết timeout = dead (`KT:Net/ConnectionProbe.cs:48-80`). Probe là scene object → không cần đăng ký prefab.
- **Thiếu probe ≠ chết** (commit `343b20e7`): probe chỉ có trong gameplay scene; ở Boot/Menu/Lobby/Results trả `true` = "không có bằng chứng chết". Trước fix, reuse tick xé link khoẻ mỗi 4s suốt lobby/rejoin nên handshake ~2s không bao giờ xong (`KT:Net/EosP2PBoundary.cs:34-55`). *(sửa hint: KT là bool, không có enum tri-state Alive/Dead/Unavailable; "Unavailable" được map thành alive. Khi port nên làm tri-state tường minh.)*
- Reuse quyết định: client giữ link chỉ khi started + đúng host + probe mới OK (`KT:Net/PersistentPeerReuse.cs:51-62`); host giữ server khi `IsStarted` (`:70-75`).
- Resume: settle 2s → probe 2s → `ReuseAndResync` | `ClientRebuild` | `HostReturnToLobby` (`KT:Net/AppLifecycleRecovery.cs:9-14,47-60`; gọi `KT:Net/SessionOrchestrator.cs:1345-1346`).
- Watchdog foreground độc lập lifecycle (đổi wifi↔4G không có pause event): `ReuseTick` 4s (`:1235-1243`).
- Host side: EOS báo interrupted ~6s sau khi peer im; sau 3s grace realtime đóng peer chủ động để reconnect được nhận ngay (`KT:FishyEOS/Core/ServerPeer.cs:330-349`).

```
probeAlive(timeout):
  if no probe in scene: return Unknown        # caller coi như không chết
  tok = send(ServerRpc); return await ack(tok) within timeout ? Alive : Dead
onResume(isHost):
  await sleep(2); s = probeAlive(2)
  if s != Dead: return ReuseAndResync
  return isHost ? HostReturnToLobby : ClientRebuild
```

## 11. Auth bootstrap `[GENERIC + EOS quirks]`

**Vấn đề.**
- Login gọi ngay trong call tạo EOS platform (AddComponent EOSManager → Awake → Init → load lib) **mất callback**; platform đã up thì login < 1s (`KT:Net/SessionOrchestrator.cs:384-389`).
- Timeout của FishyEOS dùng `Time.time` (đứng khi pause/background) — `KT:FishyEOS/Util/Coroutines/AuthLogin.cs:41-44`, `ConnectCreateUser.cs:27-30`, `Core/ClientHostPeer.cs:47-48`; `DeviceIdCreate` **không có timeout** (`DeviceIdCreate.cs:25-29`).
- Intent bấm trong lúc auth fail sẽ treo nếu coordinator không được báo.

**Cơ chế.**
- Bring-up EOS là một task trong **boot pipeline trước menu**, có kết quả bool để pipeline retry (`InitEosAsync`, `KT:Net/SessionOrchestrator.cs:391`); kể cả dev skip-login vẫn bring EOS up (commit `66a24c2c`).
- Tự đợi bằng realtime: dependency 5s → platform 15s → 1 tick → login 12s (`:398-457`). Không `yield return Connect(...)` của FishyEOS (`:432-435`).
- Mọi nhánh fail đi qua `FailEos` → `Room.OnAuthFailed(reason)` (commit `15207038`, `:466-475`) → queued intent fail ngay (§3).
- PUID cache không đủ tin: chỉ dùng khi `GetLoginStatus == LoggedIn` (`KT:Net/ProductLobbySession.cs:2514-2535`); Create/Join trả `InvalidAuth`/`AuthExpired` → **một** lần force reauth (logout ≤5s) + retry cùng intent với gen mới (`:432-445`, `LobbyMatchPolicy.IsEosAuthError`).

```
boot.task("eos"):
  wait deps ≤5s; ensurePlatform(); wait connectIface ≤15s; nextFrame()
  login = startLogin(); wait login.cb ≤12s (realtime)
  if fail: coordinator.onAuthFailed(reason); return false   # pipeline retry
  coordinator.onAuthenticated(puid); return true
connect(intent):
  r = run(intent)
  if isAuthError(r) and !retried: forceRelogin(); r = run(intent, newGen)
```

## 12. Diagnostics `[GENERIC]`

| Pattern | KT | Nguồn |
|---|---|---|
| **Alarm to tiếng một lần** khi barrier đói: client connected ≥10s mà load barrier chưa mở → `LogError` (không có nhân vật, không lỗi UI) | `PlayerSpawnService` | `KT:Net/PlayerSpawnService.cs:85-91`, `:258-280`, `:449-453` |
| Re-arm cảnh báo spawn-starved để nhắc lại | | `:451` |
| Log quyết định kèm lý do (`[PeerReuse] REBUILD: …`, `[Lifecycle] …`) — healthy state im lặng, chỉ log khi bất thường | | `KT:Net/PersistentPeerReuse.cs:77-81`; `KT:Net/SessionOrchestrator.cs:1255-1259` |
| Telemetry 2 phía nối bằng **lobby/room id**: mỗi heartbeat mang `roomId` để ghép timeline host + client | dev-only shipper | `KT:Assets/Scripts/Core/DevTelemetryShipper.cs:170-186` |

Luật: endpoint telemetry nằm trong config ngoài repo skill (không hardcode URL/IP); gate dev-only; PUID trong log nên rút gọn (`ShortId` 6 ký tự, `KT:Net/SessionOrchestrator.cs:1284-1285`).

---

## Checklist port

- [ ] Mọi await SDK có timer + nhánh quan sát kết quả muộn (§2).
- [ ] Mỗi op mang generation; kết quả stale-thành-công → orphan-leave (§1).
- [ ] Một intent in-flight; leave vượt intent; create chờ leave xong (§3).
- [ ] Registry ownership tĩnh `(user, lobby)` + fence leave trước join; reset ở cold-start (§4).
- [ ] Write → kiểm kết quả từng bước → poll snapshot xác nhận → rollback/terminal (§5).
- [ ] Đọc rỗng = unknown; kết luận alone cần 2 poll version (§6).
- [ ] Không write trong vòng poll; bảng cadence/timeout đo lại cho game mình (§7-8).
- [ ] Reconnect: chỉ re-dial khi Stopped hoặc stall; cờ trong finally; event nuốt phải được trả lời (§9).
- [ ] Liveness bằng RPC round-trip; thiếu probe ≠ chết (§10).
- [ ] EOS init + login trong boot, timer realtime riêng, mọi fail báo coordinator, reauth đúng một lần (§11).
