# Ball Battle — Game Design Document

**Phiên bản:** 0.1 — tổng hợp từ repo ngày 13/10/2026.  
**Ngôn ngữ tài liệu:** tiếng Việt; UI hiện dùng tiếng Anh.  
**Mục đích:** tài liệu chung cho thiết kế, code, art, audio, QA và playtest.  
**Baseline khảo sát:** HEAD `29fa2c3`, cộng working tree có nền tảng fixed-point 6a.6a chưa commit.  
**Phương pháp:** đối chiếu source, quyết định thiết kế và báo cáo lưu trong repo. Chưa chạy lại game hoặc test trong đợt biên soạn này.

## 1. Quy ước và mức xác nhận

| Nhãn | Ý nghĩa |
|---|---|
| **Hiện có** | Có hành vi tương ứng trong source được khảo sát; không tự đồng nghĩa đã nghiệm thu trên mọi build |
| **Đã chốt, chưa có** | Có quyết định cuối trong tài liệu thiết kế nhưng chưa thấy cơ chế tương ứng trong source |
| **Đề xuất** | Hướng bổ sung cho các vấn đề người chơi vừa phản ánh; cần chốt spec trước triển khai |
| **Chưa chốt** | Dữ liệu hoặc quyết định chưa đủ để viết thành yêu cầu thực thi |

GDD mô tả cả trò chơi hiện tại và hướng hoàn thiện. Từng phần chưa triển khai được gắn nhãn, không dùng các ý tưởng đó làm kỳ vọng đối với build hiện tại.

Khi tài liệu mâu thuẫn, xác định hai việc riêng: source cho biết hành vi hiện có; quyết định cuối cho biết hành vi mong muốn. Không tự đổi luật theo một đoạn lịch sử. Các luật 3 mạng, boss trận 3/6/8 và hồi đầy khi thua thuộc thiết kế cũ.

## 2. Tổng quan sản phẩm

Ball Battle là game auto-battler với bóng mang vũ khí, tự di chuyển và va chạm trong đấu trường. Người chơi lựa chọn vũ khí, trait và nâng cấp trước trận; sau đó quan sát build của mình chiến đấu, mạnh lên trong trận và đối đầu với cấu hình khác.

| Hạng mục | Định hướng |
|---|---|
| Thể loại | Auto-battler; Run có cấu trúc roguelite |
| Vai trò người chơi | Chuẩn bị build, đọc đối thủ/arena, quản lý HP và xu, học từ kết quả |
| Điều khiển trong trận | Tự đánh; không di chuyển bóng hoặc nhắm vũ khí thủ công |
| Chế độ hiện có | Versus best-of-3; Run 8 trận |
| Góc nhìn / bố cục | 2D, màn hình dọc |
| Mỹ thuật | Pixel art, độ phân giải native 270×480 |
| Công nghệ hiện tại | Unity 6000.5.1f1; Sim C# thuần, View Unity |
| Nền tảng trong roadmap | Windows/Steam và Android; nền tảng phát hành đầu tiên chưa chốt |
| Nhóm người chơi đề xuất | Người thích xây build, thử tương tác vũ khí và xem trận ngắn có khả năng lật kèo |
| Kiếm tiền / phát hành | Chưa chốt; chưa có hệ thống thương mại trong phạm vi hiện tại |

Giá trị trải nghiệm mong muốn: **tôi chọn một phương án có lý do, nhìn thấy tác động của nó, hiểu kết quả và biết mục tiêu tiếp theo.** Đây là định hướng cải thiện; phản hồi hiện tại cho thấy game chưa truyền đạt đủ rõ các bước đó.

## 3. Các trụ cột thiết kế

1. **Lựa chọn có tác động:** vũ khí, trait, hồi máu và xu tạo quyết định trước trận; preview giúp người chơi cân nhắc.
2. **Tăng sức mạnh nhìn thấy được:** sát thương, tốc xoay, tầm với, số đạn hoặc công trình tăng theo quy tắc từng vũ khí trong trận.
3. **Đọc được nguyên nhân:** HP, chỉ số, trạng thái, màu sát thương và kết quả giúp giải thích điều đã xảy ra.
4. **Tiến trình có mục tiêu:** vượt boss, thắng Run và mở nội dung là động lực dài hạn; UI cần nêu rõ điều kiện và bước kế tiếp.
5. **Phản hồi nghe và nhìn nhất quán:** âm thanh, hitstop, VFX và kết quả phản ánh cùng sự kiện gameplay.

## 4. Vòng chơi

### 4.1. Run — vòng chơi chính đề xuất cho bản hoàn thiện gần nhất

```text
Xem mục tiêu / tiếp tục run
    → chọn 1 trong 3 vũ khí khởi đầu
    → chọn 1 trong 3 thẻ đầu run
    → xem đối thủ và arena, chọn thẻ / dùng xu
    → khóa build, countdown, đấu tự động
    → đọc kết quả và thay đổi HP/xu
    → thắng: chuẩn bị trận kế tiếp
      thua/hòa lần đầu: chọn thẻ retry, đánh lại
      thua/hòa khi hết retry: kết thúc run
    → tổng kết / mở khóa / mục tiêu kế tiếp
    → run mới
```

Phần chọn, đấu, kết quả và lưu đã có. Cách trình bày mục tiêu/mở khóa kế tiếp và ưu tiên Run ở menu là **đề xuất cần hoàn thiện**.

### 4.2. Ba tầng tiến độ

