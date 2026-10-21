using System.Collections.Generic;
using BallBattle.Sim;
using BallBattle.Sim.Meta;
using BallBattle.Sim.Skills;

namespace BallBattle.View
{
    /// <summary>Names and effects of every ball's skill-tree nodes (English source; values match the Sim rules).</summary>
    public static class TreeText
    {
        /// <summary>Per ball: INSTINCT, OPENER, FINALE A, FINALE B names, then their effect lines (two lines each).</summary>
        sealed class Row
        {
            public string Instinct, Opener, FinA, FinB;
            public string[] InstinctFx, OpenerFx, FinAFx, FinBFx;
        }

        static readonly Dictionary<string, Row> Rows = new Dictionary<string, Row>
        {
            { "blade", new Row { Instinct = "KINDLING", Opener = "EMBERS", FinA = "SMOLDER", FinB = "BACKDRAFT",
                InstinctFx = new[] { "DAMAGE GAINED PER HIT", "+2% PER RANK" }, OpenerFx = new[] { "+0.2 DAMAGE", "AT THE START" },
                FinAFx = new[] { "BURN LASTS 2 S AFTER", "LEAVING THE TRAIL" }, FinBFx = new[] { "HITS ON A BURNING FOE +25%", "TRAIL 15% SHORTER" } } },
            { "fang", new Row { Instinct = "MOMENTUM", Opener = "SPIN UP", FinA = "AFTERSPIN", FinB = "BLOODLUST",
                InstinctFx = new[] { "SPIN GAIN FADES SLOWER", "0.92 TO 0.93 / 0.94" }, OpenerFx = new[] { "FIRST HIT", "+2 SPIN" },
                FinAFx = new[] { "AFTER A BLINK THE NEXT 3", "HITS +50% SPIN. EVERY 12 S" }, FinBFx = new[] { "PHANTOM SLASH +20% DAMAGE", "COOLDOWN +10%" } } },
            { "pike", new Row { Instinct = "MORE LINE", Opener = "FIRST CAST", FinA = "LONG CAST", FinB = "YANK",
                InstinctFx = new[] { "LENGTH AND DAMAGE PER HIT", "+2% PER RANK" }, OpenerFx = new[] { "PIKE STARTS", "1 LONGER" },
                FinAFx = new[] { "HOOK FLIES 12% FARTHER", "STUN 10% SHORTER" }, FinBFx = new[] { "PULL +20%, HOOK DAMAGE +25%", "RANGE -10%" } } },
            { "brawler", new Row { Instinct = "HEADBUTT", Opener = "RUN-UP", FinA = "STEADY", FinB = "OVERCHARGE",
                InstinctFx = new[] { "DAMAGE FACTOR PER HIT", "+2% PER RANK" }, OpenerFx = new[] { "DAMAGE FACTOR", "+0.002 AT THE START" },
                FinAFx = new[] { "BACKFIRE STUN HALVED", "HOLD TOO LONG HURTS LESS" }, FinBFx = new[] { "HELD PAST FULL: RAM +30%", "BACKFIRE STUN +50%" } } },
            { "volley", new Row { Instinct = "FUSE", Opener = "OPENER", FinA = "SPLIT", FinB = "CHAIN",
                InstinctFx = new[] { "ARROW DAMAGE PER HIT", "+0.01 PER RANK" }, OpenerFx = new[] { "FIRST VOLLEY: 1 MORE", "ARROW AT 50%" },
                FinAFx = new[] { "LAST BOUNCE SPLITS INTO", "2 SPARKS AT 40% DAMAGE" }, FinBFx = new[] { "EACH HIT ADDS A BOUNCE", "UP TO 4 HITS, 20% SLOWER" } } },
            { "venom", new Row { Instinct = "SEEP", Opener = "SEED ROT", FinA = "OUTBREAK", FinB = "LINGER",
                InstinctFx = new[] { "POISON AND CLOUD DAMAGE", "+3% PER RANK" }, OpenerFx = new[] { "FIRST 2 POISON HITS", "+1 STACK EACH" },
                FinAFx = new[] { "WHEN THE CLOUD ENDS A", "SMALL CLOUD STAYS BEHIND" }, FinBFx = new[] { "POISON LASTS 50% LONGER", "AFTER THE FOE LEAVES" } } },
            { "aegis", new Row { Instinct = "GUARD UP", Opener = "RAISE", FinA = "RIPOSTE", FinB = "FLOW",
                InstinctFx = new[] { "SHIELD WIDTH PER HIT", "+5% PER RANK" }, OpenerFx = new[] { "SHIELD STARTS", "0.25 WIDER" },
                FinAFx = new[] { "A PARRY THROWS THE BLOW", "BACK +50% HARDER" }, FinBFx = new[] { "A PARRY REFUNDS 60% OF", "COOLDOWN. A MISS COSTS +25%" } } },
            { "chorus", new Row { Instinct = "DEVOTION", Opener = "MORN LIGHT", FinA = "EVERLIGHT", FinB = "2X RAINBOW",
                InstinctFx = new[] { "COLOUR EFFECTS", "+6% PER RANK" }, OpenerFx = new[] { "THE FIGHT STARTS", "WITH PINK LIT" },
                FinAFx = new[] { "AFTER UNITY", "PINK STAYS LIT" }, FinBFx = new[] { "UNITY FIRES TWICE", "AT 75% EACH" } } },
            { "serpent", new Row { Instinct = "SCENT", Opener = "FIRST MEAL", FinA = "HYDRA", FinB = "SHED SKIN",
                InstinctFx = new[] { "HUNTS PREY HARDER", "+0.2 TURN PER RANK" }, OpenerFx = new[] { "START WITH", "2 TAIL SEGMENTS" },
                FinAFx = new[] { "EACH BITE HEALS", "0.2 PER SEGMENT" }, FinBFx = new[] { "AFTER THE COIL, THE NEXT", "HIT WITHIN 1 S IS BLOCKED" } } },
            { "rig", new Row { Instinct = "WIND UP", Opener = "RESERVE", FinA = "QUEEN", FinB = "STING STORM",
                InstinctFx = new[] { "TURRETS FIRE 1 TICK", "FASTER PER RANK" }, OpenerFx = new[] { "1 TURRET AT THE START", "GONE AFTER ITS VOLLEY" },
                FinAFx = new[] { "ONE DRONE GUARDS: BLOCKS", "1 HIT WHILE SWARM IS OUT" }, FinBFx = new[] { "+2 DRONES IN THE SWARM", "EACH 25% WEAKER" } } },
        };

