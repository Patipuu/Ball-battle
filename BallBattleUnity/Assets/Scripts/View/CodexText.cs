using System.Collections.Generic;

namespace BallBattle.View
{
    /// <summary>
    /// CODEX copy per ball: an epithet over the name, a plain intro that says exactly what grows and by how much (numbers from
    /// WeaponTuning / TributeTuning), what the skill does, and one play tip. Vietnamese is written first, not translated line by
    /// line. BGLGXLQ (the church-group tribute) is written in the Catholic spirit of unity and love. Lines fit 238 px at scale 1.
    /// </summary>
    public static class CodexText
    {
        public sealed class Entry
        {
            public string TitleVi, TitleEn;
            public string[] IntroVi, IntroEn, SkillVi, SkillEn, TipVi, TipEn;
        }

        static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>
        {
            { "blade", new Entry {
                TitleVi = "LƯỠI ĐAO HỎA NGỤC", TitleEn = "THE BURNING EDGE",
                IntroVi = new[] { "ĐAO CỦA EMBER KHỞI ĐẦU VỚI 1 SÁT THƯƠNG.", "MỖI NHÁT CHÉM TRÚNG ĐÍCH CỘNG THÊM 1 SÁT", "THƯƠNG CHO TẤT CẢ CÁC NHÁT SAU. NHÁT", "BỊ ĐỠ THÌ KHÔNG ĐƯỢC CỘNG." },
                IntroEn = new[] { "THE BLADE OF EMBER STARTS AT 1 DAMAGE.", "EVERY BLADE HIT ADDS 1 DAMAGE TO ALL", "LATER HITS. A PARRIED HIT ADDS NOTHING." },
                SkillVi = new[] { "EMBER LƯỚT TỚI VÀ ĐỂ LẠI MỘT VỆT LỬA", "SAU LƯNG. KẺ ĐỊCH CHẠM VÀO VỆT LỬA BỊ", "THIÊU ĐỐT, MẤT MÁU LIÊN TỤC." },
                SkillEn = new[] { "EMBER DASHES AND LEAVES A FIRE TRAIL.", "A FOE THAT TOUCHES THE TRAIL BURNS", "AND KEEPS LOSING HP." },
                TipVi = new[] { "CỨ CHÉM ĐỀU TAY. SAU 10 NHÁT TRÚNG, MỖI", "NHÁT ĐÃ GÂY 11 SÁT THƯƠNG." },
                TipEn = new[] { "KEEP SWINGING. AFTER 10 HITS EVERY", "HIT DEALS 11 DAMAGE." } } },
            { "fang", new Entry {
                TitleVi = "LƯỠI HÁI CUỒNG PHONG", TitleEn = "THE WHIRLING SCYTHE",
                IntroVi = new[] { "MANTIS XOAY LƯỠI HÁI QUANH MÌNH. MỖI ĐÒN", "TRÚNG CỘNG 0,44 SÁT THƯƠNG VÀ LÀM LƯỠI", "HÁI XOAY NHANH HƠN. MỨC TĂNG TỐC ĐỘ", "XOAY NHỎ DẦN SAU MỖI ĐÒN." },
                IntroEn = new[] { "MANTIS SPINS A SCYTHE AROUND ITSELF.", "EVERY HIT ADDS 0.44 DAMAGE AND SPINS IT", "FASTER, BY A LITTLE LESS EACH TIME." },
                SkillVi = new[] { "MANTIS BIẾN RA SAU LƯNG KẺ ĐỊCH, CHÉM", "MỘT NHÁT, RỒI NÉ ĐƯỢC MỌI ĐÒN TRONG", "MỘT KHOẢNG NGẮN." },
                SkillEn = new[] { "MANTIS BLINKS BEHIND THE FOE, SLASHES", "ONCE AND DODGES EVERY HIT FOR A", "SHORT TIME." },
                TipVi = new[] { "LÚC ĐẦU LƯỠI HÁI XOAY CHẬM, DỄ BỊ ĐỠ.", "DÙNG ẢO ẢNH ĐỂ VÒNG RA SAU MÀ CHÉM." },
                TipEn = new[] { "EARLY ON THE SCYTHE IS SLOW AND EASY", "TO PARRY. BLINK BEHIND AND HIT." } } },
            { "pike", new Entry {
                TitleVi = "KẺ BUÔNG CÂU", TitleEn = "THE PATIENT HOOK",
                IntroVi = new[] { "ANGLER CẦM MỘT CÂY GIÁO DÀI. MỖI ĐÒN", "ĐÂM TRÚNG CỘNG 0,75 SÁT THƯƠNG VÀ LÀM", "GIÁO DÀI RA, TỐI ĐA GẤP HƠN BA LẦN", "ĐỘ DÀI BAN ĐẦU." },
                IntroEn = new[] { "ANGLER HOLDS A LONG PIKE. EVERY HIT", "ADDS 0.75 DAMAGE AND MAKES THE PIKE", "LONGER, UP TO MORE THAN 3 TIMES." },
                SkillVi = new[] { "ANGLER PHÓNG MÓC CÂU VỀ PHÍA KẺ ĐỊCH.", "TRÚNG ĐÍCH THÌ KÉO KẺ ĐỊCH LẠI GẦN VÀ", "LÀM CHOÁNG MỘT LÚC." },
                SkillEn = new[] { "ANGLER THROWS A HOOK AT THE FOE. ON A", "HIT IT PULLS THE FOE IN AND STUNS IT", "FOR A MOMENT." },
                TipVi = new[] { "GIÁO CÀNG DÀI CÀNG DỄ BỊ ĐỠ. MÓC KẺ", "ĐỊCH LẠI GẦN RỒI HÃY ĐÂM." },
                TipEn = new[] { "A LONG PIKE IS EASY TO PARRY. HOOK", "THE FOE IN FIRST, THEN STRIKE." } } },
            { "brawler", new Entry {
                TitleVi = "QUẢ TẠ PHÁ THÀNH", TitleEn = "THE WALL BREAKER",
                IntroVi = new[] { "WRECKER KHÔNG CÓ VŨ KHÍ, NÓ ĐÂM THẲNG", "VÀO KẺ ĐỊCH. SÁT THƯƠNG TÍNH THEO TỐC", "ĐỘ LÚC VA CHẠM. MỖI LẦN NẢY TƯỜNG HOẶC", "ĐÂM TRÚNG, TỐC ĐỘ TỐI ĐA TĂNG LÊN." },
                IntroEn = new[] { "WRECKER HAS NO WEAPON. IT RAMS THE FOE", "AND HITS HARDER THE FASTER IT MOVES.", "WALL BOUNCES AND HITS RAISE ITS TOP SPEED." },
                SkillVi = new[] { "CHẠM LẦN 1 ĐỂ GỒNG, CHẠM LẦN 2 ĐỂ HÚC.", "GỒNG LÂU THÌ HÚC MẠNH HƠN, NHƯNG QUÁ", "GIỚI HẠN THÌ WRECKER TỰ BỊ CHOÁNG." },
                SkillEn = new[] { "TAP ONCE TO WIND UP, TAP AGAIN TO RAM.", "A LONGER WIND UP HITS HARDER, BUT HOLD", "TOO LONG AND WRECKER STUNS ITSELF." },
                TipVi = new[] { "CHO WRECKER NẢY TƯỜNG VÀI LẦN ĐỂ LẤY", "TỐC ĐỘ, RỒI MỚI GỒNG HÚC." },
                TipEn = new[] { "LET WRECKER BOUNCE OFF A FEW WALLS", "FOR SPEED, THEN WIND UP AND RAM." } } },
            { "volley", new Entry {
                TitleVi = "PHÁO THỦ TÓE LỬA", TitleEn = "THE SPARK GUNNER",
                IntroVi = new[] { "CỨ 1,5 GIÂY SPARKLER BẮN MỘT LOẠT ĐẠN.", "MỖI LẦN ĐÁNH TRÚNG BẰNG LƯỠI NGẮN, LOẠT", "SAU CÓ THÊM 1 VIÊN, TỐI ĐA 8 VIÊN.", "ĐẠN NẢY LẠI KHI CHẠM TƯỜNG." },
                IntroEn = new[] { "EVERY 1.5 SECONDS SPARKLER FIRES A", "VOLLEY. EACH CLOSE HIT ADDS 1 SHOT TO", "LATER VOLLEYS, UP TO 8. SHOTS BOUNCE." },
                SkillVi = new[] { "SPARKLER BẮN MỘT VIÊN ĐẠN LỚN. VIÊN", "ĐẠN DỘI KHỎI TƯỜNG VÀI LẦN TRƯỚC KHI", "BIẾN MẤT." },
                SkillEn = new[] { "SPARKLER FIRES ONE BIG BOLT THAT", "BOUNCES OFF THE WALLS A FEW TIMES." },
                TipVi = new[] { "SÂN RỘNG HỢP VỚI SPARKLER. TRONG SÂN", "HẸP, ĐẠN DỄ BỊ ĐỠ VÀ ĐÁNH BẬT LẠI." },
                TipEn = new[] { "WIDE ARENAS SUIT SPARKLER. IN TIGHT", "ONES ITS SHOTS GET PARRIED BACK." } } },
            { "venom", new Entry {
                TitleVi = "MẦM ĐỘC LẶNG THẦM", TitleEn = "THE SILENT ROT",
                IntroVi = new[] { "MỖI ĐÒN TRÚNG CỦA SPORE ĐẶT MỘT LỚP ĐỘC", "LÊN KẺ ĐỊCH. MỖI LỚP GÂY 1 SÁT THƯƠNG", "MỖI GIÂY TRONG 4 GIÂY. SỐ LỚP ĐỘC TỐI", "ĐA TĂNG DẦN THEO SỐ ĐÒN TRÚNG." },
                IntroEn = new[] { "EVERY SPORE HIT PUTS A POISON STACK ON", "THE FOE: 1 DAMAGE A SECOND FOR 4", "SECONDS. HITS RAISE THE STACK LIMIT." },
                SkillVi = new[] { "SPORE THẢ MỘT ĐÁM MÂY BÀO TỬ. KẺ ĐỊCH", "Ở TRONG MÂY BỊ LÀM CHẬM, CÒN SPORE Ở", "TRONG MÂY THÌ ĐƯỢC HỒI MÁU." },
                SkillEn = new[] { "SPORE RELEASES A SPORE CLOUD. FOES", "INSIDE ARE SLOWED, AND SPORE HEALS", "WHILE IT STAYS INSIDE." },
                TipVi = new[] { "ĐẦU TRẬN SPORE YẾU. NÉ BỚT ĐÒN, ĐÁNH", "CẦM CHỪNG ĐỂ ĐỘC CÓ THỜI GIAN NGẤM." },
                TipEn = new[] { "SPORE IS WEAK EARLY. DODGE, CHIP AWAY", "AND GIVE THE POISON TIME." } } },
            { "aegis", new Entry {
                TitleVi = "TẤM KHIÊN PHẢN CHIẾU", TitleEn = "THE SHINING WARD",
                IntroVi = new[] { "MIRROR CẦM MỘT TẤM KHIÊN GƯƠNG. ĐỠ ĐƯỢC", "NHÁT CHÉM THÌ TRẢ LẠI 45% SÁT THƯƠNG,", "BỊ HÚC THÌ TRẢ LẠI 56%. MỖI LẦN ĐỠ HAY", "TRÚNG ĐÒN, MIRROR HỒI MỘT ÍT MÁU." },
                IntroEn = new[] { "MIRROR HOLDS A GLASS SHIELD. A PARRIED", "SLASH SENDS BACK 45% DAMAGE, A RAM 56%.", "EVERY BLOCK OR HIT TAKEN HEALS IT A BIT." },
                SkillVi = new[] { "CHẠM NGAY TRƯỚC KHI BỊ ĐÁNH TRÚNG. NẾU", "ĐÚNG LÚC, MIRROR CHẶN ĐÒN ĐÓ VÀ ĐÁNH", "BẬT NGƯỢC VỀ PHÍA KẺ ĐỊCH." },
                SkillEn = new[] { "TAP JUST BEFORE A HIT LANDS. ON TIME,", "MIRROR BLOCKS IT AND THROWS IT BACK", "AT THE FOE." },
                TipVi = new[] { "ĐỪNG VỘI TẤN CÔNG. ĐỂ KẺ ĐỊCH RA ĐÒN", "TRƯỚC RỒI CHẠM ĐỠ ĐÚNG LÚC." },
                TipEn = new[] { "DO NOT RUSH. LET THE FOE SWING FIRST,", "THEN TAP TO PARRY ON TIME." } } },
            { "rig", new Entry {
                TitleVi = "TỔ ONG CƠ KHÍ", TitleEn = "THE IRON HIVE",
                IntroVi = new[] { "MỖI ĐÒN TRÚNG CỦA HIVE ĐẶT MỘT TRỤ SÚNG", "NHỎ XUỐNG SÂN, TỐI ĐA 6 TRỤ. KHOẢNG", "MỖI GIÂY, TẤT CẢ CÁC TRỤ CÙNG BẮN VỀ", "PHÍA KẺ ĐỊCH." },
                IntroEn = new[] { "EVERY HIVE HIT DROPS A SMALL TURRET,", "UP TO 6. ABOUT ONCE A SECOND EVERY", "TURRET FIRES AT THE FOE." },
                SkillVi = new[] { "HIVE THẢ RA MỘT BẦY ONG MÁY. CHÚNG TỰ", "BAY ĐUỔI THEO KẺ ĐỊCH VÀ ĐỐT KHI CHẠM", "VÀO." },
                SkillEn = new[] { "HIVE RELEASES A SWARM OF DRONES THAT", "CHASE THE FOE AND STING ON CONTACT." },
                TipVi = new[] { "ĐẦU TRẬN HIVE CHƯA CÓ TRỤ NÊN KHÁ YẾU.", "GIỮ MÁU ĐẾN KHI DỰNG ĐỦ TRỤ." },
                TipEn = new[] { "HIVE IS WEAK UNTIL THE TURRETS ARE UP.", "STAY HEALTHY UNTIL THEN." } } },
            // BGLGXLQ = Ban Giáo Lý Giáo xứ Lạc Quang: the five orbs are the five scarf colours (colours only, no group names).
            { "chorus", new Entry {
                TitleVi = "HIỆP NHẤT TRONG YÊU THƯƠNG", TitleEn = "ONE IN LOVE",
                IntroVi = new[] { "NĂM QUẢ CẦU LÀ NĂM MÀU KHĂN, GẮN BÓ", "VỚI NHAU TRONG YÊU THƯƠNG. CHẠM KẺ ĐỊCH", "ĐỂ THẮP SÁNG TỪNG MÀU. HỒNG HỒI MÁU,", "LỤC THÊM TỐC ĐỘ, LAM CHẶN 1 ĐÒN,", "VÀNG TĂNG SỨC ĐÁNH, ĐỎ THIÊU ĐỐT KẺ ĐỊCH." },
                IntroEn = new[] { "FIVE ORBS, FIVE SCARF COLORS BOUND", "TOGETHER IN LOVE. TOUCH THE FOE TO", "LIGHT THEM: PINK HEALS, GREEN SPEEDS UP,", "BLUE GUARDS 1 HIT, YELLOW HITS HARDER,", "RED BURNS THE FOE." },
                SkillVi = new[] { "KHI CÁC MÀU ĐÃ SÁNG, CHẠM ĐỂ HIỆP NHẤT.", "NĂM MÀU KHĂN HÒA LÀM MỘT, TỎA RA GÂY", "SÁT THƯƠNG LÊN KẺ ĐỊCH Ở GẦN." },
                SkillEn = new[] { "WHEN THE COLORS ARE LIT, TAP FOR UNITY.", "THE FIVE SCARVES BECOME ONE LIGHT AND", "HURT NEARBY FOES." },
                TipVi = new[] { "CÓ Ở GẦN NHAU MỚI THẮP ĐƯỢC MÀU. HÃY", "ÁP SÁT, CHỜ ĐỦ NĂM MÀU RỒI HIỆP NHẤT." },
                TipEn = new[] { "THE COLORS ONLY LIGHT UP CLOSE. STAY", "NEAR AND WAIT FOR ALL FIVE." } } },
            { "serpent", new Entry {
                TitleVi = "MÃNG XÀ SĂN MỒI", TitleEn = "THE COILED HUNTER",
                IntroVi = new[] { "MỒI NHỎ XUẤT HIỆN TRÊN SÂN, HUNTER TỰ", "BÒ TỚI ĂN. MỖI CON ĂN ĐƯỢC HỒI 1 MÁU VÀ", "MỌC THÊM 1 ĐỐT ĐUÔI, TỐI ĐA 8 ĐỐT. MỖI", "ĐỐT LÀM CÚ CẮN MẠNH THÊM 0,3." },
                IntroEn = new[] { "PREY APPEARS ON THE FIELD AND HUNTER", "GOES FOR IT. EACH MEAL HEALS 1 HP AND", "GROWS 1 TAIL SEGMENT, UP TO 8. EACH", "SEGMENT ADDS 0.3 BITE DAMAGE." },
                SkillVi = new[] { "HUNTER LAO TỚI QUẤN LẤY KẺ ĐỊCH, GIỮ", "CHẶT VÀ CẮN 3 LẦN TRƯỚC KHI NHẢ RA." },
                SkillEn = new[] { "HUNTER LUNGES, COILS AROUND THE FOE", "AND BITES 3 TIMES BEFORE LETTING GO." },
                TipVi = new[] { "BỊ CHÉM TRÚNG ĐUÔI SẼ MẤT ĐỐT. TRÁNH", "XA LƯỠI DÀI, ĂN MỒI TRƯỚC RỒI HÃY ĐÁNH." },
                TipEn = new[] { "A CUT TAIL LOSES SEGMENTS. AVOID LONG", "BLADES, EAT FIRST, THEN FIGHT." } } },
        };

        public static Entry For(string weaponId) => weaponId != null && Entries.TryGetValue(weaponId, out var e) ? e : null;

        static bool Vi => StringTable.Current == Lang.Vi;
        public static string Title(Entry e) => Vi ? e.TitleVi : e.TitleEn;
        public static string[] Intro(Entry e) => Vi ? e.IntroVi : e.IntroEn;
        public static string[] Skill(Entry e) => Vi ? e.SkillVi : e.SkillEn;
        public static string[] Tip(Entry e) => Vi ? e.TipVi : e.TipEn;
    }
}