| Tầng | Nội dung | Trạng thái |
|---|---|---|
| Trong trận | Quy tắc tăng sức mạnh của vũ khí, HP, status | Hiện có; tăng trưởng này được tạo lại mỗi trận |
| Trong Run | Thẻ, trait, chỉ số, xu, HP còn lại và số trận đã vượt | Hiện có; giữ giữa các trận của cùng Run |
| Giữa các Run | Thành tựu, kỷ lục, số lần thắng và pool nội dung mở khóa | Hiện có dữ liệu; UI mục tiêu kế tiếp cần bổ sung |

Xu và nâng cấp trong Run không trở thành chỉ số vĩnh viễn. Chưa có hệ XP tài khoản hoặc nâng sức mạnh vĩnh viễn.

### 4.3. Versus

Chọn vũ khí hai bên → chọn arena → bắt đầu series → xem các round → bên đạt 2 round thắng thắng series → rematch hoặc về menu. Chế độ này cho phép thử toàn bộ roster, không áp dụng khóa mở nội dung của Run.

## 5. Điều khiển và luồng màn hình

**Hiện có:** click/tap để chọn nút/thẻ; trận tự đánh; Run có tốc độ ×2. Esc/Android Back được dùng trong luồng điều hướng. Cần QA hành vi thoát/resume trên build mục tiêu.

| Màn | Nội dung cần cung cấp | Trạng thái |
|---|---|---|
| Menu | Chọn chế độ; Versus hiện có chọn hai vũ khí và arena | Hiện có; Run đang là nút ở dưới menu Versus |
| Run Title | Continue/New Run, best fight, số lần thắng | Hiện có; mô tả boss/mạng còn sai |
| Pick Weapon | 3 lựa chọn từ pool vũ khí đã mở | Hiện có |
| Start Pick | 3 thẻ; chọn một thẻ để bắt đầu build | Hiện có |
| Prepare | Đối thủ, arena, build, HP, trait, xu, thẻ, reroll và Fight | Hiện có |
| Countdown / Fight | HP/chỉ số/trạng thái, số trận, retry và diễn biến đấu | Hiện có |
| Fight Result | Thắng/thua/hòa, HP, damage, xu, hậu quả và đối thủ kế tiếp | Hiện có |
| Retry Pick | Một lượt chọn thẻ miễn phí rồi vào countdown | Hiện có |
| Run Summary | Kết quả, lịch sử trận, unlock | Hiện có; tổng số boss còn hardcode 3 |
| Progress / Next Goal | Điều kiện, phần thưởng và mức hoàn thành mục tiêu kế tiếp | Đề xuất |
| Audio Settings | Music/SFX volume, mute, lưu lựa chọn | Đề xuất |

**Yêu cầu UX đề xuất:** Run cần dễ tìm; đầu Run nêu mục tiêu 8 trận, 2 boss và 1 retry; phần thưởng phải có điều kiện rõ. Sau kết quả, nút chính dẫn tới bước tiếp theo phù hợp trạng thái. Chữ và vùng chạm được kiểm tra ở 270×480.

## 6. Luật chiến đấu hiện có

### 6.1. Bóng, vũ khí và va chạm

- Mỗi bên có một bóng, một vũ khí và các trait của loadout. Default bóng r=16, HP=100 trước modifier.
- Bóng tự di chuyển dưới trọng lực, nảy tường và va chạm; vũ khí xoay quanh bóng.
- Lưỡi vũ khí chạm thân đối thủ gây hit. Vũ khí không có lưỡi dùng đòn thân theo luật riêng.
- Hai lưỡi chạm nhau chặn hit của cặp đó và có thể parry: đảo chiều xoay, đẩy bật, hitstop.
- Đánh trúng kích hoạt tăng trưởng vũ khí theo luật của nó. Trait có thể bổ sung hiệu ứng hoặc thay đổi damage.
- Damage hit mặc định có cooldown 15 tick; parry cooldown 10 tick theo cặp. MANTIS dùng cooldown riêng 3 tick.
- Hitstop mặc định: hit 3 tick, parry 6 tick. Một tick = 1/60 giây mô phỏng.

### 6.2. Kết thúc trận

| Điều kiện | Kết quả |
|---|---|
| Một bên còn sống, bên kia bị hạ | Bên còn sống thắng |
| Hai bên bị hạ theo xử lý giao dịch damage | Hòa |
| Đạt giới hạn 180 giây active time | So HP theo phần trăm; bằng nhau thì hòa |

Classic bắt đầu 230×230; co tuyến tính từ giây active 90 đến 120 về 110×110. Active time dừng trong hitstop, nên thời gian thực có thể dài hơn. Cấu hình arena đặc biệt có thể thay đổi kích thước/trọng lực/hazard.

### 6.3. Nguồn sát thương và status

| Loại | Vai trò |
|---|---|
| Weapon | Đòn lưỡi hoặc đòn thân |
| Projectile | Đạn, tên/ngòi từ vũ khí |
| Status | Độc theo thời gian |
| Reflect | Phản đòn/va thân do cơ chế phản sát thương |
| Hazard | Tường gai hoặc chướng ngại gây damage |

Khiên chặn trọn một hit/shot; đòn bị khiên chặn không tạo tăng trưởng từ hit đó. Độc/reflect/hazard không được coi là hit thông thường. Các hiệu ứng hồi máu và phản đòn có điều kiện theo nguồn damage; text phải nêu đúng giới hạn để tránh hiểu sai build.