        /// <summary>BB-19c: what each skill node (level 2 / level 3 of branch A or B) gives, two lines; the level 3 line says what it becomes.</summary>
        static readonly Dictionary<string, string[]> SkillFx = new Dictionary<string, string[]>
        {
            { "blade:A2", new[] { "LV2: TRAIL BURNS 50% LONGER", "DASH GOES 25% FURTHER" } },
            { "blade:A3", new[] { "LV3: TRAIL BURNS 75% LONGER", "DASH GOES 25% FURTHER" } },
            { "blade:B2", new[] { "LV2: TRAIL HITS +70%", "SHORTER, NARROWER TRAIL" } },
            { "blade:B3", new[] { "LV3: TRAIL HITS +100%", "SHORTER, NARROWER TRAIL" } },
            { "fang:A2", new[] { "LV2: DECOY HITS FOR 60%", "DODGE WINDOW +50%" } },
            { "fang:A3", new[] { "LV3: DECOY HITS FOR 90%", "DODGE WINDOW +75%" } },
            { "fang:B2", new[] { "LV2: SLASH DOUBLES UNDER 30% HP", "" } },
            { "fang:B3", new[] { "LV3: SLASH DOUBLES UNDER 45% HP", "" } },
            { "pike:A2", new[] { "LV2: HOOK +30% SPEED AND RANGE", "HOOK 20% BIGGER" } },
            { "pike:A3", new[] { "LV3: HOOK +45% SPEED AND RANGE", "HOOK 30% BIGGER" } },
            { "pike:B2", new[] { "LV2: HOOK HITS +50%, STUN +60%", "PULL +2" } },
            { "pike:B3", new[] { "LV3: HOOK HITS +75%, STUN +90%", "PULL +3" } },
            { "brawler:A2", new[] { "LV2: HOLD 25% LONGER BEFORE", "THE BACKFIRE" } },
            { "brawler:A3", new[] { "LV3: HOLD 50% LONGER BEFORE", "THE BACKFIRE" } },
            { "brawler:B2", new[] { "LV2: FULL CHARGE IN HALF THE TIME", "RAM CAPPED AT 75%" } },
            { "brawler:B3", new[] { "LV3: FULL CHARGE IN HALF THE TIME", "RAM CAPPED AT 88%" } },
            { "volley:A2", new[] { "LV2: BOLT BOUNCES 2 MORE TIMES", "" } },
            { "volley:A3", new[] { "LV3: BOLT BOUNCES 3 MORE TIMES", "" } },
            { "volley:B2", new[] { "LV2: BOLT HITS +90%", "BIGGER BOLT, 2 BOUNCES" } },
            { "volley:B3", new[] { "LV3: BOLT HITS +125%", "BIGGER BOLT, 2 BOUNCES" } },
            { "venom:A2", new[] { "LV2: CLOUD 35% WIDER", "LASTS 30% LONGER" } },
            { "venom:A3", new[] { "LV3: CLOUD 55% WIDER", "LASTS 50% LONGER" } },
            { "venom:B2", new[] { "LV2: CLOUD POISON +80%", "SMALLER CLOUD, LESS HEAL" } },
            { "venom:B3", new[] { "LV3: CLOUD POISON +120%", "SMALLER CLOUD, LESS HEAL" } },
            { "aegis:A2", new[] { "LV2: PARRY WINDOW 67% LONGER", "" } },
            { "aegis:A3", new[] { "LV3: PARRY WINDOW 100% LONGER", "" } },
            { "aegis:B2", new[] { "LV2: WINDOW 42% SHORTER", "REFLECT +50%, STUN +33%" } },
            { "aegis:B3", new[] { "LV3: WINDOW 42% SHORTER", "REFLECT +75%, STUN +50%" } },
            { "rig:A2", new[] { "LV2: 9 DRONES, EACH STING -30%", "" } },
            { "rig:A3", new[] { "LV3: 10 DRONES, EACH STING -30%", "" } },
            { "rig:B2", new[] { "LV2: 4 DRONES, STING +90%", "DRONES LIVE 30% LONGER" } },
            { "rig:B3", new[] { "LV3: 4 DRONES, STING +125%", "DRONES LIVE 30% LONGER" } },
            { "chorus:A2", new[] { "LV2: UNITY HEALS 1% MAX HP", "" } },
            { "chorus:A3", new[] { "LV3: UNITY HEALS 1.5% MAX HP", "" } },
            { "chorus:B2", new[] { "LV2: UNITY DAMAGE +60%", "WIDER BLAST" } },
            { "chorus:B3", new[] { "LV3: UNITY DAMAGE +90%", "WIDER BLAST" } },
            { "serpent:A2", new[] { "LV2: COIL HOLDS 50% LONGER", "THEN THE FOE IS SLOWED" } },
            { "serpent:A3", new[] { "LV3: COIL HOLDS 80% LONGER", "THEN THE FOE IS SLOWED" } },
            { "serpent:B2", new[] { "LV2: 4 BITES", "EACH BITE HEALS 30% OF IT" } },
            { "serpent:B3", new[] { "LV3: 5 BITES", "EACH BITE HEALS 45% OF IT" } },
        };

