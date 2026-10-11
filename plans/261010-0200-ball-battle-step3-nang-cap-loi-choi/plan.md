# Kế hoạch triển khai: Nâng cấp lối chơi Ball Battle

**Nguồn gốc:** rút ra từ lộ trình trong báo cáo `./reports/nang-cap-loi-choi-research-report.md` (ngày 10/10/2026)
**Phạm vi:** Ball Battle (Unity 6, pixel art 270×480 dọc, mô phỏng tất định), nhắm Steam (Windows) và Android
**Trạng thái hiện tại (mã nguồn dự án):** 8 vũ khí, 10 trait, 7 đấu trường; chế độ Versus (Bo3, chỉ xem) và Run (8 trận, boss ở trận 3/6/8, 3 mạng, thẻ, coin, x2 tốc độ); lưu cục bộ; chưa có tính năng online, chưa kiếm tiền, âm thanh tạm thời. Bot ngẫu nhiên thắng 7,1%, bot tham lam thắng 35,8%.
**Thang chi phí (ước lượng tương đối cho một dev):** S = vài ngày · M = 1–3 tuần · L = 1–2 tháng · XL = trên 2 tháng

## Tổng quan các giai đoạn

| Giai đoạn | Tên | Ưu tiên | Chi phí | Phụ thuộc | Trạng thái |
|---|---|---|---|---|---|
| 0 | Nền móng cảm giác và kênh riêng | P0 (bắt buộc) | M | — | Pending |
| 1 | Tiến trình dài hạn | P0 (bắt buộc) | L | GĐ 0 | Pending |
| 2 | Khán giả và chia sẻ | P1 (khác biệt hoá) | L | GĐ 0; nên làm sau GĐ 1 | Pending |
| 3 | Ra mắt và kiếm tiền | P1 | M | GĐ 0–1 (GĐ 2 càng tốt) | Pending |
| 4 | Sáng tạo và cộng đồng | P2 (khi có cộng đồng) | XL | GĐ 2–3, có số liệu ủng hộ | Pending |

---

## Giai đoạn 0: Nền móng cảm giác và kênh riêng

**Mục tiêu:** trận đấu nào cũng "đã tai" và dễ hiểu, không bao giờ mất dữ liệu lưu, có sẵn nơi hứng lưu lượng trước khi có video viral.

**Tính năng**
- [ ] Âm thanh va chạm "clacky" riêng cho từng vũ khí; thanh âm lượng nhạc và hiệu ứng (mặc định không quá to)
- [ ] Tinh chỉnh juice: hit-stop, rung màn hình và tia lửa đã có (FxView, mã nguồn dự án); thêm hiệu ứng khi vũ khí "lớn lên"
- [ ] Màn tổng kết sau trận: sát thương theo nguồn (vũ khí, trait, thẻ), khoảnh khắc lật kèo
- [ ] Thêm tốc độ x4 và nút bỏ qua trận (giữ x2 sẵn có)
- [ ] Lưu dữ liệu có số phiên bản và migration; sao lưu; đồng bộ cloud (Steam Cloud / Play Games, cần kiểm tra tài liệu nền tảng)
- [ ] Mở rộng công cụ cân bằng sẵn có (MatchupReport, TraitBalanceReport, ArenaBalanceReport, RunDifficultyThresholdTests — mã nguồn dự án): thêm combo trait × vũ khí và vũ khí mới
- [ ] Bản sắc: tên, cá tính, câu thoại hoặc biểu cảm, hoạt hình chiến thắng cho 8 vũ khí; không dùng tên "Earclacks" trong store
- [ ] Mở trang Steam (Coming Soon) và Discord; thu thập wishlist và email

**KPI thành công**
- 0 báo lỗi mất dữ liệu lưu sau cập nhật
- Mọi cặp vũ khí có tỉ lệ thắng trong khoảng ~40–60% (ngưỡng tự đặt, đo bằng bot)
- Thời gian chơi thử trung vị ≥ 25 phút (trung vị khảo sát demo), mục tiêu kéo dài tới khoảng 60 phút (Peglin đạt trung vị 67 phút, thuộc top 5)
- Trang Steam hoạt động trước khi đăng video ngắn đầu tiên