Độc có storage tối đa 8 stack ở tầng status. Vũ khí/trait đặt giới hạn riêng thấp hơn. Đạn có thể bị lưỡi deflect và đổi chủ theo luật Sim.

## 7. Roster vũ khí

**Hiện có:** 8 vũ khí. Tên người chơi thấy lấy từ `View/WeaponNames.cs`; id và tên registry nội bộ được giữ cho save/test. Các điểm mạnh/yếu dưới đây là vai trò thiết kế, không phải bảo đảm thắng mọi matchup.

| Tên / id | Bản sắc | Tăng trưởng và chỉ số chính hiện có | Đánh đổi |
|---|---|---|---|
| EMBER / blade | Than hồng, tăng damage đều | Lưỡi 24; spin 6°/tick; damage 1, +1 mỗi hit | Cần hit để lớn; parry làm gián đoạn nhịp |
| MANTIS / fang | Nhiều hit nhanh | Lưỡi 16; spin 8→40; gain đầu +3 và giảm ×0.98; damage 1 +0.4/hit; cooldown 3 tick | Tầm ngắn; ít lợi thế parry khi gặp đòn thân |
| ANGLER / pike | Tầm với tăng dần | Lưỡi 26→90; spin 4; damage đầu 1; mỗi hit +0.75 dài và damage | Nhịp chậm; lưỡi dài chịu ảnh hưởng không gian và parry |
| WRECKER / brawler | Đòn thân theo tốc độ | Damage = tốc độ × hệ số đầu 0.1; hệ số +0.01/hit; speed bonus −3→+4, tăng khi hit/tường | Không có lưỡi; bị phản đòn thân/hazard và tương tác build chi phối |
| SPARKLER / volley | Loạt đạn tăng dần | Bắn mỗi 90 tick; tối đa 8 đạn, thêm đạn khi melee hit; đạn đầu 0.9 damage, +0.1/hit | Lưỡi ngắn 12; đạn có thể bị deflect |
| SPORE / venom | Độc gây áp lực | Melee 1.5 +0.25/hit; độc 1 DPS ×4 giây; allowance stack 1→8 | Hiệu quả phụ thuộc nhịp/arena; không tăng vô hạn như damage EMBER |
| MIRROR / aegis | Phản đòn, hồi phục và mạnh dần theo thời gian | Rộng 7→14; +0.25/hit; parry phản 40%; đòn thân phản 50%; shot hit hồi 20% damage nhận; regen 0.25/s | Hành vi phản phụ thuộc loại đòn; poison không kích hoạt phản hit |
| HIVE / rig | Tích lũy công trình | Melee 1.5 +0.2/hit; hit đặt turret, tối đa 6; bắn mỗi 68 tick, shot 1.1 damage | Cần hit để dựng; turret chịu tác động của sân co |

MIRROR bắt đầu tăng hệ số damage sau 15 giây active; hệ số tăng tuyến tính thêm 1 mỗi 10 giây. Nó nhận shot rồi hồi một phần damage, không được mô tả thành chặn hoàn toàn shot.

**Chưa có trong code:** QUENCH, FRENZY DECAY, SWEET SPOT, FINALE, SWARM là các ý tưởng trong Weapon Bible. Không đưa vào luật hiện tại. Chốt từng thay đổi riêng, đo balance và cập nhật version nếu triển khai.

## 8. Trait và xây build

**Hiện có:** tối đa 3 trait khác nhau; mỗi trait tối đa cấp 2. Chọn lại trait đã có cấp 1 nâng lên cấp 2. Điều kiện hợp lệ áp dụng cho thẻ và enemy: trait cần lưỡi không đi với WRECKER; Glass Cannon bị cấm trên WRECKER.

| Trait | Cấp 1 hiện có | Cấp 2 hiện có | Điều kiện / lưu ý |
|---|---|---|---|
| Heavy | +35% MaxHP, radius ×1.2, giảm knockback 40% | +70%, ×1.35, giảm 60% | Radius tối đa 24; hiện không giảm speed; thân lớn dễ bị chạm |
| Vampire | Hồi 20% hit damage gây ra | 28% | Weapon/Projectile; không hồi từ poison/reflect/hazard |
| Spiky | Va thân gây 1 reflect damage | 1.5 | Cooldown 2 giây; xuyên khiên hit |
| Thorns | Trả 15% melee damage nhận | 22% | Không phản shot hoặc poison theo trait này |
| Second Wind | Một lần/trận: ở ≤30% HP hồi 20, hoặc cứu KO về 20 HP | 28 HP | Hai trigger dùng chung một lượt; không cộng thêm 20/28 lên HP âm khi cứu KO |
| Glass Cannon | Weapon/Projectile damage ×1.5; MaxHP −25 | ×1.9; −35 | MaxHP tối thiểu 20; không hợp lệ với WRECKER |
| Parry Master | Mỗi parry thứ 2 gọi tăng trưởng vũ khí | Mỗi parry | Cần lưỡi; có thể tăng đạn hoặc đặt turret theo vũ khí |
| Bubble | Khiên sau 25 giây không có khiên | 18 giây | Giữ tối đa 1 charge của trait; không có khiên ngay khi spawn |
| Twin Blade | Lưỡi sau dài 30% lưỡi chính | 40% | Cần lưỡi; melee damage ×0.45 ở cả hai cấp |
| Poison Tip | Poison 0.7 DPS ×3 giây, ngừng thêm khi target có ≥1 stack | 0.8 DPS ×4 giây, ngưỡng ≥2 | Cần lưỡi để hợp lệ; hit Weapon/Projectile kích hoạt, xét số stack độc từ mọi nguồn |