        // Context stays visible at both skill levels: the numbers alone do not tell a player
        // when to press the skill or what the new branch actually changes in a fight.
        static readonly Dictionary<string, string[]> SkillContext = new Dictionary<string, string[]>
        {
            { "blade", new[] { "TAP: DASH AND LEAVE FIRE", "FOE BURNS WHILE IN THE TRAIL" } },
            { "fang", new[] { "TAP: BLINK BEHIND THE FOE", "SLASH AND DODGE ON ARRIVAL" } },
            { "pike", new[] { "TAP: HOOK, PULL AND STUN", "A MISSED HOOK CATCHES NOTHING" } },
            { "brawler", new[] { "TAP TO CHARGE, TAP TO RAM", "HOLDING TOO LONG BACKFIRES" } },
            { "volley", new[] { "TAP: FIRE A RICOCHET BOLT", "IT BOUNCES OFF THE WALLS" } },
            { "venom", new[] { "TAP: SPREAD A POISON CLOUD", "FOE SLOWS; YOU HEAL IN IT" } },
            { "aegis", new[] { "TAP JUST BEFORE A HIT", "PARRY RETURNS THE BLOW" } },
            { "rig", new[] { "TAP: SEND DRONES TO HUNT", "EACH DRONE STINGS ON CONTACT" } },
            { "chorus", new[] { "TAP WHEN THE ORBS ARE LIT", "UNITY BURSTS THE LIT COLOURS" } },
            { "serpent", new[] { "TAP: LUNGE AND COIL FOE", "BITES LAND DURING THE HOLD" } },
        };

        static readonly Dictionary<string, string[]> NodeContext = new Dictionary<string, string[]>
        {
            { "blade", new[] { "STACKS THROUGH THE FIGHT", "STARTS BEFORE THE FIRST HIT", "CATCH THE FOE AFTER THEY EXIT", "TRADE AREA FOR A HARDER HIT" } },
            { "fang", new[] { "SPIN LASTS BETWEEN HITS", "START THE SPIN ON HIT ONE", "TRIGGERS AFTER THE BLINK", "TRADE RECAST SPEED FOR POWER" } },
            { "pike", new[] { "THE PIKE GROWS AS HITS LAND", "EXTRA REACH FROM THE START", "MORE REACH, BRIEFER CONTROL", "CLOSER HOOK, STRONGER DRAG" } },
            { "brawler", new[] { "EACH HIT BUILDS THE NEXT RAM", "FIRST RAM STARTS STRONGER", "SAFER WHEN YOU OVERHOLD", "RISK A LONGER SELF-STUN" } },
            { "volley", new[] { "ARROWS BUILD UP DAMAGE", "EXTRA ARROW ON FIRST VOLLEY", "SPLITS AT THE LAST BOUNCE", "MORE HITS KEEP THE BOLT ALIVE" } },
            { "venom", new[] { "POISON AND CLOUDS GROW STRONGER", "EXTRA POISON EARLY IN FIGHT", "LEAVES AN EXTRA HAZARD", "POISON OUTLASTS THE CLOUD" } },
            { "aegis", new[] { "WIDER SHIELD CATCHES MORE", "CATCH SHOTS FROM THE START", "REQUIRES A TIMED PARRY", "REWARDS ACCURATE PARRIES" } },
            { "chorus", new[] { "ALL COLOURS GROW STRONGER", "ONE COLOUR CLOSER TO UNITY", "PINK STAYS READY FOR UNITY", "TWO WEAKER PULSES INSTEAD" } },
            { "serpent", new[] { "TURNS FASTER TOWARD PREY", "LONGER TAIL FROM THE START", "MORE SEGMENTS MEAN MORE HEAL", "BLOCKS A HIT RIGHT AFTER COIL" } },
            { "rig", new[] { "TURRETS FIRE SOONER", "FREE OPENING VOLLEY", "ONE BLOCK WHILE SWARM IS OUT", "MORE STINGS, LESS PER DRONE" } },
        };

