using System.Collections.Generic;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// UI-UX1: what each ball does, in plain words, for the weapon pick and the Codex: how it fights/grows, what it is good at, what beats it.
    /// From docs/design/weapon-bible.md and the BB-BAL2/BAL3 Versus/Run numbers. View text only (EN; VI deferred).
    /// </summary>
    public static class WeaponGuide
    {
        /// <summary>Readable on the dark panel at 1x (HpLost was too dark for text).</summary>
        public static readonly Color32 GoodColor = new Color32(120, 220, 120, 255);
        public static readonly Color32 WeakColor = new Color32(240, 120, 110, 255);
        public sealed class Entry
        {
            public readonly string Grows, Good, Weak;
            public Entry(string grows, string good, string weak) { Grows = grows; Good = good; Weak = weak; }
        }

        static readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>
        {
            { "blade", new Entry("BLADE DMG +1 WITH EVERY HIT", "STEADY DAMAGE. EASIEST TO PLAY", "PARRIES STOP IT GROWING") },
            { "fang", new Entry("SPINS FASTER WITH EVERY HIT", "FAST COMBOS ONCE IT SPINS", "SLOW START. WRECKER CRUSHES IT") },
            { "pike", new Entry("LINE GROWS LONGER EVERY HIT", "HUGE REACH LATE IN THE FIGHT", "LONG LINE IS EASY TO PARRY") },
            { "brawler", new Entry("NO WEAPON. FASTER BODY = MORE DMG", "WALL BOUNCES BUILD SPEED", "MIRROR REFLECTS BODY HITS") },
            { "volley", new Entry("+1 SHOT PER VOLLEY PER MELEE HIT", "DAMAGE FROM RANGE. SHOTS BOUNCE", "TIGHT ARENAS. PARRY TURNS SHOTS") },
            { "venom", new Entry("HITS POISON. MORE STACKS PER HIT", "LONG FIGHTS. BEATS MIRROR", "WEAK START. BURST BEATS IT") },
            { "aegis", new Entry("REFLECTS PARRIES AND BODY HITS", "PUNISHES WRECKER AND BIG HITS", "POISON (SPORE). TIGHT ARENAS") },
            { "rig", new Entry("EACH HIT BUILDS A TURRET (MAX 6)", "TURRETS FILL THE ARENA LATE", "SLOW START. WALLS EAT TURRETS") },
            { "chorus", new Entry("5 ORBS LIGHT ON TOUCH. ALL 5 = UNITY", "HEALS, GUARDS AND BURSTS UP CLOSE", "RANGED BALLS STARVE THE RING") },
            { "serpent", new Entry("EATS PREY. EACH ONE GROWS THE TAIL", "TAIL WHIPS. BITE GROWS WITH LENGTH", "LONG BLADES CUT THE TAIL") },
        };

        /// <summary>The entry for <paramref name="weaponId"/>, or null for an unknown id.</summary>
        public static Entry For(string weaponId) => weaponId != null && entries.TryGetValue(weaponId, out var e) ? e : null;

        /// <summary>Four short tactical lines for the Codex, with natural Vietnamese copy.</summary>
        public static string[] CodexLines(string id, Lang language)
        {
            var vi = language == Lang.Vi;
            switch (id)
            {
                case "blade": return vi ? new[] { "CÁCH ĐÁNH: CÀNG CHÉM CÀNG ĐAU", "ÁP SÁT ĐỂ NUÔI SÁT THƯƠNG MỖI ĐÒN", "MÀI KIẾM: BA ĐÒN TỚI ĐƯỢC TĂNG LỰC", "NHÁNH A ĐÁNH NHIỀU; B ĐÁNH ĐAU" } : new[] { "PLAY: KEEP LANDING BLADE HITS", "EACH HIT BUILDS YOUR DAMAGE", "WHETSTONE BUFFS YOUR NEXT 3 HITS", "A ADDS HITS; B MAKES THEM HEAVIER" };
                case "fang": return vi ? new[] { "CÁCH ĐÁNH: GIỮ NHỊP QUAY", "CÀNG ĐÁNH TRÚNG, XOAY CÀNG NHANH", "CUỒNG QUAY GIỮ TỐC ĐỘ KHÔNG GIẢM", "NHÁNH A GIỮ LÂU; B LÊN NHANH" } : new[] { "PLAY: BUILD AND KEEP YOUR SPIN", "EACH HIT MAKES THE FANG SPIN FASTER", "FRENZY STOPS SPIN DECAY FOR A WHILE", "A LASTS LONGER; B BUILDS SPIN FAST" };
                case "pike": return vi ? new[] { "CÁCH ĐÁNH: GIỮ CỰ LY", "ĐÂM TRÚNG ĐỂ MŨI GIÁO VƯƠN XA HƠN", "LAO TỚI TẠM THỜI TĂNG TẦM ĐÁNH", "NHÁNH A VƯƠN XA; B GIỮ LÂU" } : new[] { "PLAY: FIGHT FROM THE EDGE OF REACH", "HITS EXTEND THE PIKE THROUGH THE FIGHT", "LUNGE GRANTS EXTRA REACH BRIEFLY", "A ADDS REACH; B EXTENDS ITS TIME" };
                case "brawler": return vi ? new[] { "CÁCH ĐÁNH: LẤY ĐÀ MÀ HÚC", "NẢY TƯỜNG ĐỂ TĂNG TỐC VÀ SÁT THƯƠNG", "LAO TỚI: CÚ HÚC ĐẦU MẠNH HƠN", "NHÁNH A HÚC ĐAU; B LAO LÂU" } : new[] { "PLAY: BOUNCE OFF WALLS FOR SPEED", "BODY HITS GET STRONGER AT HIGH SPEED", "CHARGE BOOSTS THE FIRST IMPACT", "A HITS HARDER; B CHARGES LONGER" };
                case "volley": return vi ? new[] { "CÁCH ĐÁNH: BẮN TỪ XA", "ĐÁNH CẬN CHIẾN ĐỂ THÊM MŨI TÊN", "MƯA TÊN BẮN THÊM MỘT LOẠT", "NHÁNH A BẮN NHANH; B BẮN MẠNH" } : new[] { "PLAY: KEEP RANGE AND FIRE VOLLEYS", "MELEE HITS ADD SHOTS TO EACH VOLLEY", "BARRAGE FIRES AN EXTRA VOLLEY", "A FIRES SOONER; B HITS HARDER" };
                case "venom": return vi ? new[] { "CÁCH ĐÁNH: RẢI ĐỘC RỒI CÂU GIỜ", "MỖI ĐÒN ĐỂ LẠI THÊM ĐỘC", "HÚT ĐỘC HỒI MÁU KHI ĐỦ BA TẦNG", "NHÁNH A HỒI NHIỀU; B ĐỘC LÂU" } : new[] { "PLAY: STACK POISON AND BUY TIME", "HITS ADD MORE POISON TO THE FOE", "DRAIN HEALS AT 3 POISON STACKS", "A HEALS MORE; B KEEPS POISON LONGER" };
                case "aegis": return vi ? new[] { "CÁCH ĐÁNH: ĐỠ ĐÒN RỒI PHẢN CÔNG", "CHẶN ĐÒN ĐỂ BẬT SÁT THƯƠNG TRẢ LẠI", "HỘ THỂ TẠM TĂNG LỰC PHẢN ĐÒN", "NHÁNH A GIỮ LÂU; B PHẢN MẠNH" } : new[] { "PLAY: GUARD AND REFLECT BIG HITS", "PARRIES AND BODY HITS TRIGGER REFLECT", "BULWARK BOOSTS REFLECT BRIEFLY", "A LASTS LONGER; B REFLECTS MORE" };
                case "rig": return vi ? new[] { "CÁCH ĐÁNH: DỰNG TRỤ GIỮ SÂN", "ĐÁNH TRÚNG ĐỂ DỰNG THÊM TRỤ SÚNG", "TĂNG TỐC CHO TRỤ BẮN DỒN DẬP", "NHÁNH A BẮN DỒN; B THÊM TRỤ" } : new[] { "PLAY: BUILD TURRETS TO CONTROL SPACE", "EACH HIT BUILDS A TURRET, UP TO SIX", "OVERCLOCK SPEEDS UP TURRET VOLLEYS", "A ADDS BURSTS; B DEPLOYS MORE" };
                case "chorus": return vi ? new[] { "CÁCH ĐÁNH: THẮP SÁNG CẢ VÒNG", "VA CHẠM ĐỂ THẮP NĂM QUẢ CẦU", "ĐỦ NĂM MÀU SẼ HỒI VÀ GÂY SÁT THƯƠNG", "THÁNH CA THẮP NGAY HAI MÀU" } : new[] { "PLAY: LIGHT ALL FIVE ORBS", "TOUCHES LIGHT COLOURS AROUND YOU", "ALL FIVE TRIGGER HEAL AND BURST", "HYMN LIGHTS TWO UNLIT COLOURS" };
                case "serpent": return vi ? new[] { "CÁCH ĐÁNH: ĂN MỒI NUÔI ĐUÔI", "MỖI CON MỒI LÀM ĐUÔI DÀI THÊM", "ĐUÔI QUẬT XA; CẮN MẠNH KHI DÀI", "TẤN CÔNG LAO TỚI CẮN MẠNH HƠN" } : new[] { "PLAY: EAT PREY TO GROW YOUR TAIL", "EACH PREY EXTENDS THE TAIL", "LONGER TAIL MEANS STRONGER BITES", "STRIKE LUNGES IN FOR A BIG BITE" };
                default: return new[] { "", "", "", "" };
            }
        }
    }
}