**Đã chốt, chưa có:** tài liệu cuối 11/10 ghi Vampire2=24%, Thorns2=18%, Heavy giữ nguyên; source hiện vẫn 28%/22%. Các đề xuất trần hồi Vampire phải được đối chiếu quyết định cuối trước code. Không dùng các số đo từ cấu hình thử làm kết quả của build hiện tại.

## 9. Run: cấu trúc và kinh tế

### 9.1. Khởi đầu

- Bắt đầu với base 100 HP và 2 xu; trait có thể sửa HP hiệu dụng trong trận.
- Mời tối đa 3 vũ khí khác nhau từ pool đã mở; hiện bốc theo seed, chưa có bảo đảm xuất hiện trong mỗi 3 lần mời.
- Chọn 1 trong 3 thẻ đầu Run; không reroll ở màn Start Pick.
- Sau lựa chọn đầu Run, vào Prepare trận 1 và có lượt chuẩn bị bình thường.
- Đối thủ và arena được sinh theo run seed/fight index; không sinh để khắc chế lựa chọn người chơi. Pool nội dung được snapshot khi bắt đầu Run.

### 9.2. Chuẩn bị và thẻ

| Thẻ hiện có | Tác động |
|---|---|
| Trait / upgrade | Thêm trait cấp 1 hoặc nâng trait lên cấp 2 |
| Damage Up | +15% bonus hit damage; không tăng poison/reflect/hazard |
| Speed Up | +10% trần speed; không tự tăng tốc độ spawn |
| Swap Weapon | Đổi vũ khí, giữ trait và stat; chỉ mời khi mọi trait hiện có vẫn hợp lệ |
| Heal | Hồi ngay 50% base MaxHP của build, kẹp ở MaxHP |
| MaxHP | +15 base MaxHP và +15 HP ngay |

Ba thẻ được rút theo trọng số, không lặp trong một offer. Heal không được mời khi đầy máu; trọng số tăng khi HP dưới 60%. Pool trait chia sẻ tổng trọng số để mở thêm trait không tự lấn hết thẻ stat/hồi máu.

| Giao dịch | Luật hiện có |
|---|---|
| Lượt pick đầu ở Prepare | Miễn phí |
| Mua thêm một thẻ | 3 xu; tối đa 2 pick ở Prepare |
| Reroll | Giá bắt đầu 1 xu, tăng 1 mỗi lần trong cùng Prepare |
| Sau khi đã pick | Không reroll |
| Bấm Fight | Khóa build/offer và lưu trạng thái trước trận |
| Xu giữa Run | Không mang sang Run mới |

### 9.3. Thắng, thua và retry

| Kết quả | HP / xu / tiến độ |
|---|---|
| Thắng trận | +3 xu; giữ HP theo tỷ lệ sau trận trên base MaxHP, cộng 35% base MaxHP, kẹp trần; sang trận kế |
| Thua hoặc hòa khi còn retry | +1 xu; dùng lượt retry duy nhất; HP về 60% base MaxHP; giữ fight index |
| Lượt retry | Cùng đối thủ và arena, seed trận mới; offer retry được sinh theo seed riêng; chọn 1 thẻ miễn phí rồi vào countdown; không reroll |
| Thua hoặc hòa sau khi đã dùng retry | +1 xu theo code; kết thúc Run, xu không được giữ cho Run mới |
| Thắng trận 8 | Thắng Run, kể cả thắng trong retry trận 8 |

Không có sàn hồi HP 60% sau chiến thắng. HP Run lưu theo số nguyên; modifier trận như Heavy/Glass Cannon không được cộng dồn vào base HP qua từng trận. Vũ khí và trait được tạo lại cho mỗi trận; tăng trưởng trong combat bắt đầu lại, còn cấu hình nâng cấp Run được giữ.

## 10. Đối thủ, boss và thử thách

### 10.1. Sinh đối thủ hiện tại

| Nhóm trận | Trait địch thường | Bonus HP địch thường |
|---|---|---|
| 1–3 | 0 | 0% |
| 4–6, trừ boss 4 | 0–1 | 0% |
| 7–8, trừ boss 8 | 1–2; có khả năng cấp 2 | +10% |

Boss ở trận 4 có base 160 HP, 0 random trait; boss trận 8 có base 300 HP, 2 random trait. Boss radius=24 trước áp dụng trait. Preview phải phản ánh HP/kích thước hiệu dụng sau spawn modifier.

### 10.2. Boss đã chốt, chưa có cơ chế riêng

| Boss | Cơ chế thiết kế | Bài học | Phản hồi cần có |
|---|---|---|---|
| SHELL KING — trận 4 | Spawn 3 shield; khi hết shield và HP>50%, hồi 1 shield mỗi 12 giây | Đọc shield; dùng nhiều hit nhỏ hoặc nguồn xuyên shield | Preview số shield; vòng shield đọc được; cảnh báo hồi và âm vỡ shield |
| MIMIC — trận 8 | Ghi damage Weapon/Projectile/Status/Reflect trong 20 giây; thích nghi nguồn lớn nhất, giảm 40%; nếu có ít nhất 2 nguồn mỗi nguồn ≥30% thì giảm 20% | Xây nhiều nguồn damage; đọc sự thích nghi | Preview; đổi màu, icon nguồn và event thích nghi; thông báo mức giảm |