        // Vietnamese node copy in the LMHT (League of Legends VN) tooltip voice: a tag first (NỘI TẠI: passive,
        // ĐẦU TRẬN: at spawn, KÍCH HOẠT: the tapped skill), then the effect, then ĐỔI LẠI: for the trade-off.
        // Every coefficient matches the Sim rules. Lines fit the 230 px card (max 4 lines).
        // Index: instinct, opener, A2, A3, B2, B3, finale A, finale B.
        static readonly Dictionary<string, string[][]> ViFx = new Dictionary<string, string[][]>
        {
            { "blade", new[] {
                new[] { "NỘI TẠI: MỖI NHÁT CHÉM TRÚNG ĐÍCH,", "ĐAO TÍCH THÊM SÁT THƯƠNG. LƯỢNG TÍCH", "TĂNG 2% MỖI BẬC." },
                new[] { "ĐẦU TRẬN: ĐAO CÓ SẴN THÊM", "0,2 SÁT THƯƠNG." },
                new[] { "KÍCH HOẠT: LƯỚT TỚI, ĐỂ LẠI VỆT LỬA", "THIÊU ĐỐT KẺ ĐỊCH ĐỨNG TRÊN ĐÓ.", "VỆT LỬA TỒN TẠI LÂU HƠN 50%.", "TẦM LƯỚT XA HƠN 25%." },
                new[] { "KÍCH HOẠT: LƯỚT TỚI, ĐỂ LẠI VỆT LỬA", "THIÊU ĐỐT KẺ ĐỊCH ĐỨNG TRÊN ĐÓ.", "VỆT LỬA TỒN TẠI LÂU HƠN 75%.", "TẦM LƯỚT XA HƠN 25%." },
                new[] { "KÍCH HOẠT: LƯỚT TỚI, ĐỂ LẠI VỆT LỬA", "THIÊU ĐỐT KẺ ĐỊCH ĐỨNG TRÊN ĐÓ.", "VỆT LỬA GÂY THÊM 70% SÁT THƯƠNG.", "ĐỔI LẠI: VỆT NGẮN VÀ HẸP HƠN 20%." },
                new[] { "KÍCH HOẠT: LƯỚT TỚI, ĐỂ LẠI VỆT LỬA", "THIÊU ĐỐT KẺ ĐỊCH ĐỨNG TRÊN ĐÓ.", "VỆT LỬA GÂY THÊM 100% SÁT THƯƠNG.", "ĐỔI LẠI: VỆT NGẮN VÀ HẸP HƠN 20%." },
                new[] { "NỘI TẠI: KẺ ĐỊCH ĐÃ RỜI VỆT LỬA VẪN", "TIẾP TỤC BỊ THIÊU ĐỐT, TỔNG CỘNG", "TỐI ĐA 2,5 GIÂY." },
                new[] { "NỘI TẠI: NHÁT CHÉM LÊN KẺ ĐỊCH ĐANG", "BỊ THIÊU ĐỐT GÂY THÊM 25% SÁT THƯƠNG.", "ĐỔI LẠI: VỆT LỬA NGẮN HƠN 15%." }
            } },
            { "fang", new[] {
                new[] { "NỘI TẠI: ĐÀ XOÁY HAO CHẬM HƠN. SAU", "MỖI NHỊP CÒN GIỮ 93% Ở BẬC 1, 94%", "Ở BẬC 2, THAY VÌ 92%." },
                new[] { "ĐẦU TRẬN: ĐÒN ĐÁNH TRÚNG ĐẦU TIÊN", "CỘNG THÊM 2 ĐÀ XOÁY." },
                new[] { "KÍCH HOẠT: CHỚP RA SAU LƯNG KẺ ĐỊCH,", "CHÉM MỘT NHÁT RỒI NÉ ĐÒN.", "ĐỂ LẠI TÀN ẢNH GÂY 60% SÁT THƯƠNG.", "THỜI GIAN NÉ ĐÒN DÀI HƠN 50%." },
                new[] { "KÍCH HOẠT: CHỚP RA SAU LƯNG KẺ ĐỊCH,", "CHÉM MỘT NHÁT RỒI NÉ ĐÒN.", "ĐỂ LẠI TÀN ẢNH GÂY 90% SÁT THƯƠNG.", "THỜI GIAN NÉ ĐÒN DÀI HƠN 75%." },
                new[] { "KÍCH HOẠT: CHỚP RA SAU LƯNG KẺ ĐỊCH,", "CHÉM MỘT NHÁT RỒI NÉ ĐÒN.", "NHÁT CHÉM GÂY GẤP ĐÔI SÁT THƯƠNG", "NẾU KẺ ĐỊCH CÒN DƯỚI 30% MÁU." },
                new[] { "KÍCH HOẠT: CHỚP RA SAU LƯNG KẺ ĐỊCH,", "CHÉM MỘT NHÁT RỒI NÉ ĐÒN.", "NHÁT CHÉM GÂY GẤP ĐÔI SÁT THƯƠNG", "NẾU KẺ ĐỊCH CÒN DƯỚI 45% MÁU." },
                new[] { "NỘI TẠI: SAU KHI CHỚP, 3 ĐÒN TRÚNG", "KẾ TIẾP TÍCH ĐÀ XOÁY NHANH HƠN 50%.", "HỒI CHIÊU NỘI TẠI: 12 GIÂY." },
                new[] { "NỘI TẠI: NHÁT CHÉM ẢO ẢNH GÂY THÊM", "20% SÁT THƯƠNG.", "ĐỔI LẠI: HỒI CHIÊU DÀI HƠN 10%." }
            } },
            { "pike", new[] {
                new[] { "NỘI TẠI: MỖI ĐÒN ĐÂM TRÚNG ĐÍCH, GIÁO", "DÀI RA VÀ MẠNH LÊN. MỨC TĂNG", "THÊM 2% MỖI BẬC." },
                new[] { "ĐẦU TRẬN: GIÁO DÀI SẴN THÊM 1." },
                new[] { "KÍCH HOẠT: PHÓNG MÓC CÂU, KÉO KẺ ĐỊCH", "VỀ PHÍA MÌNH VÀ LÀM CHOÁNG.", "MÓC BAY NHANH VÀ XA HƠN 30%.", "ĐẦU MÓC TO HƠN 20%." },
                new[] { "KÍCH HOẠT: PHÓNG MÓC CÂU, KÉO KẺ ĐỊCH", "VỀ PHÍA MÌNH VÀ LÀM CHOÁNG.", "MÓC BAY NHANH VÀ XA HƠN 45%.", "ĐẦU MÓC TO HƠN 30%." },
                new[] { "KÍCH HOẠT: PHÓNG MÓC CÂU, KÉO KẺ ĐỊCH", "VỀ PHÍA MÌNH VÀ LÀM CHOÁNG.", "MÓC GÂY THÊM 50% SÁT THƯƠNG, LỰC KÉO", "+2, LÀM CHOÁNG LÂU HƠN 60%." },
                new[] { "KÍCH HOẠT: PHÓNG MÓC CÂU, KÉO KẺ ĐỊCH", "VỀ PHÍA MÌNH VÀ LÀM CHOÁNG.", "MÓC GÂY THÊM 75% SÁT THƯƠNG, LỰC KÉO", "+3, LÀM CHOÁNG LÂU HƠN 90%." },
                new[] { "NỘI TẠI: MÓC CÂU BAY XA HƠN 12%.", "ĐỔI LẠI: LÀM CHOÁNG NGẮN HƠN 10%." },
                new[] { "NỘI TẠI: MÓC CÂU KÉO MẠNH HƠN 20%", "VÀ GÂY THÊM 25% SÁT THƯƠNG.", "ĐỔI LẠI: TẦM MÓC NGẮN HƠN 10%." }
            } },
            { "brawler", new[] {
                new[] { "NỘI TẠI: MỖI CÚ HÚC TRÚNG ĐÍCH LÀM", "CÁC CÚ HÚC SAU MẠNH HƠN. MỨC TĂNG", "THÊM 2% MỖI BẬC." },
                new[] { "ĐẦU TRẬN: HỆ SỐ SỨC HÚC TĂNG SẴN", "THÊM 0,002." },
                new[] { "KÍCH HOẠT: CHẠM ĐỂ GỒNG, CHẠM LẦN NỮA", "ĐỂ HÚC. GỒNG QUÁ LÂU SẼ BỊ PHẢN LỰC.", "GỒNG ĐƯỢC LÂU HƠN 25% TRƯỚC KHI", "BỊ PHẢN LỰC." },
                new[] { "KÍCH HOẠT: CHẠM ĐỂ GỒNG, CHẠM LẦN NỮA", "ĐỂ HÚC. GỒNG QUÁ LÂU SẼ BỊ PHẢN LỰC.", "GỒNG ĐƯỢC LÂU HƠN 50% TRƯỚC KHI", "BỊ PHẢN LỰC." },
                new[] { "KÍCH HOẠT: CHẠM ĐỂ GỒNG, CHẠM LẦN NỮA", "ĐỂ HÚC. GỒNG QUÁ LÂU SẼ BỊ PHẢN LỰC.", "GỒNG ĐẦY CHỈ MẤT NỬA THỜI GIAN.", "ĐỔI LẠI: SỨC HÚC TỐI ĐA CÒN 75%." },
                new[] { "KÍCH HOẠT: CHẠM ĐỂ GỒNG, CHẠM LẦN NỮA", "ĐỂ HÚC. GỒNG QUÁ LÂU SẼ BỊ PHẢN LỰC.", "GỒNG ĐẦY CHỈ MẤT NỬA THỜI GIAN.", "ĐỔI LẠI: SỨC HÚC TỐI ĐA CÒN 88%." },
                new[] { "NỘI TẠI: KHI BỊ PHẢN LỰC, THỜI GIAN", "TỰ LÀM CHOÁNG GIẢM 50%." },
                new[] { "NỘI TẠI: HÚC KHI ĐÃ GỒNG ĐẦY GÂY", "THÊM 30% SÁT THƯƠNG.", "ĐỔI LẠI: PHẢN LỰC CHOÁNG LÂU HƠN 50%." }
            } },
            { "volley", new[] {
                new[] { "NỘI TẠI: MỖI ĐÒN TRÚNG ĐÍCH, MỖI VIÊN", "ĐẠN MẠNH THÊM 0,01 SÁT THƯƠNG", "MỖI BẬC." },
                new[] { "ĐẦU TRẬN: LOẠT BẮN ĐẦU TIÊN CÓ THÊM", "1 VIÊN, GÂY 50% SÁT THƯƠNG." },
                new[] { "KÍCH HOẠT: BẮN MỘT VIÊN ĐẠN DỘI,", "NẢY BẬT KHỎI TƯỜNG.", "ĐẠN NẢY THÊM 2 LẦN." },
                new[] { "KÍCH HOẠT: BẮN MỘT VIÊN ĐẠN DỘI,", "NẢY BẬT KHỎI TƯỜNG.", "ĐẠN NẢY THÊM 3 LẦN." },
                new[] { "KÍCH HOẠT: BẮN MỘT VIÊN ĐẠN DỘI,", "NẢY BẬT KHỎI TƯỜNG.", "ĐẠN TO HƠN 50%, GÂY THÊM 90% SÁT", "THƯƠNG. ĐỔI LẠI: CHỈ NẢY 2 LẦN." },
                new[] { "KÍCH HOẠT: BẮN MỘT VIÊN ĐẠN DỘI,", "NẢY BẬT KHỎI TƯỜNG.", "ĐẠN TO HƠN 75%, GÂY THÊM 125% SÁT", "THƯƠNG. ĐỔI LẠI: CHỈ NẢY 2 LẦN." },
                new[] { "NỘI TẠI: Ở LẦN NẢY CUỐI, ĐẠN TÁCH", "THÀNH 2 TIA, MỖI TIA GÂY 40%", "SÁT THƯƠNG." },
                new[] { "NỘI TẠI: MỖI LẦN TRÚNG ĐÍCH, ĐẠN", "NẢY THÊM 1 LẦN, TỐI ĐA 4 LẦN TRÚNG.", "ĐỔI LẠI: ĐẠN BAY CHẬM HƠN 20%." }
            } },
            { "venom", new[] {
                new[] { "NỘI TẠI: ĐỘC VÀ MÂY BÀO TỬ GÂY THÊM", "3% SÁT THƯƠNG MỖI BẬC." },
                new[] { "ĐẦU TRẬN: 2 ĐÒN GÂY ĐỘC ĐẦU TIÊN", "MỖI ĐÒN THÊM 1 ĐIỂM CỘNG DỒN ĐỘC." },
                new[] { "KÍCH HOẠT: THẢ MÂY BÀO TỬ LÀM CHẬM", "KẺ ĐỊCH, HỒI MÁU CHO BẢN THÂN.", "MÂY RỘNG HƠN 35%, TỒN TẠI LÂU", "HƠN 30%." },
                new[] { "KÍCH HOẠT: THẢ MÂY BÀO TỬ LÀM CHẬM", "KẺ ĐỊCH, HỒI MÁU CHO BẢN THÂN.", "MÂY RỘNG HƠN 55%, TỒN TẠI LÂU", "HƠN 50%." },
                new[] { "KÍCH HOẠT: THẢ MÂY BÀO TỬ LÀM CHẬM", "KẺ ĐỊCH, HỒI MÁU CHO BẢN THÂN.", "ĐỘC TRONG MÂY MẠNH HƠN 80%.", "ĐỔI LẠI: MÂY HẸP 20%, HỒI MÁU -60%." },
                new[] { "KÍCH HOẠT: THẢ MÂY BÀO TỬ LÀM CHẬM", "KẺ ĐỊCH, HỒI MÁU CHO BẢN THÂN.", "ĐỘC TRONG MÂY MẠNH HƠN 120%.", "ĐỔI LẠI: MÂY HẸP 20%, HỒI MÁU -60%." },
                new[] { "NỘI TẠI: KHI MÂY TAN, ĐỂ LẠI MỘT ĐÁM", "MÂY NHỎ BẰNG 45%, TỒN TẠI 2,5 GIÂY.", "ĐÁM MÂY NÀY KHÔNG HỒI MÁU." },
                new[] { "NỘI TẠI: ĐỘC TỪ MÂY KÉO DÀI THÊM 50%", "SAU KHI KẺ ĐỊCH RỜI KHỎI MÂY." }
            } },
            { "aegis", new[] {
                new[] { "NỘI TẠI: MỖI ĐÒN TRÚNG ĐÍCH, KHIÊN", "NỞ RỘNG THÊM. MỨC TĂNG THÊM 5%", "MỖI BẬC." },
                new[] { "ĐẦU TRẬN: KHIÊN RỘNG SẴN THÊM 0,25." },
                new[] { "KÍCH HOẠT: CHẠM NGAY TRƯỚC KHI DÍNH", "ĐÒN ĐỂ ĐỠ VÀ PHẢN LẠI ĐÒN ĐÁNH.", "THỜI GIAN ĐỠ DÀI HƠN 67%." },
                new[] { "KÍCH HOẠT: CHẠM NGAY TRƯỚC KHI DÍNH", "ĐÒN ĐỂ ĐỠ VÀ PHẢN LẠI ĐÒN ĐÁNH.", "THỜI GIAN ĐỠ DÀI HƠN 100%." },
                new[] { "KÍCH HOẠT: CHẠM NGAY TRƯỚC KHI DÍNH", "ĐÒN ĐỂ ĐỠ VÀ PHẢN LẠI ĐÒN ĐÁNH.", "PHẢN ĐÒN +50%, CHOÁNG LÂU HƠN 33%.", "ĐỔI LẠI: THỜI GIAN ĐỠ NGẮN HƠN 42%." },
                new[] { "KÍCH HOẠT: CHẠM NGAY TRƯỚC KHI DÍNH", "ĐÒN ĐỂ ĐỠ VÀ PHẢN LẠI ĐÒN ĐÁNH.", "PHẢN ĐÒN +75%, CHOÁNG LÂU HƠN 50%.", "ĐỔI LẠI: THỜI GIAN ĐỠ NGẮN HƠN 42%." },
                new[] { "NỘI TẠI: ĐỠ ĐÒN THÀNH CÔNG SẼ PHẢN", "LẠI ĐÒN ĐÁNH MẠNH HƠN 50%." },
                new[] { "NỘI TẠI: ĐỠ THÀNH CÔNG HOÀN LẠI 60%", "THỜI GIAN HỒI CHIÊU.", "ĐỔI LẠI: ĐỠ HỤT BỊ PHẠT HỒI CHIÊU +25%." }
            } },
            { "rig", new[] {
                new[] { "NỘI TẠI: TRỤ BẮN NHANH HƠN 1/60 GIÂY", "MỖI BẬC." },
                new[] { "ĐẦU TRẬN: CÓ SẴN 1 TRỤ TẠM, BIẾN MẤT", "SAU LOẠT BẮN ĐẦU TIÊN." },
                new[] { "KÍCH HOẠT: THẢ BẦY ONG ĐUỔI THEO,", "ĐỐT KẺ ĐỊCH KHI CHẠM VÀO.", "BẦY CÓ 9 CON, MỖI CÚ ĐỐT YẾU HƠN 30%." },
                new[] { "KÍCH HOẠT: THẢ BẦY ONG ĐUỔI THEO,", "ĐỐT KẺ ĐỊCH KHI CHẠM VÀO.", "BẦY CÓ 10 CON, MỖI CÚ ĐỐT YẾU HƠN 30%." },
                new[] { "KÍCH HOẠT: THẢ BẦY ONG ĐUỔI THEO,", "ĐỐT KẺ ĐỊCH KHI CHẠM VÀO.", "BẦY CHỈ CÓ 4 CON, ĐỐT MẠNH HƠN 90%.", "ONG TỒN TẠI LÂU HƠN 30%." },
                new[] { "KÍCH HOẠT: THẢ BẦY ONG ĐUỔI THEO,", "ĐỐT KẺ ĐỊCH KHI CHẠM VÀO.", "BẦY CHỈ CÓ 4 CON, ĐỐT MẠNH HƠN 125%.", "ONG TỒN TẠI LÂU HƠN 30%." },
                new[] { "NỘI TẠI: KHI BẦY ONG ĐANG BAY, ONG", "CHÚA CHẶN GIÚP 1 ĐÒN ĐÁNH." },
                new[] { "NỘI TẠI: BẦY ONG CÓ THÊM 2 CON.", "ĐỔI LẠI: MỖI CÚ ĐỐT YẾU HƠN 25%." }
            } },
            { "chorus", new[] {
                new[] { "NỘI TẠI: HIỆU ỨNG CỦA CÁC MÀU HỒNG,", "VÀNG, LỤC, ĐỎ MẠNH HƠN 6% MỖI BẬC." },
                new[] { "ĐẦU TRẬN: MÀU HỒNG ĐÃ ĐƯỢC THẮP SÁNG." },
                new[] { "KÍCH HOẠT: KHI CÁC QUẢ CẦU ĐÃ SÁNG,", "CHẠM ĐỂ HIỆP NHẤT, NỔ TUNG CÁC MÀU.", "VƯỢT ẢI: HỒI 1% MÁU TỐI ĐA.", "SO TÀI: HỒI 1,5% MÁU TỐI ĐA." },
                new[] { "KÍCH HOẠT: KHI CÁC QUẢ CẦU ĐÃ SÁNG,", "CHẠM ĐỂ HIỆP NHẤT, NỔ TUNG CÁC MÀU.", "VƯỢT ẢI: HỒI 1,5% MÁU TỐI ĐA.", "SO TÀI: HỒI 2,25% MÁU TỐI ĐA." },
                new[] { "KÍCH HOẠT: KHI CÁC QUẢ CẦU ĐÃ SÁNG,", "CHẠM ĐỂ HIỆP NHẤT, NỔ TUNG CÁC MÀU.", "HIỆP NHẤT GÂY THÊM 60% SÁT THƯƠNG,", "TẦM NỔ RỘNG THÊM 30." },
                new[] { "KÍCH HOẠT: KHI CÁC QUẢ CẦU ĐÃ SÁNG,", "CHẠM ĐỂ HIỆP NHẤT, NỔ TUNG CÁC MÀU.", "HIỆP NHẤT GÂY THÊM 90% SÁT THƯƠNG,", "TẦM NỔ RỘNG THÊM 30." },
                new[] { "NỘI TẠI: SAU KHI HIỆP NHẤT, MÀU HỒNG", "VẪN GIỮ NGUYÊN TRẠNG THÁI SÁNG." },
                new[] { "NỘI TẠI: HIỆP NHẤT NỔ 2 ĐỢT CÁCH NHAU", "0,3 GIÂY, MỖI ĐỢT GÂY 75% SÁT THƯƠNG." }
            } },
            { "serpent", new[] {
                new[] { "NỘI TẠI: KHI SĂN MỒI, RẮN XOAY HƯỚNG", "NHANH HƠN 0,2 ĐỘ MỖI NHỊP, MỖI BẬC." },
                new[] { "ĐẦU TRẬN: CÓ SẴN 2 ĐỐT ĐUÔI." },
                new[] { "KÍCH HOẠT: LAO TỚI QUẤN SIẾT KẺ ĐỊCH,", "GIỮ CHẶT VÀ CẮN 3 LẦN.", "GIỮ LÂU HƠN 50%. HẾT QUẤN, LÀM CHẬM", "KẺ ĐỊCH 50% TRONG 1,5 GIÂY." },
                new[] { "KÍCH HOẠT: LAO TỚI QUẤN SIẾT KẺ ĐỊCH,", "GIỮ CHẶT VÀ CẮN 3 LẦN.", "GIỮ LÂU HƠN 80%. HẾT QUẤN, LÀM CHẬM", "KẺ ĐỊCH 50% TRONG 1,5 GIÂY." },
                new[] { "KÍCH HOẠT: LAO TỚI QUẤN SIẾT KẺ ĐỊCH,", "GIỮ CHẶT VÀ CẮN LIÊN TỤC.", "CẮN 4 LẦN, MỖI CÚ CẮN HỒI MÁU BẰNG", "30% SÁT THƯƠNG GÂY RA." },
                new[] { "KÍCH HOẠT: LAO TỚI QUẤN SIẾT KẺ ĐỊCH,", "GIỮ CHẶT VÀ CẮN LIÊN TỤC.", "CẮN 5 LẦN, MỖI CÚ CẮN HỒI MÁU BẰNG", "45% SÁT THƯƠNG GÂY RA." },
                new[] { "NỘI TẠI: MỖI CÚ CẮN HỒI THÊM 0,2 MÁU", "CHO MỖI ĐỐT ĐUÔI ĐANG CÓ." },
                new[] { "NỘI TẠI: SAU KHI QUẤN XONG, CHẶN", "1 ĐÒN ĐÁNH TRONG VÒNG 1 GIÂY." }
            } },
        };

