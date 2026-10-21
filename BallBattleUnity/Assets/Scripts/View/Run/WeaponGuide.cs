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

        /// <summary>Two tactical lines for the Codex. The skill and both branches are already shown above them.</summary>
        public static string[] CodexLines(string id, Lang language)
        {
            var vi = language == Lang.Vi;
            switch (id)
            {
                case "blade": return vi ? Two("CÁCH ĐÁNH: CHÉM TRÚNG ĐỂ NUÔI ĐAO", "ĐỠ ĐÒN SẼ NGẮT ĐÀ TĂNG SÁT THƯƠNG") : Two("PLAY: LAND BLADE HITS", "PARRIES STOP ITS DAMAGE GROWTH");
                case "fang": return vi ? Two("CÁCH ĐÁNH: TÍCH ĐÀ XOÁY", "TRÚNG ĐÒN LIÊN TỤC, XOÁY CÀNG NHANH") : Two("PLAY: BUILD SPIN", "EACH HIT MAKES IT SPIN FASTER");
                case "pike": return vi ? Two("CÁCH ĐÁNH: GIỮ CỰ LY", "ĐÂM TRÚNG ĐỂ NỚI TẦM GIÁO") : Two("PLAY: KEEP YOUR DISTANCE", "HITS EXTEND THE PIKE");
                case "brawler": return vi ? Two("CÁCH ĐÁNH: NẢY TƯỜNG LẤY ĐÀ", "TỐC ĐỘ CAO, CÚ HÚC CÀNG ĐAU") : Two("PLAY: BOUNCE FOR SPEED", "FASTER BODY HITS HURT MORE");
                case "volley": return vi ? Two("CÁCH ĐÁNH: BẮN TỪ XA", "ĐÁNH CẬN CHIẾN ĐỂ THÊM ĐẠN") : Two("PLAY: FIRE FROM RANGE", "MELEE HITS ADD SHOTS TO VOLLEYS");
                case "venom": return vi ? Two("CÁCH ĐÁNH: CHỒNG ĐỘC", "ĐỘC CÀNG NHIỀU, TRẬN DÀI CÀNG LỢI") : Two("PLAY: STACK POISON", "LONG FIGHTS FAVOR THE POISON");
                case "aegis": return vi ? Two("CÁCH ĐÁNH: ĐỠ RỒI PHẢN ĐÒN", "VA CHẠM CŨNG KÍCH HOẠT PHẢN ĐÒN") : Two("PLAY: PARRY AND REFLECT", "BODY HITS ALSO TRIGGER REFLECT");
                case "rig": return vi ? Two("CÁCH ĐÁNH: DỰNG TRỤ GIỮ SÂN", "TRÚNG ĐÒN ĐỂ DỰNG THÊM TRỤ") : Two("PLAY: CONTROL SPACE WITH TURRETS", "HITS BUILD MORE TURRETS");
                case "chorus": return vi ? Two("CÁCH ĐÁNH: THẮP SÁNG NĂM MÀU", "ĐỦ MÀU SẼ HỒI MÁU VÀ GÂY NỔ") : Two("PLAY: LIGHT ALL FIVE ORBS", "ALL FIVE TRIGGER HEAL AND BURST");
                case "serpent": return vi ? Two("CÁCH ĐÁNH: NUÔI ĐUÔI BẰNG MỒI", "ĐUÔI DÀI GIÚP CẮN MẠNH HƠN") : Two("PLAY: EAT PREY TO GROW", "LONGER TAIL MEANS STRONGER BITES");
                default: return new[] { "", "", "", "" };
            }
        }

        static string[] Two(string first, string second) => new[] { first, second, "", "" };
    }
}