MIMIC cần hook theo damage thực nhận và trạng thái thích nghi được hash. Hazard không dùng để chọn nguồn thích nghi. Quy tắc tie khi hai nguồn ngang nhau hoặc không nhận damage trong 20 giây **chưa chốt**.

Art hai boss đã có nhưng `BossArtEnabled=false`. Bản hiện tại dùng boss có chỉ số lớn và vũ khí/trait thông thường; không bật art/tên hứa hẹn cơ chế mới trước khi luật đó tồn tại.

WARDEN đã bị loại khỏi roster boss. Arena co sớm chuyển thành nội dung arena dự kiến. Boss riêng cần đo matchup và độ khó sau khi Sim baseline ổn định.

## 11. Đấu trường

| Arena hiện có | Đặc điểm | Tác động chiến thuật dự kiến |
|---|---|---|
| Classic | 230×230, co về 110×110 | Mốc so sánh chung |
| Pillar | Trụ tròn giữa sân, r=14 | Cản đường và thay đổi tiếp cận |
| Bumpers | 4 bumper, r=9, offset=95, boost 15% | Tăng va chạm và biến động tốc độ |
| Spikes | Wall damage cố định 2 mỗi lần chạm | Phạt va tường, thúc ép trận đấu |
| Low Grav | Gravity 0.02 thay cho default 0.05 | Thay đổi quỹ đạo/nơi va chạm |
| Tight | 170×170, co về 90×90 | Tăng áp lực cận chiến |
| Wide | 260×230 | Thêm khoảng cách ngang |

Run sinh arena theo seed; retry giữ arena. Versus cho chọn arena.

**Đã chốt, chưa có:** Spikes bắt đầu vô hại 15 giây, sau đó damage 1, tăng +1 mỗi 20 giây đến trần 3; cảnh báo 1 giây trước mỗi mức. Tính theo active time. SPORE được chấp nhận có lợi thế sân nhà; cần bù bằng preview và counter.

**Đã chốt, chưa có:** boss không sinh SPORE trên Spikes; ANTIDOTE bảo đảm khi Spikes + đối thủ SPORE/Poison Tip, kể cả retry. Hiện source chưa có bộ lọc này.

**Chưa có:** arena Closing với co sớm. Thông số cuối cần chốt trước triển khai.

## 12. ANTIDOTE và thẻ counter dự kiến

**Đã chốt, chưa có:** ANTIDOTE giảm 60% poison damage nhận trong một trận; không chiếm trait slot, dùng được làm pick miễn phí, pick thứ hai vẫn trả 3 xu. Hiệu ứng hết sau trận.

Ở offer trước Spikes + địch SPORE hoặc Poison Tip, bảo đảm một ô ANTIDOTE. Các lần gặp SPORE khác có đề xuất tăng trọng số counter; chốt cách rút/chèn trước code để replay theo seed ổn định. Chốt cách giữ hiệu ứng ở retry vì đó là trận mới.

Mẫu CardFace/asset ANTIDOTE hiện có phục vụ trình bày, chưa phải thẻ chơi được. Preview không được hứa giảm poison khi chưa có luật thật.

## 13. Progression dài hạn

### 13.1. Nội dung mở khóa hiện tại

| Điều kiện | Nội dung mở |
|---|---|
| Hồ sơ mới | EMBER, MANTIS, ANGLER, WRECKER; Heavy, Vampire, Spiky, Thorns, Second Wind |
| Thắng boss trận 4 — boss1 | Glass Cannon |
| Thắng boss trận 8 — boss2 | SPARKLER, Poison Tip |
| Thắng Run lần đầu — win1 | MIRROR, SPORE, Bubble, Parry Master |
| Thắng tổng cộng 3 Run — win3 | HIVE, Twin Blade |

Thắng boss trận 8 cũng thắng Run, nên các mốc boss2/win1 có thể phát cùng lúc. Legacy `boss3` còn trong bảng unlock cho dữ liệu cũ; logic Record hiện không tạo boss3 mới. Versus luôn cho dùng đủ 8 vũ khí.

ProgressData giữ achievements, số Run hoàn tất/bỏ, số Run thắng và BestFight. BestFight hiện là trận đã được xử lý kết quả xa nhất, có thể bao gồm trận thua. Không diễn giải BestFight thành số trận đã thắng. RunsPlayed tăng khi Run kết thúc hoặc bị bỏ, không ngay khi mở một Run mới.

### 13.2. Trình bày mục tiêu kế tiếp — đề xuất ưu tiên

UI hiển thị một mục tiêu chính có điều kiện, phần thưởng và mức hoàn thành lấy từ dữ liệu thật. Ví dụ: thắng boss trận 4 để mở Glass Cannon; thắng Run để mở các vũ khí/trait tương ứng; đã thắng 1/3 Run để mở HIVE/Twin Blade.

Sau trận/Run, hiện nội dung vừa mở và mục tiêu kế tiếp. Khi thua mà chưa đạt achievement, vẫn có thể ghi nhận kỷ lục mới; không tạo phần thưởng hoặc XP giả. Khi mở hết, hiện trạng thái hoàn thành và mời thử build mới; chưa tự tạo hệ cấp độ mới.

**Roadmap đề xuất:** cấp khó theo vũ khí, codex/thống kê, XP tài khoản, endless và daily challenge. Không thuộc bản hiện tại; cần thiết kế phần thưởng, save và điều kiện riêng.

## 14. UI, onboarding và text