        static string[] VietnameseEffect(string weaponId, TreeNode node, char branch, int skillLevel)
        {
            if (node == TreeNode.Vitality) return new[] { $"NỘI TẠI: +{TreeTuning.HpPct(weaponId)}% MÁU TỐI ĐA MỖI BẬC.", "TỐI ĐA 2 BẬC." };
            if (node == TreeNode.Power) return new[] { $"NỘI TẠI: +{TreeTuning.DamagePct(weaponId)}% SÁT THƯƠNG MỖI BẬC.", "TỐI ĐA 2 BẬC." };
            if (!ViFx.TryGetValue(weaponId, out var rows)) return null;
            switch (node)
            {
                case TreeNode.Instinct: return rows[0];
                case TreeNode.Opener: return rows[1];
                case TreeNode.SkillA: return rows[skillLevel == 3 ? 3 : 2];
                case TreeNode.SkillB: return rows[skillLevel == 3 ? 5 : 4];
                default: return rows[branch == 'B' ? 7 : 6];
            }
        }

        static string[] AddDetail(string[] effect, string detail) =>
            new[] { effect[0], effect[1], detail };

        static string[] DescribeSkill(string weaponId, string[] effect)
        {
            if (!SkillContext.TryGetValue(weaponId, out var context)) return effect;
            return string.IsNullOrEmpty(effect[1])
                ? new[] { context[0], context[1], effect[0] }
                : new[] { context[0], context[1], effect[0], effect[1] };
        }

