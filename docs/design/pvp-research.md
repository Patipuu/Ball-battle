# PvP mode — nghiên cứu và đề xuất (Game Designer, 11/10/2026)

**Yêu cầu của Phú:** người chơi sưu tầm trait và vũ khí, tự ráp tổ hợp, đấu với nhau trên arena ngẫu nhiên.

**Phạm vi:** tài liệu này là nghiên cứu cho các giai đoạn sau Step 3 / lô v4. Không có số đo mới; mọi ngưỡng là **[chưa đo]**.

---

## 1. Thị trường và mô hình tham khảo (có nguồn)

| Game | Mô hình PvP | Bài học cho Ball Battle |
|---|---|---|
| **Super Auto Pets** | Arena **bất đồng bộ**: mỗi lượt nộp đội, ghép với đội người khác nộp **ở cùng lượt**. Đội nằm trong pool "một hai ngày" ([SAP Wiki](https://superautopets.wiki.gg/wiki/The_Basics), [Reddit](https://www.reddit.com/r/superautopets/comments/1uq0cls/asynchronous_interactions_in_super_auto_pets/)). Bản v44 làm lại ghép trận theo tim/cúp/lượt ([Steam News](https://store.steampowered.com/news/app/1714040/view/501718945006355541)). Versus là chế độ live riêng ([Wikipedia](https://en.wikipedia.org/wiki/Super_Auto_Pets)). | Async ghép theo "cùng tiến độ". Người chơi nhận xét ghost thắng ở lại pool lâu hơn, nên đối thủ trung bình mạnh hơn ([Reddit](https://www.reddit.com/r/superautopets/comments/1l54npq/the_game_feels_incredibly_rigged/)). |
| **SAP — kiếm tiền** | Gói thêm pet trả phí. **Ghép trận theo gói**, nên không gặp pet mình không có. Có **Weekly Pack** xoay vòng miễn phí cho mọi người ([SAP Wiki Weekly Pack](https://superautopets.wiki.gg/wiki/Weekly_Pack), [TapTap review](https://www.taptap.io/post/1557189)). | Mẫu "content pack" không bán sức mạnh trực tiếp. Pool tuần dùng chung là cách ghép công bằng. |
| **Backpack Battles** | Ghost: build được lưu thành snapshot, ghép với người **cùng vòng và cùng rank**. Rank tính theo từng nhân vật ([Steam guide](https://steamcommunity.com/sharedfiles/filedetails/?id=3187896204), [Steam thread](https://steamcommunity.com/app/2427700/discussions/0/4414173228276923532/), [Wiki](https://backpackbattles.wiki.gg/wiki/Game_Mechanics)). Dev: "the world needs more asynchronous auto battlers!" ([GameDiscoverCo](https://newsletter.gamediscover.co/p/how-backpack-battles-sold-650k-copies)). | Async + rank hoạt động tốt. Nhưng người chơi vẫn nghi bị "rig" và đòi minh bạch ([Steam thread](https://steamcommunity.com/app/2427700/discussions/0/6770657890634656281/)). |
| **The Bazaar** | Ghost chỉ ghép theo **ngày trong run**, gần như không theo rank. Người Bronze gặp ghost Diamond tối ưu; có lúc gặp lại cùng một ghost nhiều lần ([Reddit 1](https://www.reddit.com/r/PlayTheBazaar/comments/1n7f2yg/i_should_not_be_facing_diamond_player_ghosts_in/), [Reddit 2](https://www.reddit.com/r/PlayTheBazaar/comments/1lqx822/did_they_change_something_with_matchmaking_for/), [Steam](https://steamcommunity.com/app/1617400/discussions/0/591781130180636067/)). Review Steam "Mixed", chê bán nhân vật (research Step 3). | **Bài học ngược:** phải tách người mới khỏi pool ghost tối ưu; không lặp ghost; không bán sức mạnh. |
| **Mechabellum** | Live 1v1, sau đó thêm ladder async "Star Expedition". Người chơi thích vì "play at my own pace" ([Reddit](https://www.reddit.com/r/Mechabellum/comments/1w3sx8r/feedback_on_star_expedition_after_reaching_top_10/), [Wikipedia](https://en.wikipedia.org/wiki/Mechabellum)). | Ngay cả game live cũng thêm async cho tệp ít người online. |
| **Hearthstone Battlegrounds** | Live 8 người; đội dev nói cần "eight player tech" và ý thức đây là đầu tư lớn ([Game Developer](https://www.gamedeveloper.com/design/why-the-i-hearthstone-i-devs-wanted-to-make-an-auto-battler)). | Live nhiều người đắt; không hợp một dev indie. |
| **Clash Royale** | Giải đấu chuẩn hoá cấp thẻ về một mức trần (thường Level 11); thẻ thấp hơn trần giữ nguyên ([Fandom](https://clashroyale.fandom.com/wiki/Tournament)). | Tiền lệ "chuẩn hoá cấp" cho chế độ xếp hạng. |
| **Đối thủ trực tiếp: Auto Ball Arena** (Big Quail Games) | Auto-battler bóng vật lý, gắn vũ khí và "charm", **"Async Online PvP… share your build with your friends"**, có unlock. Dự kiến **Q4 2026** ([Steam](https://store.steampowered.com/app/4114620/Auto_Ball_Arena/)). | **Rủi ro cạnh tranh trực tiếp** cho đúng ý tưởng này. Khác biệt của mình: sim tất định + replay/mã chia sẻ + Run roguelite đã có. |
| Thể loại "weapon ball" | Research Step 3: fan Earclacks cá cược trên Discord, thích giải Bo3 vòng 16; Ball2 Battle có PvP 4v4. Earclacks đang làm trình mô phỏng cho người chơi tự tạo ([earclacks.fun](https://earclacks.fun)). | Khán giả thích **đấu giải / dự đoán**; PvP nên phục vụ cả xem lẫn chơi. |

**Mô hình sưu tầm:**
- Marvel Snap: thẻ mở theo thanh tiến trình, bán bán-ngẫu nhiên.
- Hearthstone: gói ngẫu nhiên + "dust" để chế thẻ cụ thể.
- Snap được xem thân thiện F2P hơn nhưng vẫn bị coi là "pay-to-progress" ([Blizzard Watch](https://blizzardwatch.com/2022/10/14/marvel-snap-hearthstone-compare/), [Game Math Done Quick](https://gmdq.substack.com/p/how-marvel-snap-drops-gacha)).
- Research Step 3 đã chốt hướng: **không gacha trả phí, không bán sức mạnh** (The Bazaar Mixed, SAP bị chê pay-to-win ở review tiêu cực).

## 2. Lợi thế kỹ thuật sẵn có (từ code)

- `MatchSim(config, seed, loadouts)` là **tất định**: cùng build + seed + loadout + layout cho cùng `ComputeHash()`. `WeaponTuning` / `TraitTuning` fingerprint được trộn vào hash; có `SimVersion.Rules` (hiện 3, lô v4 lên 4).
- `RunBuild` đã là loadout **serializable** (weapon id, trait id + level, DamagePct, SpeedPct, MaxHp, Hp, Radius) với `ToLoadout()`. Arena chọn bằng `ArenaRegistry.ForSeed(seed)`. 60 tick/s.
- **Hệ quả:** một trận PvP async chỉ cần gửi `(loadoutA, loadoutB, seed, arenaId, SimVersion, tuning fingerprint)`, khoảng vài trăm byte. Client và server replay ra **cùng trận**:
  - không cần netcode realtime;
  - chống gian lận bằng **server re-sim**;
  - mã chia sẻ trận và clip có sẵn.

  Đây là mô hình "recipe, not dish": lưu đầu vào để tính lại, không lưu kết quả ([UVS spec](https://github.com/constarik/uvs/blob/master/uvs.md)). Verification quy về so hash ([Klotho design](https://github.com/xpTURN/Klotho/blob/main/Docs/SynchronizationDesign.md)).
- **Giới hạn quan trọng:**
  - `docs/system-architecture.md` ghi rõ sim dùng **float**, "not guaranteed across CPU/runtime (Mono vs IL2CPP, x86 vs ARM)".
  - Unity xác nhận float IL2CPP không bảo đảm bit-identical giữa ARM và x86. Đường cross-platform là Burst `FloatMode.Deterministic` hoặc fixed-point ([Unity Discussions](https://discussions.unity.com/t/deterministic-floats-with-burst-il2cpp-and-unity-mathematics-sleef/910205), [Unity Manual](https://docs.unity3d.com/6000.6/Documentation/Manual/burst/float-precision-determinism.html)).
  - **Kết luận: server là trọng tài duy nhất.** Kết quả ở client (Android ARM) chỉ là "xem trước"; nếu hash lệch thì hiển thị kết quả server. Cross-device chính xác 100% cần fixed-point, việc lớn, để sau.

## 3. (1) Định dạng PvP đề xuất

**Chính: Async Ghost 1v1, Bo3.** Không làm live realtime ở giai đoạn đầu (chi phí cao, cần lượng người online, tiền lệ Battlegrounds).

**Hai chế độ:**
| Chế độ | Mô tả | Xếp hạng |
|---|---|---|
| **Arena Draft** (lõi) | Mỗi mùa/tuần có một **pool chung** gồm cả 8 vũ khí + 10 trait. Người chơi ráp **1 bóng** theo luật ngân sách bên dưới, nộp làm "defense ghost" và đấu ghost người khác. | Ranked, **chuẩn hoá** |
| **Collection Brawl** (casual) | Dùng vũ khí và trait **đã sở hữu** (mở từ Run). Ghép theo "điểm sưu tầm" gần nhau. | Unranked |
| **Run Ghost** (tuỳ chọn, sau) | Trong Run, trận 4 hoặc 8 có thể gặp **ghost của người chơi khác** ở cùng trận (mẫu SAP/Backpack Battles) thay cho địch sinh từ seed. | Không |

**Luật loadout [chưa đo]:**
- 1 vũ khí + tối đa **3 trait** (giữ `RunTuning.MaxTraits`), trait cấp 1–2 (giữ `MaxTraitLevel`).
- **Ngân sách 6 điểm:** trait L1 = 1, L2 = 2. Thẻ chỉ số DMG +15% / SPD +10% / MaxHp +15 = 1 điểm mỗi thẻ, tối đa 2 thẻ chỉ số.
  - Ví dụ: 3 trait L2 = 6 điểm, không còn chỗ cho chỉ số; hoặc 2 trait L2 + 2 thẻ chỉ số.
  - Giúp combo L2 có giá. Bot báo combo tệ nhất (Brawler + Vampire2 + SecondWind2 + Thorns2) là 3 trait L2.
- HP luôn bắt đầu đầy; không mang HP qua trận.
- **Ban list theo mùa** (vd. cấm Glass Cannon trên Brawler như đề xuất trong card-pool-v2). Thêm luật "**1 trait sustain**": Vampire / Second Wind / Bubble tối đa 1 (xem §5).

**Arena và Bo3 (có input của Game Tester):**
- Game Tester thấy arena quyết định kết quả quá mạnh (Venom @ Spikes 4/4). Arena mù làm thua có cảm giác "do may".
- **Luật:** pool ranked mùa có 5 arena [chưa đo]. **Mỗi bên ban 1 arena**: ghost ban sẵn khi nộp, người đánh ban lúc vào trận. Bo3 rút ngẫu nhiên từ phần còn lại.
- **Cả 3 arena được hiện TRƯỚC khi khoá loadout.** Người tấn công thấy arena rồi mới chỉnh build; ghost cố định nên được ban trước để bù.
- Loadout khoá cho cả 3 ván; async không có lượt phản ứng.
- Seed ván i = `Series.SeedFor(matchSeed, i)` (đã có trong `Sim/Series.cs`).
- **Thay đổi code:** arena tách khỏi seed. Truyền `arenaId` tường minh qua `ArenaRegistry.Create(id)` thay vì `ForSeed(seed)`; `MatchSim` đã nhận `config` nên Sim không đổi.

**Ghép trận:**
- Glicko-2 theo **mùa**, xử lý cả bất định và thời gian vắng ([Glicko-2 PDF](https://glicko.net/glicko/glicko2.pdf)).
- Pool ghost chia theo bậc rating. Thiếu ghost thì **nới bậc theo thời gian**, cuối cùng dùng **bot ghost** đánh dấu rõ "BOT" và **không tính điểm** (khuyến nghị chung cho tệp nhỏ: [Asobi docs](https://asobi.dev/docs/matchmaking)).
- **Chống lỗi The Bazaar:**
  - 10 trận đầu chỉ gặp ghost bậc thấp hoặc bot;
  - không gặp cùng một ghost 2 lần trong 24 h;
  - không gặp ghost của chính mình;
  - ghost thắng nhiều có trần số lần xuất hiện, để giảm thiên lệch "ghost thắng sống lâu" mà người chơi SAP nhắc.

## 4. (2) Sưu tầm và tiến trình

**Kiếm nội dung:**
- **Vũ khí:** đang mở qua thành tựu Run (`UnlockRules`). Giữ, cộng thêm "Weapon Stars" (Step 3 GĐ1): thắng Run với vũ khí X thì X lên sao, chỉ mở **cosmetic và danh hiệu**, không mở sức mạnh.
- **Trait:** mở qua Run. Thêm **Codex** (Step 3 GĐ1): ghi lại trait/vũ khí đã gặp và đã thắng.
- **Không gacha, không dust.** Nội dung ít (8 + 10), nên mở bằng chơi và thành tựu là đủ. Hệ chế tạo chỉ có nghĩa khi có ≥ 30 món.

**Công bằng:**
- **Ranked = chuẩn hoá:** Arena Draft dùng pool chung, mọi thứ đều dùng được kể cả chưa mở; tiền lệ Clash Royale event "may let players use cards they have not unlocked" ([Fandom](https://clashroyale.fandom.com/wiki/Tournament)).
- Không có "cấp vũ khí" ảnh hưởng chỉ số, kể cả ngoài ranked.
- **Casual = sở hữu:** Collection Brawl dùng đồ đã mở, ghép theo số món sở hữu (giống SAP ghép theo gói).

**Kiếm tiền (nhất quán với research Step 3; không thêm mô hình mới):**
- Steam bán trả trước; Android F2P với quảng cáo có thưởng tự nguyện; gói Supporter; **cosmetic mua thẳng**.
- PvP thêm chỗ cho cosmetic có ý nghĩa xã hội: skin bóng, vệt lưỡi, hoạt hình thắng, khung ghost, danh hiệu mùa.
- **Không:**
  - bán vũ khí/trait (bài học The Bazaar, SAP);
  - bán vé ranked;
  - quảng cáo thưởng tăng sức mạnh trong PvP (reroll/thêm thẻ chỉ dành cho Run);
  - cược tiền thật.

  Chế độ "dự đoán" ở GĐ2 dùng tiền ảo không bán.

## 5. (3) Hệ quả cân bằng

PvP phơi bày combo, vì người chơi sẽ tối ưu và chép meta: Backpack Battles bị chê "phải chơi theo meta" (research Step 3).

**Bằng chứng đã có:**
- Bot: combo Brawler + Vampire2 + SecondWind2 + Thorns2 thắng **85.7%** (report trait).
- Người thật: Brawler + Vampire + Heavy + Thorns thắng **8/8** trận Run.
- Venom @ Spikes: **72%** (bot) và **4/4** ván (playtest, ~20 s/ván).

**Luật đề xuất [chưa đo]:**
1. **Gate PvP** mở rộng `bot-sweep-v4.md`:
   - Không loadout hợp lệ nào (theo ngân sách) thắng > **65%** trước 200 loadout ngẫu nhiên hợp lệ trên toàn pool arena.
   - Combo Heavy + Vampire + Thorns và Venom @ Spikes là gate bắt buộc.
2. **Giới hạn sustain:** tối đa 1 trong {Vampire, Second Wind, Bubble}; trần hồi mỗi đòn của Vampire (card-pool-v2 P2).
3. **Luật theo arena:**
   - Arena có `Layout` gây sát thương (Spikes) có thể ra một **modifier PvP** riêng, ví dụ sát thương tường giảm hoặc độc ngắn hơn.
   - Phương án đơn giản hơn: loại Spikes khỏi pool ranked tới khi G2 xanh.
4. **Meta đọc được:** công bố **bảng tỉ lệ thắng + patch notes** mỗi mùa. Research Step 3 ghi người xem tự lập bảng tỉ lệ thắng và đòi patch notes.
5. **Bot "Meta-hunter":** hill-climb tìm loadout mạnh nhất trong ngân sách trước mỗi bản cập nhật. Sim ~38 ms/trận (một luồng, theo plan phase-03) nên chạy hàng nghìn trận mỗi lần thử khá rẻ.

## 6. (4) Kiến trúc kỹ thuật (tóm tắt; chi tiết ở `pvp-tech.md` của Dev)

Phần này không lặp lại thiết kế kỹ thuật. Dev đang viết `pvp-tech.md`, các điểm chính:
- ghost loadout chỉ vài chục byte, lưu trên Supabase/Firebase;
- server re-sim bằng .NET, vì Sim không phụ thuộc Unity;
- so hash trạng thái cuối (`HashInto` / `ComputeHash`);
- chỉ ghép trong cùng `SimVersion.Rules`.

Yêu cầu từ phía thiết kế:
- **Pha A (client-only):** mã chia sẻ build/trận (đấu ghost bạn bè, "Trận của ngày"). Không xếp hạng.
- **Pha B (backend nhẹ):** pool ghost, Glicko-2 theo mùa, server là trọng tài kết quả. Lý do: float không bảo đảm giống nhau giữa ARM/x86 (`docs/system-architecture.md`; [Unity Discussions](https://discussions.unity.com/t/deterministic-floats-with-burst-il2cpp-and-unity-mathematics-sleef/910205)).
- **Live realtime: không khuyến nghị.** Trận không có input nên live chỉ thêm "xem cùng lúc".
- Đổi version (mùa / bản cân bằng) thì ghost cũ hết hạn. Replay chỉ phát được trong cùng version.

## 7. (5) Lộ trình (sau Step 3 GĐ0–1 và lô v4)

| Pha | Việc giao | Phụ thuộc | Thay đổi code |
|---|---|---|---|
| **P0 — chuẩn bị** | Gate PvP trong SimTests; bot Meta-hunter; ban list. **Điều kiện bắt buộc trước khi mở PvP:** FX phản sát thương (Art 2), màn kết quả sau trận (card-pool-v2 P3), **replay** | Lô v4 (SimVersion 4), bot-sweep-v4 xanh | `SimTests`: sinh loadout hợp lệ theo ngân sách |
| **P1 — mã chia sẻ (pha A)** | Builder loadout (UI chọn vũ khí, trait, điểm), **ban/preview arena**, xuất/nhập mã, đấu Bo3 với ghost từ mã, "Trận của ngày" | P0; Versus hiện có | `PvpLoadout` (từ `RunBuild`: thêm validate ngân sách, `Serialize/Parse`, checksum, version header). View `LoadoutBuilderView`, `ShareCodeView`. Không đổi Sim. |
| **P2 — async ghost (pha B)** | Backend ghost + ghép trận + re-sim + Glicko-2 mùa; Arena Draft ranked; bot ghost lấp chỗ | P1; quyết định backend/ngân sách; `pvp-tech.md` | Theo `pvp-tech.md`. Thiết kế chỉ yêu cầu: bản ghi trận có arena đã ban + 3 arena, ghép gate theo version. |
| **P3 — Collection Brawl + cosmetic** | Casual dùng đồ sở hữu; cosmetic; danh hiệu mùa | P2; Step 3 GĐ1 (Codex, Stars) | `ProgressData` thêm cosmetic (Save version). |
| **P4 — Run Ghost / giải đấu / dự đoán** | Ghost trong Run; giải Bo3 nhánh 8/16 (Step 3 GĐ2) | P2 | Không đổi Sim lõi. |
| (Không hẹn) Live | Chỉ nếu số liệu ủng hộ | — | — |

## 8. (6) Rủi ro và câu hỏi cho Phú

**Rủi ro:**
1. **Đối thủ trực tiếp** Auto Ball Arena (async PvP, Q4 2026). Cần khác biệt rõ: replay tất định, mã chia sẻ, Run roguelite, bản sắc vũ khí (weapon-bible).
2. **Tệp người chơi nhỏ** khiến pool ghost mỏng, gặp lại cùng ghost (lỗi The Bazaar). Giảm bằng bot ghost và nới bậc.
3. **Float không tất định giữa ARM/x86.** Server làm trọng tài; replay trên điện thoại có thể khác chút với server. Đổi sang fixed-point là dự án riêng.
4. **Meta đóng băng.** Chỉ 8 vũ khí + 10 trait; ngân sách và ban list giúp, nhưng cần nội dung mới (Step 3 nhắm 14–16 vũ khí).
5. **Cảm giác "rig"** (Backpack Battles). Công bố cách ghép trận và tỉ lệ thắng; luôn có replay.
6. **Chi phí vận hành backend** cho một dev; dữ liệu cá nhân (GDPR/PDPL nếu lưu tên); kiểm duyệt tên ghost. Đề xuất không cho đặt tên tự do, chỉ chọn từ danh sách.

**Câu hỏi cho Phú:**
1. Nền tảng PvP trước: Steam, Android hay cross-play? Cross-play ARM ↔ x86 làm nặng vấn đề float.
2. Ngân sách backend hằng tháng, và ai vận hành? (Steam có thể dùng leaderboard/Cloud của nền tảng cho pha A.)
3. Ranked chuẩn hoá (pool chung) có đúng ý "sưu tầm" của Phú không, hay muốn ranked dùng đồ sở hữu?
4. Kiếm tiền PvP chỉ cosmetic, có chấp nhận không?
5. Có muốn PvP 2v2 / FFA 3–4 bóng không? Brainstorm Step 2 lo khó đọc trên 270 px.

## Cập nhật sau sweep 02:27 (11/10/2026) — tất định chéo nền tảng

**Phát hiện (Dev):** sim **đã lệch giữa nền tảng**.
- `Vec2.FromAngleDeg` dùng `MathF.Cos/MathF.Sin` (`Sim/Vec2.cs:32`).
- Digest golden của `VersusOutcomesMatchGolden` chạy trên Linux .NET 8 ra `0xD82531CC135DBA09`, khác giá trị golden ghi từ Windows (`simrunner/reports/step1-alltests.log`).
- Đây không còn là rủi ro lý thuyết ARM/x86: ngay x64 Linux và x64 Windows đã khác.

**Mong muốn thiết kế:**
- **Đưa lượng giác về bảng tra cố định** (lookup table theo độ / tick góc, sinh sẵn thành hằng số) **trong đợt nâng SimVersion của lô v4**. Mọi nền tảng ra cùng hash.
- Khi đó:
  - client tự kiểm được trận (so hash);
  - mã chia sẻ / replay (pha A) dùng được giữa máy bạn bè khác hệ điều hành;
  - server re-sim chỉ là kiểm tra, không phải nguồn sự thật duy nhất.
- **Cho tới lúc đó: server là trọng tài.** Replay ở client chỉ là "xem lại", không phải bằng chứng.
- **Việc cần kèm theo:**
  - ghi lại golden digest mới sau đổi;
  - chạy golden trên ít nhất Windows + Linux + một máy Android/ARM (IL2CPP) trước khi mở PvP pha A.
- Còn lại `sqrt` và phép float thường cũng có thể lệch. Chi tiết kỹ thuật và phạm vi để Dev quyết trong `pvp-tech.md`.
- Bảng tra đổi kết quả mọi trận, nên phải chạy lại toàn bộ bot-sweep-v4 sau khi đổi.

## Cập nhật sau sweep 02:46 (11/10/2026)
- **Combo sustain trong PvP:** không chỉnh số Heavy/Thorns. Dựa vào luật loadout ở §3: ngân sách 6 điểm và **tối đa 1 trait sustain** (Vampire / Second Wind / Bubble). Gate PvP đo vs build 3-trait ngẫu nhiên, không vs vũ khí trơn.
- **Spikes:** chỉ đưa vào pool ranked khi gate G2 mới (tường leo thang, mọi vũ khí 38–62%) xanh. Cơ chế ban 1 arena vẫn giữ.

## Cập nhật 03:05 (11/10/2026)
- Nếu Phú duyệt Spikes là sân nhà của Venom (~72.8% với tường leo thang N = 15 s), **ban 1 arena** trở thành công cụ chính để đối phó Venom trong PvP.
- Ranked có thể giữ Spikes trong pool vì người chơi tự ban được. ANTIDOTE là thẻ counter của Run, không có trong loadout PvP [chưa quyết].

## Đã chốt 11/10 (Phú duyệt 14:51 ICT)

- **Hướng PvP FINAL: "no-cost" (không tốn tiền).**
  - **Giai đoạn 1 không có server.** Người chơi xuất loadout thành **mã chia sẻ ngắn** (vũ khí, trait, seed, `SimVersion`).
  - Đối thủ dán mã và **đấu ngay trên máy mình**, kết quả tất định.
  - **Bộ thẻ chung** với **ngân sách điểm** (tối đa **1 trait sustain**). **Không lưu đồ sở hữu**.
  - **Chưa làm** ranked, 2v2, FFA.
  - Sau này nếu cần: backend **gói miễn phí** (tuỳ chọn).
- **Điều kiện bắt buộc:** v4 chuyển `MathF.Sin/Cos` sang **bảng tra lượng giác** để tất định giữa các nền tảng. Mã có `SimVersion` lệch thì báo không tương thích.
- Các nội dung về ranked, server là nguồn sự thật, BaaS trả phí ở trên chỉ còn là tham khảo cho sau này.