**Onboarding đề xuất:** hướng dẫn ngắn tại đúng thời điểm lựa chọn, đủ cho người mới hiểu: chọn build trước trận, fight tự đánh, HP mang sang trận sau, một retry dùng cho cả Run, mở khóa qua thành tích.

| Quy tắc | Yêu cầu |
|---|---|
| Text theo luật | Số trận/boss/retry và chỉ số thẻ lấy từ nguồn tuning/rules chung |
| Text thẻ hiện tại | Title ≤18 ký tự; 2 detail line, mỗi line ≤44 ký tự |
| Font hiện tại | Pixel 3×5; uppercase A–Z, số và tập dấu hạn chế; tiếng Việt có dấu chưa có hỗ trợ đầy đủ |
| Tên vũ khí | Dùng tên người chơi thấy; id nội bộ giữ nguyên |
| Hành động phá run | Confirm trước khi thay Run đang lưu |
| Input | Có lock tránh click xuyên lúc mở dialog/chuyển màn; kiểm tra tap nhanh |
| Kết quả | Nêu hậu quả thật: HP, xu, retry, Run kết thúc và bước kế tiếp |

Hai lỗi đã xác nhận cần sửa: Run Title còn `3 BOSSES 3 LIVES`; Run Summary còn chia số boss cho 3. `UnlockRules.UnlockedNames` hiện đọc tên registry cũ, cần đồng bộ với tên View trong phần thưởng.

Tùy chọn giảm screen shake và trình bày status bằng icon/text ngoài màu là **đề xuất**. Mute không được làm mất thông tin gameplay thiết yếu. Chưa có thiết kế localization đầy đủ.

## 15. Mỹ thuật và VFX

- Native 270×480, PPU=1, Point filtering, phóng theo tỷ lệ pixel nguyên; dùng Pixel Perfect Camera/Windowbox.
- Nền tối, bóng/vũ khí nổi rõ; HUD không che vùng arena. Sprite chuyển động và VFX phải giữ khả năng đọc trên màn nhỏ.
- Body, lưỡi, projectile, status, số damage và UI có lớp hiển thị riêng.
- Đã có hit flash, hitstop, screen shake, particle, damage popup, HP bar, trait badge, shield/poison và một số phản hồi trait.
- Reflect dùng magenta `(255,90,200)`; poison lime `(96,255,64)`; heal xanh `(80,255,160)`; Second Wind có màu/label riêng.
- Tăng trưởng cần đọc được bằng chỉ số và hình dạng/tint. Những ý tưởng afterimage, heat thân WRECKER, nứt MIRROR và hoạt hình thắng riêng phải xác nhận thực thi riêng.

Art boss dùng sprite native 48×48 thay cho phóng gây lệch pixel. Các palette đề xuất trong Weapon Bible chưa được coi là toàn bộ màu đang chạy; ArtLibrary/asset và capture build cần được dùng để nghiệm thu màu thực tế.

## 16. Audio

### 16.1. Hiện trạng

SfxLibrary có 5 clip: hit, parry, wall, death, win; WAV generated là placeholder. SfxPlayer dùng pool AudioSource, mặc định FxView dùng 8 voice; giới hạn phát lặp clip gần nhau khoảng 30 ms. Chưa thấy music controller, nhạc nền hoặc cài đặt Music/SFX trong source khảo sát.

### 16.2. Thiết kế audio đề xuất

| Trạng thái/sự kiện | Âm thanh cần bổ sung | Hành vi |
|---|---|---|
| Menu / Run Title / Prepare | Một nhạc nền lặp | Dễ nghe lâu, hỗ trợ tập trung đọc build |
| Battle | Một nhạc trận lặp | Tạo nhịp, nhường khoảng nghe hit/parry |
| Boss | Track hoặc biến thể nhạc trận | Báo cột mốc thử thách; số track chốt theo nguồn lực |
| Thắng/thua trận | Cue riêng | Đồng bộ trạng thái kết quả, không phát thắng cho cả hai kết quả |
| Thắng/thua Run | Cue kết thúc | Phân biệt kết thúc hành trình với thắng trận thường |
| UI/chọn thẻ/reroll | Cue chọn/xác nhận/giao dịch | Phát đúng hành động thành công; không báo mua khi thiếu xu |
| Retry | Cue cảnh báo cơ hội cuối | Đồng bộ banner/trạng thái retry |
| Unlock | Cue nhận thưởng | Phát một lần khi achievement mới được ghi nhận |
| Khiên/độc/trait | Các cue cần thiết sau chốt | Có ưu tiên/cooldown để tránh dồn âm |

**Yêu cầu tích hợp đề xuất:** một nhạc chính tại một thời điểm; chuyển màn có fade phù hợp; Music/SFX volume độc lập, mute và lưu lựa chọn; ×2 không tự tăng pitch nhạc; âm thanh ở View, không thay kết quả Sim. Asset manifest ghi clip, nguồn/quyền dùng, loop, gain, trigger, stop/fade và giới hạn phát. Nghiệm thu bằng nghe thực tế.

## 17. Lưu game và tiếp tục

**Hiện có:** JSON cục bộ trong persistentDataPath; SaveData version=2; lưu progress và Run dở. RunFlow lưu sau thay đổi, khóa và lưu build trước khi bắt đầu trận. Thoát giữa trận rồi resume tái tạo trận bị khóa, không cho xem kết quả rồi đổi build để né trận.

Save cũ giữ progress và bỏ Run dở khi nâng định dạng. File không đọc được được giữ thành `.bad`, khởi tạo dữ liệu mới và log; chưa có cloud save hoặc tài khoản. GDD không coi cơ chế file tạm là bảo đảm chống mọi trường hợp mất dữ liệu.