**Dẫn chứng chính**
- Không có âm thanh thì "feels eerie": [itch.io Weapon Balls Simulation](https://w12isawesome.itch.io/weapon-balls-simulation); "clacky" là thương hiệu: [YouTube @Earclacks](https://www.youtube.com/@Earclacks)
- "MY DATA GOT WIPED", "SO LOUD", "slow motion": [itch.io Naroza](https://naroza.itch.io/weapon-balls)
- Fan nhân cách hoá bóng, GIF chiến thắng: [NamuWiki Weapon Ball](https://en.namu.wiki/w/Earclacks/Weapon%20Ball), [NamuWiki Earclacks](https://en.namu.wiki/w/Earclacks)
- Cân bằng theo số liệu: [Slay the Spire GDC](https://www.gamedeveloper.com/design/learn-i-slay-the-spire-i-s-metrics-driven-approach-to-game-balancing-at-gdc-2019)
- Mất khoảng 15k wishlist vì viral trước khi có trang Steam: [How To Market A Game](https://howtomarketagame.com/2023/09/11/three-inspiring-marketing-success-stories/)
- Mốc thời gian chơi demo: [Peglin](https://howtomarketagame.com/2022/05/04/how-peglin-made-1049413-in-1-week/)
- Cảm giác năng lực (SDT): [ScienceDaily](https://www.sciencedaily.com/releases/2006/12/061226134706.htm)

---

## Giai đoạn 1: Tiến trình dài hạn

**Mục tiêu:** lượt chạy nào cũng có giá trị, kể cả khi thua; có lý do quay lại mỗi ngày; tăng độ rộng nội dung.

**Tính năng**
- [ ] Thang "Cấp sao" theo từng vũ khí (giống Stakes/Ascension): thắng Run với vũ khí X thì mở cấp khó tiếp theo của X
- [ ] XP tài khoản và tiến độ mở khoá cộng dồn cả khi thua
- [ ] Codex sưu tầm: vũ khí, trait, đấu trường, boss đã gặp, kèm thống kê cá nhân
- [ ] Endless sau boss trận 8, có giới hạn hiệu năng (tránh lỗi "low framerate torture")
- [ ] Daily Run: seed chung toàn cầu, 2–3 modifier có đánh đổi, không lặp hai ngày liền; bảng xếp hạng (ban đầu có thể dùng dịch vụ của nền tảng)
- [ ] Tăng lên khoảng 14–16 vũ khí và thêm trait (mức ước lượng)
- [ ] Tuỳ chọn "Unlock all" cho người chơi kỳ cựu
- [ ] Không làm: phần thưởng đăng nhập hằng ngày, nâng chỉ số vĩnh viễn kiểu cày cuốc (nếu có thì phải cho hoàn lại được)

**KPI thành công**
- Android: D1 ≥ 30%, D7 ≥ 6% (nhóm top 25% của GameAnalytics); PC: D1 ≥ 15% (nhóm top 25% PC đạt khoảng 15–16%)
- ≥ 70% lượt chạy kết thúc có ít nhất một mục tiến độ hoặc mở khoá (mục tiêu tự đặt)
- Tỉ lệ DAU tham gia Daily Run; số ngày quay lại liên tiếp
- Số cấp sao trung bình đã mở mỗi người chơi sau 7 ngày

**Dẫn chứng chính**
- Stakes và mở khoá bộ bài, "unlock all": [Wikipedia Balatro](https://en.wikipedia.org/wiki/Balatro), [TouchArcade](https://toucharcade.com/2024/03/18/balatro-interview-mobile-port-localthunk-dlc-plans-updates-new-jokers-demo-feedback/)
- "Every run counts", tránh phần thưởng đăng nhập: [Tom's Guide Hades](https://www.tomsguide.com/uk/features/hades-exclusive-interview-supergiant)
- Thành tựu và nâng cấp hoàn lại được: [The Conversation](https://theconversation.com/vampire-survivors-how-developers-used-gambling-psychology-to-create-a-bafta-winning-game-203613)
- Daily làm xong trong một cuối tuần: [Game Developer](https://www.gamedeveloper.com/design/the-24-hour-ticket-examining-daily-runs-); Daily Climb: [Kotaku](https://kotaku.com/slay-the-spires-daily-challenges-keep-getting-better-1823780588)
- Endless kéo dài vòng đời: [Trampoline Tales](https://blog.trampolinetales.com/whats-next-for-luck-be-a-landlord/)
- "Loses its charm after about 3 weeks": [Google Play SayGames](https://play.google.com/store/apps/details?id=com.playcharge.weaponball&hl=en_US)
- Completion nằm trong top 3 động lực: [WN Hub](https://wnhub.io/news/analytics/item-11980); Strategy giảm: [Quantic Foundry](https://quanticfoundry.com/?p=57581)
- Benchmark retention: [GameAnalytics 2026](https://gamedevreports.substack.com/p/gameanalytics-mobile-and-pc-game)
- Độ rộng nội dung của đối thủ: [Ball Game 116 bóng](https://bluecoyotestudio.itch.io/ball-game), [Naroza 35+](https://naroza.itch.io/weapon-balls)

---

## Giai đoạn 2: Khán giả và chia sẻ

**Mục tiêu:** biến mỗi trận thành nội dung chia sẻ được; tạo kịch tính bằng việc chọn phe; chiếm khoảng trống tính năng dự đoán.

**Tính năng**
- [ ] Chế độ Dự đoán trong Versus và giải đấu: chọn bóng thắng trước trận, cược bằng đồng tiền ảo **chỉ kiếm được trong game, không bán, không quy đổi**
- [ ] Giải đấu nhánh 8/16 bóng, đấu Bo3, theo dõi chuỗi dự đoán đúng
- [ ] Battle Royale / FFA 3–4 bóng
- [ ] Mã chia sẻ trận (seed + loadout + đấu trường), nhập mã để xem lại chính xác
- [ ] Xuất clip hoặc GIF dọc 9:16 kèm watermark và tên game
- [ ] "Trận đấu của ngày": cùng một cặp đấu cho mọi người, đoán người thắng, chia sẻ kết quả dạng lưới không tiết lộ đáp án
- [ ] Ưu tiên cân bằng để có lật kèo muộn trong trận (độ bất định nảy sinh trong trận)

**KPI thành công**
- Số lượt chia sẻ hoặc xuất clip trên 100 lượt chạy
- Khoảng 1.000 wishlist cho mỗi 330 nghìn lượt xem video ngắn (mốc tham khảo của Infinitum, Instagram Reels)
- % người chơi Versus dùng chế độ Dự đoán; số trận xem mỗi phiên
- Lượng mã trận được nhập (đo truyền miệng)

**Dẫn chứng chính**
- Fan cá cược trên Discord chính thức: [NamuWiki Earclacks](https://en.namu.wiki/w/Earclacks)
- Chọn phe là điều kiện của kịch tính: [Knobloch-Westerwick et al.](https://deepblue.lib.umich.edu/handle/2027.42/78673?show=full); bất định trong trận: [Oh, Lee & Jang](https://pure.jbnu.ac.kr/en/publications/outcome-uncertainty-and-esports-viewership-the-case-of-overwatch-/)
- Giải Bo3 vòng 16 và Battle Royale của Earclacks: [NamuWiki Weapon Ball](https://en.namu.wiki/w/Earclacks/Weapon%20Ball); người chơi xin "3-4 weapons at once": [App Store BalanGo](https://apps.apple.com/app/id6749248257)
- Quay clip TikTok là điểm bán hàng: [App Store BalanGo](https://apps.apple.com/app/id6749248257)
- Lưới chia sẻ của Wordle: [Wikipedia Wordle](https://en.wikipedia.org/wiki/Wordle)
- Tỉ lệ view ra wishlist: [How To Market A Game](https://howtomarketagame.com/2023/09/11/three-inspiring-marketing-success-stories/)
- Ranh giới cờ bạc: [GOV.UK](https://www.gov.uk/government/consultations/loot-boxes-in-video-games-call-for-evidence/outcome/government-response-to-the-call-for-evidence-on-loot-boxes-in-video-games), [Esports Legal News](https://esportslegal.news/2026/01/28/austrian-supreme-court-loot-boxes/)

---

## Giai đoạn 3: Ra mắt và kiếm tiền

**Mục tiêu:** có doanh thu bền mà không làm hỏng điểm đánh giá; kiểm tra nhu cầu trên Steam trước khi đầu tư lớn.

**Tính năng / việc cần làm**
- [ ] Demo Steam (Run rút gọn và Versus); tham gia Next Fest
- [ ] Giá Steam 4,99–6,99 USD, bán trả trước; không quảng cáo, không IAP kiểu di động; DLC rẻ (gói vũ khí hoặc đồ trang trí) sau khi ra mắt
- [ ] Android miễn phí: quảng cáo có thưởng do người chơi tự bấm (thêm reroll, nhân đôi coin, thêm thẻ); **không** interstitial sau trận, **không** quảng cáo ở màn nhận thưởng, **không** thanh năng lượng
- [ ] Gói "Supporter / Xoá quảng cáo" một lần (~2,99–4,99 USD) kèm bộ skin
- [ ] Đồ trang trí mua thẳng: skin bóng, vệt vũ khí, theme đấu trường, hoạt hình chiến thắng; **không** gacha, **không** vật phẩm ngẫu nhiên trả tiền, **không** bán sức mạnh
- [ ] Không đặt lời mời mua hàng ngay sau trận thua sát nút
- [ ] Bản địa hoá: EN + VI khi ra mắt, sau đó ZH/JA/KO
- [ ] Thủ tục: tài khoản Google Play (25 USD, thử nghiệm kín cho tài khoản cá nhân), AdMob (xác minh PIN 2–3 tuần), Steam Direct (100 USD), khảo sát thuế Mỹ, tham vấn thuế Việt Nam (ngưỡng 500 triệu đồng/năm, Nghị định 117/2025)

**KPI thành công**
- Chơi demo → wishlist ≥ 19% (trung vị Next Fest 6/2026)
- Bán tuần đầu ≥ 0,15 lần số wishlist lúc ra mắt
- Điểm Google Play ≥ 4,5★ (mốc BalanGo); tỉ lệ đánh giá Steam tích cực ≥ 85%
- Tỉ lệ người chơi trả tiền trên Android 0,5–1%; tỉ lệ chủ động xem quảng cáo có thưởng
- Tỉ lệ đánh giá 1★ có nhắc tới quảng cáo < 10% (mục tiêu tự đặt)

**Dẫn chứng chính**
- Giá các game tương đồng: [Steam Brotato](https://store.steampowered.com/app/1942280/Brotato/), [Steam Vampire Survivors](https://store.steampowered.com/app/1794680/Vampire_Survivors/), [Steam Balatro](https://store.steampowered.com/app/2379780/Balatro/)
- Chuyển đổi wishlist: [GameDiscoverCo](https://newsletter.gamediscover.co/p/the-state-of-steam-wishlist-conversions); demo: [How To Market A Game](https://howtomarketagame.com/2026/06/30/nobody-plays-demos-and-that-is-ok/)
- Phân phối doanh thu Steam 2025: [WN Hub](https://wnhub.io/news/stores-and-publishing/item-49120); phí và thuế Steam: [presskit.gg](https://presskit.gg/field-guides/how-much-does-steam-take), [Steamworks FAQ](https://partner.steamgames.com/doc/finance/payments_salesreporting/faq)
- Mô hình di động "free for real": [PocketGamer.biz](https://www.pocketgamer.biz/news/80396/vampire-survivors-surprise-launches-to-mobile/), [mobilegamer.biz](https://mobilegamer.biz/vampire-survivors-mobile-hits-3m-downloads/), [TouchArcade DLC](https://toucharcade.com/2023/03/14/vampire-survivors-legacy-of-the-moonspell-dlc-mobile-download-release-date-price-iphone-android-ios-poncle/)
- Phản ứng với quảng cáo: [App Store reviews SayGames](https://apps.apple.com/us/app/weapon-ball-battles/id6751324223?see-all=reviews), [Google Play SayGames](https://play.google.com/store/apps/details?id=com.playcharge.weaponball&hl=en_US), [Google Play BalanGo](https://play.google.com/store/apps/details?id=com.balangodev.weaponballfight&hl=en_US), [Playio](https://blog.playio.co/impact-of-ads-on-mobile-gamers)
- eCPM và cơ cấu doanh thu: [Playio eCPM](https://blog.playio.co/mobile-game-ecpm-benchmarks-2026), [Sensor Tower](https://gamedevreports.substack.com/p/sensor-tower-mobile-game-ad-monetization), [Juego Studio](https://www.juegostudio.com/blog/arpdau-benchmarks-by-game-genre)
- Quy định vật phẩm ngẫu nhiên: [Apple 3.1.1](https://developer.apple.com/app-store/review/guidelines/), [Google Play Payments](https://support.google.com/googleplay/android-developer/answer/9858738)
- Bản địa hoá: [GameDiscoverCo Backpack Battles](https://newsletter.gamediscover.co/p/how-backpack-battles-sold-650k-copies), [Bilibili/Dwarves](https://howtomarketagame.com/2023/09/11/three-inspiring-marketing-success-stories/)
- Thủ tục: [Play Console](https://support.google.com/googleplay/android-developer/answer/6112435?hl=en), [Play phí](https://support.google.com/googleplay/android-developer/answer/112622?hl=en), [AdMob](https://support.google.com/admob/answer/3001017?hl=en), [Baker McKenzie](https://www.bakermckenzie.com/-/media/files/insight/publications/2026/01/vietnamtax-administration-pit-and-vat-laws.pdf), [Nghị định 117](https://insightplus.bakermckenzie.com/bm/tax/vietnam-implementing-decree-requiring-e-commerce-platforms-and-digital-platforms-to-withhold-tax-on-individual-sales) (link chuyển hướng; đối chiếu qua [EY Tax News](https://taxnews.ey.com/news/2025-1337-vietnam-promulgates-decree-on-tax-administration-for-business-activities-on-e-commerce-and-digital-platforms-of-households-and-individuals))

---

## Giai đoạn 4: Sáng tạo và cộng đồng

**Mục tiêu:** cộng đồng tự sinh nội dung, kéo dài vòng đời; chỉ làm khi GĐ 2–3 cho thấy có cộng đồng.

**Tính năng**
- [ ] Trình chỉnh bóng và đấu trường theo **tham số** (chỉ số, quy tắc lớn dần, hành vi chọn từ bộ có sẵn); xuất mã chia sẻ; **không** cho nhập văn bản tự do hay tải ảnh lên (để chi phí kiểm duyệt gần bằng không)
- [ ] Bình chọn "Bóng của tuần" trên Discord, bóng thắng được đưa vào game hoặc giải đấu tuần
- [ ] Stream mode: `!join` thả bóng vào trận; tự mở Twitch Prediction mỗi trận (2–10 kết quả)
- [ ] PvP ghost bất đồng bộ: đấu với bản chụp đội hình Run của người khác, ghép theo cấp, kho nội dung như nhau cho mọi người (không pay-to-win)
- [ ] Nhịp cập nhật: mỗi 2–4 tuần có vũ khí hoặc chế độ mới, mỗi lần đều đi kèm video ngắn

**KPI thành công**
- Số mã bóng tự tạo được chia sẻ hoặc nhập mỗi tuần
- Số kênh stream khác nhau dùng game mỗi tháng
- Người chơi đồng thời tăng sau mỗi bản cập nhật; thành viên Discord hoạt động
- Tỉ lệ thắng của người mới trong PvP ghost ≥ 40% (kiểm tra ghép trận)

**Dẫn chứng chính**
- Người chơi xin tự thiết kế đấu trường và chỉnh máu: [itch.io Naroza](https://naroza.itch.io/weapon-balls)
- UGC tương quan với doanh thu dài hạn: [GameDiscoverCo UGC](https://gameindustrylibrary.com/documents/ugc-impact-study-2025/read); chi phí kiểm duyệt: [Vice Roblox](https://www.vice.com/en/article/leaked-documents-how-roblox-moderates-content-mass-shootings-grooming/)
- Mô hình Marbles on Stream: [Streams Charts](https://streamscharts.com/games/marbles-on-stream/statistics), [Steam](https://store.steampowered.com/app/1170970/Marbles_on_Stream/); API: [Twitch Predictions](https://dev.twitch.tv/docs/api/predictions/)
- PvP bất đồng bộ: [Wikipedia Super Auto Pets](https://en.wikipedia.org/wiki/Super_Auto_Pets), [GameDiscoverCo Backpack Battles](https://newsletter.gamediscover.co/p/how-backpack-battles-sold-650k-copies); bài học ghép trận và pay-to-win: [PC Gamer via Yahoo](https://tech.yahoo.com/gaming/articles/back-month-spent-strangling-itself-161941715.html), [CardGamer](https://cardgamer.com/games/digital-card-games/the-bazaar/6-reasons-why-the-bazaars-open-beta-caused-an-uproar)
- Cập nhật tạo đỉnh người chơi: [Gamepressure](https://www.gamepressure.com/newsroom/vampire-survivors-continues-to-amaze-new-activity-record/z1408e); nhịp của BalanGo: [App Store](https://apps.apple.com/app/id6749248257)

---

## Ghi chú và giả định
- Mốc chi phí và một số KPI (tỉ lệ thắng 40–60%, ≥ 70% lượt chạy có tiến độ, số vũ khí 14–16, đánh giá 1★ nhắc quảng cáo < 10%) là mục tiêu tự đặt, không có nguồn benchmark.
- Lượt cài và doanh thu đối thủ là ước tính của bên thứ ba. Đã đọc trực tiếp 566 bình luận YouTube hàng đầu của 10 video Earclacks (mã hoá chủ đề bằng từ khoá); Reddit bị chặn, bình luận TikTok không đọc được.
- Bằng chứng từ bình luận ủng hộ thứ tự ưu tiên: bình luận được thích nhiều nhất là về lật kèo ở HP thấp, combo và cá tính vũ khí → GĐ0 (màn tổng kết khoảnh khắc lật kèo, bản sắc vũ khí), GĐ2 (dự đoán, giải đấu); người xem đòi patch notes và tự lập bảng tỉ lệ thắng → công bố patch notes và bảng tỉ lệ thắng mỗi bản cập nhật ([YouTube Sword vs Dagger](https://www.youtube.com/watch?v=RMjY1CnGPt4), [Season 1 Tournament](https://www.youtube.com/watch?v=FkM-klhexJE)).
- Rủi ro cạnh tranh: Earclacks đang tự làm trình mô phỏng cho người chơi tạo vũ khí và kịch bản, hẹn thông tin trong 2026 ([earclacks.fun](https://earclacks.fun)) → GĐ4 (trình chỉnh bóng) giữ ưu tiên thấp; khác biệt hoá bằng Run roguelite và tiến trình.
- Bài học từ đánh giá Steam: không bán nhân vật/vũ khí ảnh hưởng sức mạnh (The Bazaar Mixed, chê giá ~20 USD/nhân vật), ghép trận PvP minh bạch (Backpack Battles), không làm mất dữ liệu lưu khi cập nhật (Brotato) ([Steam API The Bazaar](https://store.steampowered.com/appreviews/1617400?json=1&filter=all&review_type=negative&language=english&purchase_type=all&num_per_page=30&day_range=365), [Backpack Battles](https://store.steampowered.com/appreviews/2427700?json=1&filter=all&review_type=negative&language=english&purchase_type=all&num_per_page=30&day_range=365), [Brotato](https://store.steampowered.com/appreviews/1942280?json=1&filter=all&review_type=negative&language=english&purchase_type=all&num_per_page=30&day_range=365)).
- Phần thuế không phải tư vấn pháp lý; cần chuyên gia thuế Việt Nam xác nhận.
