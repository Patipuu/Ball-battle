using UnityEngine;

namespace BallBattle.View
{
    /// <summary>One thing the guide says: Vietnamese and English text (upper case, the pixel font has no lower case) and,
    /// optionally, the part of the screen it talks about (highlighted, the rest dimmed).</summary>
    public sealed class GuideLine
    {
        public readonly string Vi, En;
        public readonly Rect? Focus;
        public GuideLine(string vi, string en, Rect? focus = null) { Vi = vi; En = en; Focus = focus; }
        public string Text(Lang lang) => lang == Lang.Vi ? Vi : En;
    }

    /// <summary>A short conversation shown once, the first time its screen appears (GuideDirector decides when).</summary>
    public sealed class GuideScene
    {
        public readonly string Id;
        public readonly GuideLine[] Lines;
        public GuideScene(string id, params GuideLine[] lines) { Id = id; Lines = lines; }
    }

    /// <summary>
    /// Everything the guide ball explains (Phu 21/10: "đảm bảo có tutorial cho toàn bộ chức năng"): account, hub, Run (chapters,
    /// heat, ball pick, cards, fight, result, boss, summary), balls and the skill tree, quests and chests, codex, settings and PvP
    /// (defense, offers, blind bans, pick, result, history). Screen rects are the screens' own constants where they exist.
    /// </summary>
    public static class GuideScripts
    {
        // PvP screen layout (PvpView.Build).
        static readonly Rect PvpCards = new Rect(-112, -2, 224, 128);   // PvpView.CardRect 0..2
        static readonly Rect PvpBans = new Rect(-112, -90, 224, 58);
        static readonly Rect PvpBall = new Rect(-112, -58, 224, 26);
        static readonly Rect PvpAction = new Rect(12, -124, 100, 27);
        static readonly Rect PvpBottom = new Rect(-112, -158, 224, 61);
        static readonly Rect HudBottom = new Rect(-135, -238, 270, 116);
        static readonly Rect FightControls = new Rect(-126, 214, 69, 26);
        static readonly Rect ChapterRow = new Rect(-125, -62, 250, 40);
        static readonly Rect HeatRow = new Rect(-117, -96, 234, 26);
        static readonly Rect GoalBox = new Rect(-127, -64, 254, 60);
        static readonly Rect FoeBox = new Rect(-125, 113, 250, 83);
        static Rect Union(Rect a, Rect b) => Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));

        public static readonly GuideScene Welcome = new GuideScene("welcome",
            new GuideLine("XIN CHÀO CAO THỦ CHƠI BÓNG! MÌNH LÀ BI, QUẢ BÓNG HƯỚNG DẪN CỦA BALL BATTLE.",
                          "HELLO, BALL MASTER! I AM BI, YOUR GUIDE BALL IN BALL BATTLE."),
            new GuideLine("Ở ĐÂY CÁC QUẢ BÓNG TỰ ĐÁNH NHAU. CAO THỦ CHỌN BÓNG, CHỌN THẺ VÀ CHẠM ĐÚNG LÚC ĐỂ TUNG KỸ NĂNG.",
                          "HERE THE BALLS FIGHT ON THEIR OWN. YOU PICK THE BALL AND THE CARDS, AND TAP AT THE RIGHT TIME TO FIRE ITS SKILL."),
            new GuideLine("TRƯỚC TIÊN HÃY TẠO TÀI KHOẢN: GÕ TÊN, MẬT KHẨU RỒI NHẬP LẠI MẬT KHẨU CHO CHẮC.",
                          "FIRST, MAKE AN ACCOUNT: TYPE A NAME, A PASSWORD, THEN THE PASSWORD AGAIN TO BE SURE.", Union(AccountView.NameArea, AccountView.ConfirmArea)),
            new GuideLine("TÀI KHOẢN LƯU TIẾN TRÌNH TRÊN MẠNG VÀ LÀ TÊN CỦA CAO THỦ Ở PVP. ĐỔI MÁY CHỈ CẦN ĐĂNG NHẬP LẠI.",
                          "YOUR ACCOUNT KEEPS YOUR PROGRESS ONLINE AND IS YOUR NAME IN PVP. ON A NEW DEVICE, JUST LOG IN."),
            new GuideLine("LẦN ĐẦU THÌ BẤM ĐĂNG KÝ. ĐÃ CÓ TÀI KHOẢN THÌ BẤM ĐĂNG NHẬP.",
                          "NEW HERE? PRESS REGISTER. HAVE AN ACCOUNT ALREADY? PRESS LOG IN.", Union(AccountView.RegisterArea, AccountView.LoginArea)));

        public static readonly GuideScene MenuStart = new GuideScene("menu_start",
            new GuideLine("ĐÂY LÀ SẢNH CHÍNH. MỌI CUỘC PHIÊU LƯU BẮT ĐẦU TỪ NÚT VƯỢT ẢI.",
                          "THIS IS THE HUB. EVERY ADVENTURE STARTS FROM THE RUN BUTTON.", MenuView.RunArea),
            new GuideLine("MỖI LƯỢT VƯỢT ẢI CÓ 8 TRẬN VÀ 2 TRÙM. THẮNG CẢ 8 LÀ QUA CHƯƠNG.",
                          "A RUN IS 8 FIGHTS WITH 2 BOSSES. WIN ALL 8 TO CLEAR THE CHAPTER."),
            new GuideLine("NÚT BÁNH RĂNG Ở GÓC LÀ CÀI ĐẶT: ÂM THANH, NGÔN NGỮ, TÀI KHOẢN VÀ XEM LẠI HƯỚNG DẪN.",
                          "THE GEAR IN THE CORNER IS SETTINGS: SOUND, LANGUAGE, ACCOUNT AND THIS GUIDE AGAIN.", SettingsView.GearArea),
            new GuideLine("GIỜ CHẠM VƯỢT ẢI ĐỂ BẮT ĐẦU NHÉ!", "NOW TAP RUN TO BEGIN!", MenuView.RunArea));

        public static readonly GuideScene RunTitle = new GuideScene("run_title",
            new GuideLine("ĐÂY LÀ MÀN CHỌN CHƯƠNG. MỖI CHƯƠNG CÓ CẶP TRÙM, BÓNG ĐỊCH, SÀN ĐẤU VÀ KHUNG CẢNH RIÊNG.",
                          "PICK A CHAPTER HERE. EACH ONE HAS ITS OWN BOSS PAIR, ENEMY BALLS, ARENAS AND SCENERY.", ChapterRow),
            new GuideLine("QUA MỘT CHƯƠNG SẼ MỞ CHƯƠNG TIẾP THEO. DÙNG HAI MŨI TÊN ĐỂ XEM CÁC CHƯƠNG.",
                          "CLEARING A CHAPTER OPENS THE NEXT. USE THE ARROWS TO LOOK AROUND.", Union(Rect.MinMaxRect(-125, -62, -99, -26), Rect.MinMaxRect(99, -62, 125, -26))),
            new GuideLine("HEAT LÀ ĐỘ KHÓ TRONG CHƯƠNG. THẮNG HEAT 1 MỞ HEAT 2: KHÓ HƠN NHƯNG RƯƠNG XỊN HƠN.",
                          "HEAT IS THE DIFFICULTY INSIDE A CHAPTER. WIN HEAT 1 TO OPEN HEAT 2: HARDER, BUT BETTER CHESTS.", HeatRow),
            new GuideLine("SẴN SÀNG THÌ BẤM VÁN MỚI.", "READY? PRESS NEW RUN.", RunTitleView.NewRunArea));

        public static readonly GuideScene Heat = new GuideScene("heat",
            new GuideLine("CAO THỦ VỪA MỞ HEAT 2! ĐỊCH THÊM 1 TRAIT VÀ 10% MÁU, NHƯNG RƯƠNG LÊN 1 BẬC VÀ THÊM 50% VÀNG.",
                          "HEAT 2 IS OPEN! FOES GET +1 TRAIT AND +10% HP, BUT THE CHEST GOES UP A TIER WITH +50% GOLD.", HeatRow),
            new GuideLine("HEAT 3 KHÔNG CHO ĐẤU LẠI VÀ TRÙM MẠNH HƠN, ĐỔI LẠI RƯƠNG LÊN 2 BẬC VÀ GẤP ĐÔI VÀNG.",
                          "HEAT 3 HAS NO RETRY AND TOUGHER BOSSES, BUT THE CHEST GOES UP TWO TIERS WITH DOUBLE GOLD.", HeatRow));

        public static readonly GuideScene Pick = new GuideScene("pick",
            new GuideLine("CHỌN BÓNG CỦA CAO THỦ. CHẠM MỘT LẦN ĐỂ ĐỌC, CHẠM LẦN NỮA ĐỂ CHỌN.",
                          "PICK YOUR BALL. TAP ONCE TO READ IT, AGAIN TO PICK IT."),
            new GuideLine("MỖI BÓNG CÓ VŨ KHÍ VÀ KỸ NĂNG RIÊNG. BÓNG CẤP CAO HƠN THÌ MẠNH HƠN.",
                          "EVERY BALL HAS ITS OWN WEAPON AND SKILL. HIGHER LEVEL BALLS ARE STRONGER."));

        public static readonly GuideScene StartPick = new GuideScene("startpick",
            new GuideLine("CHỌN 1 THẺ KHỞI ĐẦU. THẺ GIỮ NGUYÊN SUỐT LƯỢT VƯỢT ẢI.",
                          "PICK 1 STARTING CARD. CARDS STAY FOR THE WHOLE RUN.", Union(StartPickView.CardArea(0), StartPickView.CardArea(2))),
            new GuideLine("THẺ CÓ THỂ TĂNG MÁU, SÁT THƯƠNG, TỐC ĐỘ, THÊM TRAIT HOẶC NÂNG KỸ NĂNG CỦA BÓNG.",
                          "CARDS ADD HP, DAMAGE OR SPEED, GIVE A TRAIT, OR LEVEL UP THE SKILL OF YOUR BALL."));

        public static readonly GuideScene Prepare = new GuideScene("prepare",
            new GuideLine("PHÍA TRÊN LÀ ĐỐI THỦ KẾ TIẾP VÀ SÀN ĐẤU. XEM VŨ KHÍ VÀ TRAIT CỦA NÓ NHÉ.",
                          "UP TOP: YOUR NEXT FOE AND THE ARENA. READ ITS WEAPON AND TRAITS.", FoeBox),
            new GuideLine("TRƯỚC MỖI TRẬN CAO THỦ ĐƯỢC LẤY 1 THẺ MIỄN PHÍ.",
                          "BEFORE EVERY FIGHT YOU GET 1 FREE CARD.", Union(PrepareView.CardArea(0), PrepareView.CardArea(2))),
            new GuideLine("THẮNG TRẬN ĐƯỢC XU. XU DÙNG ĐỂ MUA THÊM THẺ HOẶC ĐỔI BỘ THẺ KHÁC.",
                          "WINS PAY COINS. SPEND THEM ON ANOTHER CARD OR A REROLL."),
            new GuideLine("XONG THÌ BẤM VÀO ĐẤU.", "DONE? PRESS FIGHT.", PrepareView.FightArea));

        public static readonly GuideScene Fight = new GuideScene("fight",
            new GuideLine("BÓNG TỰ ĐÁNH. VIỆC CỦA CAO THỦ: KHI KỸ NĂNG SẴN SÀNG, CHẠM MÀN HÌNH ĐỂ TUNG CHIÊU.",
                          "YOUR BALL FIGHTS BY ITSELF. YOUR JOB: WHEN THE SKILL IS READY, TAP THE SCREEN TO FIRE IT."),
            new GuideLine("PHẦN DƯỚI LÀ CỦA CAO THỦ, PHÍA TRÊN LÀ CỦA ĐỊCH. HẾT MÁU LÀ BỂ BÓNG.",
                          "THE BOTTOM IS YOURS, THE TOP IS THE FOE. RUN OUT OF HP AND YOU LOSE.", HudBottom),
            new GuideLine("NÚT X1/X2 ĐỂ TUA NHANH, BÁNH RĂNG ĐỂ TẠM DỪNG HOẶC LƯU VÀ THOÁT.",
                          "X1/X2 SPEEDS THE FIGHT UP, THE GEAR PAUSES OR SAVES AND QUITS.", FightControls));

        public static readonly GuideScene Result = new GuideScene("result",
            new GuideLine("MÁU CÒN LẠI ĐƯỢC GIỮ SANG TRẬN SAU, THẮNG CÒN ĐƯỢC HỒI THÊM MỘT ÍT.",
                          "THE HP YOU KEEP CARRIES TO THE NEXT FIGHT, AND A WIN HEALS A LITTLE."),
            new GuideLine("BỂ BÓNG MỘT TRẬN THÌ ĐƯỢC ĐẤU LẠI 1 LẦN VỚI 60% MÁU. BỂ LẦN NỮA LÀ HẾT VÁN.",
                          "LOSE A FIGHT AND YOU GET ONE RETRY AT 60% HP. LOSE AGAIN AND THE RUN IS OVER."));

        public static readonly GuideScene Boss = new GuideScene("boss",
            new GuideLine("CẨN THẬN, TRẬN NÀY LÀ TRÙM! TRÙM TO HƠN, NHIỀU MÁU HƠN VÀ CÓ CHIÊU RIÊNG.",
                          "CAREFUL, THIS ONE IS A BOSS! BIGGER, MORE HP AND ITS OWN MOVES.", FoeBox),
            new GuideLine("ĐỌC KỸ CHIÊU CỦA TRÙM RỒI CHỌN THẺ HỢP LÝ. HẠ TRÙM ĐƯỢC RƯƠNG TỐT HƠN.",
                          "READ ITS MOVES AND PICK YOUR CARD WITH CARE. BEATING A BOSS EARNS A BETTER CHEST."));

        public static readonly GuideScene Summary = new GuideScene("summary",
            new GuideLine("HẾT LƯỢT! THẮNG HAY THUA CAO THỦ ĐỀU NHẬN RƯƠNG CÓ MẢNH BÓNG VÀ VÀNG.",
                          "RUN OVER! WIN OR LOSE, YOU GET A CHEST WITH BALL SHARDS AND GOLD."),
            new GuideLine("MẢNH VÀ VÀNG DÙNG ĐỂ NÂNG CẤP BÓNG Ở KHO BÓNG. BÓNG MẠNH HƠN THÌ ĐI XA HƠN.",
                          "SHARDS AND GOLD LEVEL UP YOUR BALLS IN BALLS. STRONGER BALLS GO FURTHER."),
            new GuideLine("BẤM VÁN MỚI ĐỂ CHƠI TIẾP, HOẶC VỀ MENU ĐỂ NÂNG CẤP.",
                          "PRESS NEW RUN TO GO AGAIN, OR MENU TO UPGRADE."));

        public static readonly GuideScene MenuMore = new GuideScene("menu_more",
            new GuideLine("SẢNH ĐÃ MỞ THÊM! KHO BÓNG LÀ NƠI NÂNG CẤP BÓNG VÀ CỘNG ĐIỂM KỸ NĂNG.",
                          "THE HUB HAS GROWN! BALLS IS WHERE YOU LEVEL UP BALLS AND SPEND SKILL POINTS.", MenuView.BallsArea),
            new GuideLine("ĐÂY LÀ NHIỆM VỤ HẰNG NGÀY. LÀM XONG ĐƯỢC RƯƠNG, MỖI NGÀY CÓ NHIỆM VỤ MỚI.",
                          "THESE ARE DAILY QUESTS. FINISH THEM FOR CHESTS. NEW ONES EVERY DAY.", GoalBox),
            new GuideLine("KHI CÓ RƯƠNG ĐANG CHỜ, NÚT MỞ RƯƠNG HIỆN Ở GÓC TRÊN BÊN TRÁI.",
                          "WHEN A CHEST IS WAITING, OPEN CHEST SHOWS UP AT THE TOP LEFT.", MenuView.ChestArea));

        public static readonly GuideScene MenuPvp = new GuideScene("menu_pvp",
            new GuideLine("CAO THỦ ĐÃ MỞ PVP! ĐẤU XẾP HẠNG VỚI ĐỘI PHÒNG THỦ CỦA NGƯỜI KHÁC, KHÔNG CẦN CÙNG ONLINE.",
                          "PVP IS OPEN! RANKED FIGHTS AGAINST THE DEFENSES OF OTHER PLAYERS, NOBODY NEEDS TO BE ONLINE.", MenuView.VersusArea),
            new GuideLine("SỔ TAY GHI LẠI KỸ NĂNG, TRAIT VÀ TRÙM CAO THỦ ĐÃ GẶP.",
                          "THE CODEX KEEPS EVERY SKILL, TRAIT AND BOSS YOU HAVE MET.", MenuView.CodexArea),
            new GuideLine("QUÊN GÌ THÌ MỞ CÁCH CHƠI, HOẶC VÀO CÀI ĐẶT ĐỂ NGHE MÌNH NÓI LẠI.",
                          "FORGOT SOMETHING? OPEN HOW TO PLAY, OR ASK ME AGAIN IN SETTINGS.", MenuView.HowToArea));

        public static readonly GuideScene Balls = new GuideScene("balls",
            new GuideLine("ĐÂY LÀ KHO BÓNG. MỖI BÓNG CÓ CẤP RIÊNG, TỐI ĐA CẤP 10.",
                          "THESE ARE YOUR BALLS. EACH HAS ITS OWN LEVEL, UP TO 10."),
            new GuideLine("ĐỦ MẢNH VÀ VÀNG THÌ BẤM NÂNG CẤP. BÓNG CÓ DẤU HIỆU NỔI LÊN LÀ NÂNG ĐƯỢC.",
                          "WITH ENOUGH SHARDS AND GOLD, PRESS UPGRADE. A BADGE MARKS BALLS READY TO LEVEL."),
            new GuideLine("MỖI CẤP CHO 1 ĐIỂM KỸ NĂNG. MỞ CÂY KỸ NĂNG CỦA BÓNG ĐỂ CỘNG ĐIỂM.",
                          "EVERY LEVEL GIVES 1 SKILL POINT. OPEN THE SKILL TREE OF THE BALL TO SPEND IT."));

        public static readonly GuideScene Tree = new GuideScene("tree",
            new GuideLine("CÂY KỸ NĂNG: SINH LỰC TĂNG MÁU, SÁT THƯƠNG TĂNG ĐÒN ĐÁNH, BẢN NĂNG MỞ HÀNG GIỮA.",
                          "SKILL TREE: VITALITY ADDS HP, POWER ADDS DAMAGE, INSTINCT OPENS THE MIDDLE ROW."),
            new GuideLine("HÀNG GIỮA CÓ KHAI PHÁO VÀ NÂNG KỸ NĂNG. CHỌN NHÁNH A HOẶC B, CUỐI CÙNG LÀ TÀI NĂNG.",
                          "THE MIDDLE ROW HAS THE OPENER AND SKILL LEVELS. PICK BRANCH A OR B, THE FINALE IS LAST."),
            new GuideLine("ĐẶT LẠI ĐIỂM MIỄN PHÍ, CỨ THỬ NHIỀU CÁCH XEM CÁCH NÀO HỢP NHẤT!",
                          "RESETTING IS FREE, SO TRY DIFFERENT BUILDS!"));

        public static readonly GuideScene Codex = new GuideScene("codex",
            new GuideLine("SỔ TAY GHI MỌI KỸ NĂNG, TRAIT VÀ TRÙM. CHẠM VÀO TỪNG MỤC ĐỂ ĐỌC KỸ.",
                          "THE CODEX LISTS EVERY SKILL, TRAIT AND BOSS. TAP AN ENTRY TO READ IT."));

        public static readonly GuideScene Settings = new GuideScene("settings",
            new GuideLine("CÀI ĐẶT: CHỈNH NHẠC, ÂM THANH, NGÔN NGỮ VÀ GỢI Ý.",
                          "SETTINGS: MUSIC, SOUND, LANGUAGE AND HINTS."),
            new GuideLine("NÚT TÀI KHOẢN ĐỂ XEM TÊN, ĐĂNG XUẤT HOẶC CHỌN BẢN LƯU NÀO ĐƯỢC GIỮ.",
                          "ACCOUNT SHOWS YOUR NAME, LOGS OUT, OR PICKS WHICH SAVE TO KEEP.", SettingsView.AccountArea),
            new GuideLine("BẤM XEM LẠI HƯỚNG DẪN LÀ MÌNH SẼ GIẢI THÍCH LẠI TỪ ĐẦU.",
                          "PRESS REPLAY GUIDE AND I WILL EXPLAIN EVERYTHING AGAIN.", SettingsView.GuideArea));

        public static readonly GuideScene PvpIntro = new GuideScene("pvp_intro",
            new GuideLine("CHÀO MỪNG CAO THỦ ĐẾN PVP XẾP HẠNG! CAO THỦ ĐÁNH VỚI ĐỘI PHÒNG THỦ MÀ NGƯỜI KHÁC ĐÃ ĐẶT SẴN.",
                          "WELCOME TO RANKED PVP! YOU FIGHT DEFENSES OTHER PLAYERS HAVE SET UP."),
            new GuideLine("MỖI TRẬN ĐẤU 3 VÁN, THẮNG 2 LÀ THẮNG. MÁY CHỦ TỰ TÍNH NÊN AI CŨNG CÔNG BẰNG.",
                          "EACH CHALLENGE IS BEST OF 3, WORKED OUT BY THE SERVER, SO IT IS FAIR FOR EVERYONE."),
            new GuideLine("THẮNG ĐƯỢC CỘNG MMR, THUA BỊ TRỪ. LÚC ĐẦU ÍT NGƯỜI THÌ CÓ BOT, TÊN BẮT ĐẦU BẰNG BOT.",
                          "WINS ADD MMR, LOSSES TAKE SOME. WHILE FEW PLAYERS ARE ON, BOTS NAMED BOT FILL IN."));

        public static readonly GuideScene PvpSetup = new GuideScene("pvp_setup",
            new GuideLine("ĐẶT ĐỘI PHÒNG THỦ: 3 BÓNG THEO THỨ TỰ ƯU TIÊN. CHẠM ĐỂ ĐỔI BÓNG.",
                          "SET YOUR DEFENSE: 3 BALLS IN ORDER OF PRIORITY. TAP TO CHANGE ONE.", PvpCards),
            new GuideLine("CẤM SẴN 1 BÓNG VÀ 1 SÀN. KHI CÓ NGƯỜI ĐÁNH CAO THỦ, LỆNH CẤM NÀY TỰ ĐƯỢC DÙNG.",
                          "PRESET 1 BALL BAN AND 1 ARENA BAN. THEY ARE USED WHEN SOMEONE ATTACKS YOU.", PvpBans),
            new GuideLine("XONG THÌ BẤM ĐĂNG ĐỘI. CAO THỦ KHÔNG CẦN ONLINE KHI BỊ ĐÁNH.",
                          "THEN PRESS PUBLISH. YOU DO NOT NEED TO BE ONLINE WHEN YOU ARE ATTACKED.", PvpAction));

        public static readonly GuideScene PvpOffers = new GuideScene("pvp_offers",
            new GuideLine("ĐÂY LÀ 3 ĐỐI THỦ GẦN TRÌNH CAO THỦ, KÈM MMR VÀ SỐ ĐIỂM THẮNG/THUA.",
                          "HERE ARE 3 OPPONENTS NEAR YOUR LEVEL, WITH THEIR MMR AND WHAT A WIN OR LOSS IS WORTH.", PvpCards),
            new GuideLine("CHẠM MỘT NGƯỜI ĐỂ THÁCH ĐẤU. DANH SÁCH ĐỔI SAU 10 PHÚT, MỖI NGƯỜI CHỈ ĐÁNH 1 LẦN MỖI NGÀY.",
                          "TAP ONE TO CHALLENGE. THE LIST CHANGES EVERY 10 MINUTES, EACH OPPONENT ONCE A DAY."));

        public static readonly GuideScene PvpBan = new GuideScene("pvp_ban",
            new GuideLine("CẤM MÙ: CHỌN 1 BÓNG VÀ 1 SÀN CAO THỦ KHÔNG MUỐN GẶP. ĐỐI THỦ CŨNG ĐÃ CẤM SẴN.",
                          "BLIND BAN: PICK 1 BALL AND 1 ARENA YOU DO NOT WANT TO FACE. THEY HAVE BANNED TOO.", PvpBans),
            new GuideLine("BẤM CHỐT CẤM, LỆNH CẤM CỦA HAI BÊN SẼ CÙNG LỘ RA.",
                          "PRESS LOCK BANS AND BOTH SIDES ARE REVEALED.", PvpAction));

        public static readonly GuideScene PvpPick = new GuideScene("pvp_pick",
            new GuideLine("GIỜ CHỌN ĐỘI 3 BÓNG THEO THỨ TỰ ĐÁNH: HIỆP 1 LÀ BÓNG ĐẦU, HIỆP 2 BÓNG THỨ HAI... CHẠM ĐỂ ĐỔI.",
                          "NOW PICK A TEAM OF 3 IN FIGHTING ORDER: ROUND 1 IS THE FIRST BALL, ROUND 2 THE SECOND... TAP TO CHANGE.", PvpCards),
            new GuideLine("BẤM VÀO ĐẤU ĐỂ ĐÁNH. NẾU CAO THỦ BỎ ĐI LÚC NÀY, MÁY CHỦ SẼ CHỌN GIÚP.",
                          "PRESS FIGHT TO PLAY. IF YOU LEAVE NOW, THE SERVER PICKS FOR YOU.", PvpAction));

        public static readonly GuideScene PvpFight = new GuideScene("pvp_fight",
            new GuideLine("TRẬN PVP LÀ ĐÁNH THẬT: CHẠM MÀN HÌNH ĐỂ TUNG KỸ NĂNG KHI VÒNG SÁNG QUANH BÓNG ĐẦY.",
                          "PVP IS PLAYED LIVE: TAP THE SCREEN TO USE YOUR SKILL WHEN THE RING AROUND YOUR BALL IS FULL.", HudBottom),
            new GuideLine("MỖI HIỆP LÀ MỘT CẶP BÓNG KHÁC. ĐỐI THỦ KHÔNG ONLINE NÊN BOT ĐIỀU KHIỂN BÓNG CỦA HỌ. THẮNG 2 HIỆP LÀ THẮNG.",
                          "EACH ROUND IS A NEW PAIR OF BALLS. YOUR OPPONENT IS OFFLINE, SO A BOT PLAYS THEIRS. WIN 2 ROUNDS TO WIN."));

        public static readonly GuideScene PvpResult = new GuideScene("pvp_result",
            new GuideLine("XONG! MMR CỦA CAO THỦ ĐÃ ĐƯỢC CẬP NHẬT.", "DONE! YOUR MMR HAS BEEN UPDATED."),
            new GuideLine("LỊCH SỬ GHI LẠI CÁC TRẬN. BẤM PHÒNG THỦ ĐỂ ĐỔI ĐỘI BẤT CỨ LÚC NÀO.",
                          "HISTORY KEEPS YOUR FIGHTS. PRESS DEFENSE TO CHANGE YOUR TEAM ANY TIME.", PvpBottom));

        public static readonly GuideScene[] All =
        {
            Welcome, MenuStart, RunTitle, Heat, Pick, StartPick, Prepare, Fight, Result, Boss, Summary, MenuMore, MenuPvp,
            Balls, Tree, Codex, Settings, PvpIntro, PvpSetup, PvpOffers, PvpBan, PvpPick, PvpFight, PvpResult,
        };
    }
}