QA cần kiểm tra: save mới/cũ/lỗi, đóng mở sau pick/reroll, giữa trận, trước/sau retry và sau unlock; xác nhận reward/stat không cộng lặp. Thay định dạng save phải có quy tắc migration riêng.

## 18. Replay, tính tái lập và PvP dự kiến

Trong cùng môi trường/rules/tuning, seed cùng loadout và lựa chọn Run dùng để tái tạo đối thủ, offer và trận. Đây là nền tảng debug/balance.

**Giới hạn hiện tại:** báo cáo 6a.5 ghi lệch trạng thái Mono so với .NET. Nền tảng Fix Q16.16 6a.6a có test chung, nhưng MatchSim/Vec2 chưa chuyển hoàn toàn. SimVersion.Rules hiện=4; không khẳng định toàn game đã tái lập giống nhau giữa nền tảng.

**Đã chốt, chưa có:** PvP giai đoạn đầu dùng mã chia sẻ loadout/seed/version, đấu cục bộ, không server; chung bộ thẻ với ngân sách điểm và tối đa một trait sustain. Không có ranked/2v2/FFA ở giai đoạn này.

Điều kiện trước khi hứa kết quả chia sẻ nhất quán: xác nhận determinism trên runtime/nền tảng mục tiêu; quy định version không tương thích; chốt ngân sách build và arena ban. BaaS/ranked trong các đoạn nghiên cứu cũ thuộc hướng tham khảo về sau.

## 19. Balance và phép đo

### 19.1. Gate hiện có trong test

| Gate | Ngưỡng / cấu hình |
|---|---|
| Matchup vũ khí không mirror | 30–70%; ngoại lệ tạm Fang→Rig tối đa 72%, xét lại ở 6c |
| Thời lượng matchup trung vị | 20–90 giây |
| Draw/cap | Draw <5%, cap <1% theo matchup |
| Trait cấp 1 | +3..+15 điểm win rate; Heavy tạm có sàn +1.5 |
| Trait cấp 2 | Delta lớn hơn cấp 1 |
| Random bot thắng Run | 1–6%; test hiện dùng 800 seed |
| Greedy bot thắng Run | 25–40%; test hiện dùng 800 seed |

Các gate này là code assertion hiện có, không phải số đo mới của GDD. Báo cáo 6a.6a ghi SimTests 178 pass và EditMode 189 pass/1 skip; không chạy lại trong đợt viết tài liệu.

### 19.2. Mục tiêu thiết kế cần đo sau thay đổi

- Đề xuất nhịp trận thường 25–60 giây; phân biệt với gate rộng 20–90 giây hiện tại.
- Đo độ rõ của lựa chọn, HP trước boss, lúc dùng retry, win rate retry và chọn Heal.
- Đo combo với build có trait tương đương, không chỉ so với vũ khí trơn.
- Retune từng nhóm, giữ seed/cấu hình thử và ghi version để so sánh có nghĩa.
- Ngưỡng combo ≤70% và các gate boss/counter trong bot-sweep-v4 là mục tiêu theo cấu hình thiết kế; không đưa số đo cấu hình cũ sang baseline mới.
- Không nới golden/threshold để che regression; thay luật phải ghi quyết định và tái đo.

## 20. Nghiệm thu bản hoàn thiện gần nhất

### 20.1. Chức năng

1. Chơi từ hồ sơ mới qua chọn vũ khí/thẻ, Prepare, trận, kết quả, retry hoặc kết thúc Run.
2. UI đúng 8 trận/2 boss/1 retry, cùng dữ liệu gameplay; text tên vũ khí và unlock nhất quán.
3. Thắng/thua/hòa, hồi HP, xu, giá reroll, lock thẻ và replay retry đúng luật ở phần 9.
4. Tiến độ/mở khóa giữ sau đóng mở; stat/reward không ghi lặp; save cũ xử lý đúng policy.
5. Music/SFX chuyển trạng thái đúng, không chồng nhạc, volume lưu lại và ×2 không làm nhạc méo.
6. 270×480 đọc được text/HP/status và dùng được input; không click xuyên dialog/chuyển màn.
7. Thay đổi code hoàn tất gate repo: SimTests, Unity compile và EditMode; audio/art kiểm tra thực tế.

### 20.2. Trải nghiệm — mục tiêu thăm dò đề xuất

Quan sát 5 người mới trên cùng build trước khi giải thích. Ít nhất 4/5 người xác định được mục tiêu và bước tiếp theo. Ghi vì sao chọn thẻ, hiểu retry, nhận biết unlock và lý do chơi tiếp/thoát. Mẫu nhỏ dùng phát hiện vấn đề, không chứng minh retention hay balance.

## 21. Backlog theo khoảng cách hiện trạng–thiết kế