        static Row RowOf(string weaponId) => weaponId != null && Rows.TryGetValue(weaponId, out var r) ? r : Rows["blade"];

        static string Branch(string weaponId, SkillBranch b) => SkillDefinition.ForWeapon(weaponId)?.BranchName(b) ?? (b == SkillBranch.A ? "A" : "B");

        /// <summary>Short name on the node box (at most 11 characters).</summary>
        public static string Name(string weaponId, TreeNode n, int skillLevel = 2)
        {
            var r = RowOf(weaponId);
            switch (n)
            {
                case TreeNode.Vitality: return "VITALITY";
                case TreeNode.Power: return "POWER";
                case TreeNode.Instinct: return r.Instinct;
                case TreeNode.Opener: return r.Opener;
                case TreeNode.SkillA: return Branch(weaponId, SkillBranch.A) + " LV" + skillLevel;
                case TreeNode.SkillB: return Branch(weaponId, SkillBranch.B) + " LV" + skillLevel;
                default: return "FINALE";
            }
        }

        public static string FinaleName(string weaponId, char branch) => branch == 'A' ? RowOf(weaponId).FinA : RowOf(weaponId).FinB;

        /// <summary>Up to four lines describing the trigger, effect and trade-off of a node.</summary>
        public static string[] Effect(string weaponId, TreeNode n, char finaleBranch = 'A', int skillLevel = 2)
        {
            if (StringTable.Current == Lang.Vi)
            {
                var localized = VietnameseEffect(weaponId, n, finaleBranch, skillLevel);
                if (localized != null) return localized;
            }
            var r = RowOf(weaponId);
            var skill = SkillDefinition.ForWeapon(weaponId);
            var detail = NodeContext.TryGetValue(weaponId, out var context) ? context : NodeContext["blade"];
            switch (n)
            {
                case TreeNode.Vitality: return new[] { $"+{TreeTuning.HpPct(weaponId)}% MAX HP", "PER RANK. MAX 2 RANKS" };
                case TreeNode.Power: return new[] { $"+{TreeTuning.DamagePct(weaponId)}% DAMAGE", "PER RANK. MAX 2 RANKS" };
                case TreeNode.Instinct: return AddDetail(r.InstinctFx, detail[0]);
                case TreeNode.Opener: return AddDetail(r.OpenerFx, detail[1]);
                case TreeNode.SkillA:
                    return DescribeSkill(weaponId, SkillFx.TryGetValue(weaponId + ":A" + skillLevel, out var fa) ? fa : new[] { $"{skill?.Name ?? "SKILL"} LV{skillLevel}", skill?.SummaryA ?? "" });
                case TreeNode.SkillB:
                    return DescribeSkill(weaponId, SkillFx.TryGetValue(weaponId + ":B" + skillLevel, out var fb) ? fb : new[] { $"{skill?.Name ?? "SKILL"} LV{skillLevel}", skill?.SummaryB ?? "" });
                default: return AddDetail(finaleBranch == 'A' ? r.FinAFx : r.FinBFx, finaleBranch == 'A' ? detail[2] : detail[3]);
            }
        }
    }
}