| ID | Việc | Trạng thái / ưu tiên |
|---|---|---|
| GDD-01 | Sửa mô tả boss/mạng và total boss; đồng bộ tên unlock | Lỗi UI xác nhận, P0 |
| GDD-02 | Ưu tiên đường vào Run, mục tiêu và onboarding | Đề xuất, P0 |
| GDD-03 | Hiển thị milestone/next unlock từ progress hiện có | Đề xuất, P0 |
| GDD-04 | Music, UI/result/unlock cue, Music/SFX settings | Đề xuất theo nhu cầu người dùng, P0 |
| GDD-05 | Shell King trước, Mimic sau | Đã chốt nhưng cơ chế chưa có, P1 sau baseline Sim |
| GDD-06 | Spikes leo thang, filter boss và ANTIDOTE | Đã chốt, chưa có; tích hợp một gói nhất quán sau baseline Sim |
| GDD-07 | Retune Vampire/Thorns và đo combo | Chênh lệch spec/source; chốt phạm vi và chạy sweep |
| GDD-08 | Hoàn tất determinism giữa runtime | Có nền Fix, migration chưa xong; cần trước PvP chia sẻ chính xác |
| GDD-09 | Bảo đảm/ưu tiên vũ khí khởi đầu chưa chơi | Đề xuất trong thiết kế, chưa có |
| GDD-10 | Cấp khó/codex/XP/endless/daily | Roadmap, chưa chốt phạm vi bản gần nhất |

## 22. Phân công và quản lý thay đổi

| Vai trò | Trách nhiệm GDD |
|---|---|
| Projects Manager | Version GDD/task, baseline build, ưu tiên, quyết định và file ownership |
| Game Designer | Luật, milestone, counter, economy, balance và spec thay đổi |
| Art Design – VFX | Mockup/asset manifest, tín hiệu mục tiêu/status/boss |
| SFX | Cue sheet, track/clip, nguồn asset và nghiệm thu nghe |
| Agent code | Triển khai theo spec, kiểm tra, bàn giao diff/baseline |
| Dev – Code review | Kiểm tra contract, correctness, save/determinism và acceptance |
| Game Tester | Test chức năng/hồi quy trên build có revision/patch manifest |
| Playtest Operator | Hành vi người mới, vấn đề UX và động lực chơi tiếp |
| Review language | Text đúng luật, tên nhất quán và giới hạn font/layout |

Một thay đổi luật có một task chính thức: mục tiêu, version, phạm vi file, acceptance, save/Sim impact, người chốt và bằng chứng kiểm tra. Góp ý cập nhật task; không duy trì nhiều prompt cuối mâu thuẫn. Reviewer tập trung diff; sửa gom theo lượt. Các task chung module và đợt migration Sim tích hợp tuần tự.

Kế hoạch triển khai chi tiết: [core loop/audio/team](../../plans/261013-core-loop-audio-team/plan.md); [brief từng bot](../../plans/261013-core-loop-audio-team/team-briefs.md).

## 23. Nguồn đối chiếu

Các link dưới là nguồn nội bộ. Tài liệu có lịch sử nhiều phương án cần đọc phần quyết định cuối và đối chiếu source.

- [Project overview](../project-overview-pdr.md), [architecture](../system-architecture.md), [code standards](../code-standards.md).
- [Weapon Bible](./weapon-bible.md), [Card Pool v2](./card-pool-v2.md), [boss/loss rules](./boss-and-loss-rules.md), [bot sweep](./bot-sweep-v4.md), [PvP](./pvp-tech.md).
- [Báo cáo Run 6b](../claude-runs/6b.md), [divergence 6a.5](../claude-runs/6a5.md), [Fix foundation 6a.6a](../claude-runs/6a6.md).
- [RunTuning](../../BallBattleUnity/Assets/Scripts/Sim/Run/RunTuning.cs), [RunState](../../BallBattleUnity/Assets/Scripts/Sim/Run/RunState.cs), [CardOffer](../../BallBattleUnity/Assets/Scripts/Sim/Run/CardOffer.cs), [UnlockRules](../../BallBattleUnity/Assets/Scripts/Sim/Run/UnlockRules.cs).
- [WeaponNames](../../BallBattleUnity/Assets/Scripts/View/WeaponNames.cs), [WeaponTuning](../../BallBattleUnity/Assets/Scripts/Sim/Weapons/WeaponTuning.cs), [TraitTuning](../../BallBattleUnity/Assets/Scripts/Sim/Traits/TraitTuning.cs), [ArenaRegistry](../../BallBattleUnity/Assets/Scripts/Sim/Arena/ArenaRegistry.cs).
- [MatchConfig](../../BallBattleUnity/Assets/Scripts/Sim/MatchConfig.cs), [SaveStore](../../BallBattleUnity/Assets/Scripts/View/Run/SaveStore.cs), [RunFlow](../../BallBattleUnity/Assets/Scripts/View/Run/RunFlow.cs), [SfxLibrary](../../BallBattleUnity/Assets/Scripts/View/SfxLibrary.cs).

## 24. Quyết định còn mở

1. Nền tảng phát hành đầu tiên và mức ưu tiên determinism giữa runtime trước bản chơi mới.
2. Bố cục menu: Run mặc định hay hub hai chế độ; mục tiêu progression ưu tiên hiển thị.
3. Thứ tự triển khai boss/Spikes/counter sau khi Sim baseline ổn định; các tham số và edge case MIMIC còn thiếu.
4. Retune và trần hồi Vampire cuối cùng; xác nhận ngưỡng đo trên cùng bộ luật/version.
5. Quy tắc thẻ counter khi retry, cách rút/chèn và phạm vi tất cả counter trong Card Pool v2.
6. Track/cue cuối, phong cách nhạc, asset source và thông số volume/fade.
7. Ngân sách điểm PvP, định nghĩa sustain, arena ban và mã chia sẻ.
8. Chính sách localization, tính năng accessibility và mô hình phát hành/kiếm tiền.

Các mục này chưa được điền bằng giả định để giao code. PM/Designer chốt trong task liên quan trước triển khai.
