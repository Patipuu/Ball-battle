using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BallBattle.EditorTools;
using BallBattle.Sim;
using BallBattle.Sim.Run;
using BallBattle.Sim.Weapons;
using BallBattle.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BallBattle.Tests
{
    /// <summary>
    /// BB-FONT1: the 5x7 variable-width font (file, loading, widths, fallback, NFC), MeasureWidth, the layout of every screen
    /// at 270x480 in EN and VI, the hidden VI switch, and RunStrings.UnlockedNames from the goal reward data.
    /// </summary>
    public class BbFont1Tests
    {
        const float HalfW = TextWrap.ScreenWidth / 2f;
        const float HalfH = TextWrap.ScreenHeight / 2f;
        const float Frame = 1f / 60f;
        public const int MinTapHeight = 24;

        GameObject root;
        ArtLibrary art;

        [SetUp]
        public void SetUp()
        {
            art = AssetDatabase.LoadAssetAtPath<ArtLibrary>(PlaceholderArtGenerator.LibraryPath);
            Assert.That(art, Is.Not.Null);
            root = new GameObject("BbFont1Root");
            StringTable.Current = Lang.En;
        }

        [TearDown]
        public void TearDown()
        {
            StringTable.Current = Lang.En;
            Time.timeScale = 1f;
            Object.DestroyImmediate(root);
        }

        // ================= A1: the font file =================

        /// <summary>Every character the 5x7 v2 file has: space, the old punctuation plus the comma (BB-SFX2), 0-9, A-Z, Đ and the 72 Vietnamese capital vowels.</summary>
        public static readonly string ExpectedChars = BuildExpected();

        static string BuildExpected()
        {
            var sb = new StringBuilder(" !%+-./:?,0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZĐĂÂÊÔƠƯ");
            foreach (var b in "AĂÂEÊIOÔƠUƯY")
                foreach (var t in new[] { "\u0300", "\u0301", "\u0309", "\u0303", "\u0323" })
                    sb.Append((b + t).Normalize(NormalizationForm.FormC));
            return sb.ToString();
        }

        [Test]
        public void FontFileIsValidAndEveryCharacterLoads()
        {
            var asset = Resources.Load<TextAsset>(PixelFont.ResourcePath);
            Assert.That(asset, Is.Not.Null, "Resources/Fonts/pixelfont_5x7.json");
            Assert.That(asset.text.Normalize(NormalizationForm.FormC), Is.EqualTo(asset.text.TrimStart('\uFEFF')), "file is NFC");
            var font = PixelFont.Parse(asset.text);
            Assert.That(ExpectedChars.Length, Is.EqualTo(113));
            Assert.That(font.Count, Is.EqualTo(ExpectedChars.Length), "v2 file: 113 glyphs, comma included");
            foreach (var c in ExpectedChars) Assert.That(font.Has(c), Is.True, $"'{c}' U+{(int)c:X4}");
            foreach (var c in font.Chars)
            {
                var g = font.Get(c);
                Assert.That(g.Rows.Length, Is.EqualTo(PixelFont.CellHeight), $"'{c}' rows");
                Assert.That(g.Rows.All(r => r.Length == g.Width), Is.True, $"'{c}' row widths");
                Assert.That(g.Width, Is.InRange(PixelFont.MinGlyphWidth, PixelFont.MaxGlyphWidth), $"'{c}' width");
                Assert.That(g.Rows[11].Contains('#'), Is.EqualTo(c == ','), $"'{c}' row 11 (only the comma's tail reaches it)");
                // Row 3 is blank except for the horn of Ơ/Ư (and their toned forms), which Art drew reaching into it.
                var horn = "ƠỚỜỞỠỢƯỨỪỬỮỰ".IndexOf(c) >= 0;
                Assert.That(g.Rows[3].Contains('#'), Is.EqualTo(horn), $"'{c}' row 3");
            }
            // Rows 0-2 marks, 4-10 body, 12 dot below.
            Assert.That(font.Get('Ở').Rows.Take(3).Any(r => r.Contains('#')), Is.True, "Ở has a mark above");
            Assert.That(font.Get('Ạ').Rows[12].Contains('#'), Is.True, "Ạ has the dot below");
            Assert.That(font.Get('A').Rows.Take(3).Concat(new[] { font.Get('A').Rows[12] }).Any(r => r.Contains('#')), Is.False, "A body only");
        }

        [Test]
        public void ParseRejectsMalformedGlyphs()
        {
            string Row(int w, char c = '.') => "\"" + new string(c, w) + "\"";
            string Glyph(string key, params string[] rows) => "\"" + key + "\": [" + string.Join(",", rows) + "]";
            var thirteen = Enumerable.Repeat(Row(5), 13).ToArray();
            var space = Glyph(" ", Enumerable.Repeat(Row(3), 13).ToArray());
            Assert.That(PixelFont.Parse("{" + space + "," + Glyph("A", thirteen) + "}").Count, Is.EqualTo(2));
            Assert.Throws<FormatException>(() => PixelFont.Parse("{" + space + "," + Glyph("A", thirteen.Take(12).ToArray()) + "}"), "12 rows");
            var ragged = thirteen.ToArray();
            ragged[5] = Row(4);
            Assert.Throws<FormatException>(() => PixelFont.Parse("{" + space + "," + Glyph("A", ragged) + "}"), "unequal rows");
            var bad = thirteen.ToArray();
            bad[5] = Row(5, 'x');
            Assert.Throws<FormatException>(() => PixelFont.Parse("{" + space + "," + Glyph("A", bad) + "}"), "bad pixel");
            Assert.Throws<FormatException>(() => PixelFont.Parse("{" + space + "," + Glyph("A", Enumerable.Repeat(Row(8), 13).ToArray()) + "}"), "too wide");
            Assert.Throws<FormatException>(() => PixelFont.Parse("{" + Glyph("A", thirteen) + "}"), "no space glyph");
            Assert.That(PixelFont.Parse("{" + space + ",\"\\u00C0\": [" + string.Join(",", thirteen) + "]}").Has('À'), Is.True, "\\u escapes");
        }

        // ================= A2: drawing =================

        [Test]
        public void MixedWidthsDrawAtTheirRealWidthsOnePixelApart()
        {
            var font = PixelFont.Default;
            var widths = font.Chars.Select(c => font.Get(c).Width).Distinct().OrderBy(w => w).ToList();
            Assert.That(widths.Count, Is.GreaterThanOrEqualTo(3), "variable width: " + string.Join(",", widths));
            var t = PixelText.Create(root.transform, "T", art.Font, Vector2.zero, PixelText.Align.Left, 2, 0);
            const string s = "W.M I";
            t.Set(s, Palette.Text);
            var glyphs = t.GetComponentsInChildren<SpriteRenderer>(true);
            var x = 0f;
            for (var i = 0; i < s.Length; i++)
            {
                var g = font.Get(s[i]);
                Assert.That(glyphs[i].transform.localPosition.x, Is.EqualTo(x), $"'{s[i]}' x");
                if (!g.Blank) Assert.That(glyphs[i].sprite.rect.width, Is.EqualTo(g.Width), $"'{s[i]}' sprite width");
                Assert.That(glyphs[i].enabled, Is.EqualTo(!g.Blank));
                x += (g.Width + PixelFont.Spacing) * 2;
            }
            Assert.That(t.Bounds.height, Is.EqualTo(PixelFont.CellHeight * 2));
            Assert.That(font.Atlas.filterMode, Is.EqualTo(FilterMode.Point));
        }

        [Test]
        public void LowercaseAndDecomposedInputDrawTheSameAsNfcCapitals()
        {
            var t = PixelText.Create(root.transform, "T", art.Font, Vector2.zero, PixelText.Align.Left, 1, 0);
            t.Set("bo\u031B\u0309 tra\u0302\u0300n", Palette.Text);   // decomposed lowercase "bở trần"
            Assert.That(t.Text, Is.EqualTo("BỞ TRẦN"));
            Assert.That(PixelFont.MeasureWidth("bở trần"), Is.EqualTo(PixelFont.MeasureWidth("BỞ TRẦN")));
            Assert.That(PixelFont.Normalize("abc"), Is.EqualTo("ABC"));
        }

        [Test]
        public void MissingCharacterDrawsTheFallbackAndIsLoggedOnce()
        {
            var font = PixelFont.Parse(Resources.Load<TextAsset>(PixelFont.ResourcePath).text);
            Assert.That(font.Has('#'), Is.False, "'#' is not in the font");
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("no glyph for '#'"));
            Assert.That(font.Get('#').Char, Is.EqualTo(PixelFont.Fallback));
            Assert.That(font.Get('#').Char, Is.EqualTo(PixelFont.Fallback), "second time: no second log");
            LogAssert.NoUnexpectedReceived();
            Assert.That(font.Measure("A#B"), Is.EqualTo(font.Measure("A B")), "fallback = the space glyph");
        }

        // ================= A3: MeasureWidth =================

        [Test]
        public void MeasureWidthSumsRealGlyphWidthsPlusOnePixelSpacing()
        {
            var font = PixelFont.Default;
            foreach (var s in new[] { "A", "MI", "W.M", "BOSS Ở TRẬN 4", "THUA LẦN 1: CHỌN THẺ MIỄN PHÍ", "  ", "1/100" })
            {
                var sum = s.Sum(c => font.Get(c).Width) + (s.Length - 1) * PixelFont.Spacing;
                Assert.That(PixelFont.MeasureWidth(s), Is.EqualTo(sum), s);
            }
            Assert.That(PixelFont.MeasureWidth(""), Is.Zero);
            Assert.That(PixelFont.MeasureWidth(null), Is.Zero);
            Assert.That(PixelFont.MeasureWidth("I"), Is.Not.EqualTo(PixelFont.MeasureWidth("W")).Or.Not.EqualTo(PixelFont.MeasureWidth(".")), "not fixed width");
            var t = PixelText.Create(root.transform, "T", art.Font, Vector2.zero, PixelText.Align.Center, 3, 0);
            t.Set("MIỄN PHÍ", Palette.Text);
            Assert.That(t.Bounds.width, Is.EqualTo(PixelFont.MeasureWidth("MIỄN PHÍ") * 3));
        }

        [Test]
        public void WrapUsesPixelWidths()
        {
            var lines = TextWrap.Items(new[] { "MIRROR", "SPORE", "SPARKLER", "WRECKER", "PARRY MASTER" }, "UNLOCKED: ", "  ", 120, true);
            Assert.That(lines.Count, Is.GreaterThan(1));
            foreach (var l in lines) Assert.That(PixelFont.MeasureWidth(l), Is.LessThanOrEqualTo(120), l);
            Assert.That(TextWrap.Cut("WWWWWWWWWW", 20), Is.EqualTo("WWW"));
        }

        // ================= A5: hidden VI switch =================

        [Test]
        public void VietnameseIsOffByDefaultAndOnlyAHiddenSwitchTurnsItOn()
        {
            Assert.That(StringTable.Current, Is.EqualTo(Lang.En));
            Assert.That(DebugOptions.ParseLangVi(new[] { "game.exe" }), Is.False);
            Assert.That(DebugOptions.ParseLangVi(null), Is.False);
            Assert.That(DebugOptions.ParseLangVi(new[] { "game.exe", "-bbLangVi" }), Is.True);
            StringTable.Current = Lang.Vi;   // the test hook
            var t = PixelText.Create(root.transform, "T", art.Font, Vector2.zero, PixelText.Align.Left, 1, 0);
            t.Set(RunStrings.BossAt, Palette.Text);
            Assert.That(t.Text, Is.EqualTo("BOSS Ở TRẬN 4 VÀ 8"));
            Assert.That(t.GetComponentsInChildren<SpriteRenderer>().Where(r => r.enabled).All(r => r.sprite != null && !r.sprite.name.EndsWith(" ")), Is.True);
        }

        // ================= C: unlock names =================

        [Test]
        public void EveryGoalThatUnlocksSomethingNamesItsRewards()
        {
            var expected = new Dictionary<string, string[]>
            {
                { UnlockRules.Boss1, new[] { "BGLGXLQ", "GLASS CANNON" } },   // BB-14
                { UnlockRules.Win1, new[] { "SPARKLER", "POISON TIP", "MIRROR", "SPORE", "HUNTER", "BUBBLE", "PARRY MASTER" } },   // BB-14: + the snake
                { UnlockRules.Win3, new[] { "HIVE", "TWIN BLADE" } },
            };
            var ids = new HashSet<string> { UnlockRules.Boss1, UnlockRules.Boss2, UnlockRules.Boss3, UnlockRules.Win1, UnlockRules.Win3 };
            foreach (var row in UnlockRules.GoalTable)
            {
                ids.Add(row.Id);
                var rewards = UnlockRules.NewRewards(row, new ProgressData());
                if (rewards.Count == 0) continue;
                Assert.That(expected.ContainsKey(row.Id), Is.True, $"goal {row.Id} unlocks something: add it here");
                Assert.That(RunStrings.UnlockedNames(row.Id), Is.EqualTo(expected[row.Id]), row.Id);
            }
            Assert.That(RunStrings.UnlockedNames(UnlockRules.Boss2), Is.EqualTo(new[] { "SPARKLER", "POISON TIP" }), "achievement without a goal row");
            foreach (var a in ids)
                foreach (var n in RunStrings.UnlockedNames(a))
                {
                    Assert.That(ids.Contains(n) || ids.Contains(n.ToLowerInvariant()), Is.False, $"{a}: '{n}' is an id");
                    Assert.That(WeaponRegistry.All.Any(w => w.Id == n.ToLowerInvariant()), Is.False, $"{a}: '{n}' is a weapon id");
                }
            Assert.That(RunStrings.UnlockedNames("no-such-goal"), Is.Empty, "never shows an id");
        }

        // ================= A4: every screen at 270x480, EN and VI =================

        public struct Shot
        {
            public string Name;
            /// <summary>What the audit checks (a dialog: only the dialog).</summary>
            public Transform Audit;
            /// <summary>What a capture shows (the whole screen).</summary>
            public Transform Screen;
            public Action Show;
        }

        [Test]
        public void EveryScreenFitsWithoutOverlapInEnglish() => AuditAll(Lang.En);

        [Test]
        public void EveryScreenFitsWithoutOverlapInVietnamese() => AuditAll(Lang.Vi);

        void AuditAll(Lang lang)
        {
            StringTable.Current = lang;
            var issues = new List<string>();
            var count = 0;
            foreach (var shot in Shots(root.transform))
            {
                shot.Show();
                count++;
                foreach (var i in Audit(shot.Audit)) issues.Add($"[{lang}] {shot.Name}: {i}");
                Hide(shot);
            }
            Assert.That(count, Is.GreaterThanOrEqualTo(30), "screens audited");
            var outDir = Environment.GetEnvironmentVariable("BBFONT_AUDIT");
            if (!string.IsNullOrEmpty(outDir))
            {
                System.IO.Directory.CreateDirectory(outDir);
                System.IO.File.WriteAllLines(System.IO.Path.Combine(outDir, $"audit-{lang}.txt"), issues.Count == 0 ? new[] { "OK " + count } : issues.ToArray());
            }
            Assert.That(issues, Is.Empty, string.Join("\n", issues));
        }

        static void Hide(Shot shot)
        {
            var t = shot.Screen;
            while (t != null && t.parent != null && t.parent.name != "BbFont1Root") t = t.parent;
            if (t != null) t.gameObject.SetActive(false);
        }

        /// <summary>
        /// Text rects (13*scale px cells) inside 270x480, no two texts overlapping, every button label inside its button,
        /// every button at least <see cref="MinTapHeight"/> px tall.
        /// </summary>
        public static List<string> Audit(Transform screen)
        {
            var issues = new List<string>();
            var texts = screen.GetComponentsInChildren<PixelText>(false).Where(t => t.Text.Trim().Length > 0).ToList();
            if (texts.Count == 0) issues.Add("nothing drawn");
            foreach (var t in texts)
            {
                var b = t.WorldBounds;
                if (b.xMin < -HalfW || b.xMax > HalfW || b.yMin < -HalfH || b.yMax > HalfH)
                    issues.Add($"'{t.Text}' (x{t.TextScale}) off screen {R(b)}");
            }
            for (var i = 0; i < texts.Count; i++)
                for (var j = i + 1; j < texts.Count; j++)
                    if (texts[i].WorldBounds.Overlaps(texts[j].WorldBounds))
                        issues.Add($"'{texts[i].Text}' {R(texts[i].WorldBounds)} overlaps '{texts[j].Text}' {R(texts[j].WorldBounds)}");
            foreach (var btn in screen.GetComponentsInChildren<PixelButton>(false))
            {
                if (btn.Area.width >= 270f && btn.Area.height >= 480f) continue;   // full-screen tap catcher
                if (btn.Area.height < MinTapHeight) issues.Add($"button '{btn.name}' only {btn.Area.height} px tall");
                var label = btn.transform.Find("Label");
                var lt = label != null && label.gameObject.activeInHierarchy ? label.GetComponent<PixelText>() : null;
                if (lt == null || lt.Text.Trim().Length == 0) continue;
                var lb = lt.Bounds;
                if (lb.xMin < btn.Area.xMin + 1 || lb.xMax > btn.Area.xMax - 1 || lb.yMin < btn.Area.yMin || lb.yMax > btn.Area.yMax)
                    issues.Add($"label '{lt.Text}' {R(lb)} outside button {R(btn.Area)}");
            }
            return issues;
        }

        static string R(Rect r) => $"({r.xMin:0},{r.yMin:0})-({r.xMax:0},{r.yMax:0})";

        // ---- the catalogue ----

        static ProgressData P(int bestFight = 0, int runsWon = 0, params string[] achievements)
        {
            var p = new ProgressData { BestFight = bestFight, RunsWon = runsWon, RunsPlayed = runsWon + 2 };
            p.Achievements.AddRange(achievements);
            return p;
        }

        static string FirstChoice(uint seed) => RunState.StartChoices(seed, UnlockRules.Weapons(new ProgressData()))[0];

        static RunState StartedRun(uint seed = 53, string weapon = null)
        {
            var run = RunState.Start(seed, weapon ?? FirstChoice(seed), new ProgressData());
            run.Pick(0);
            return run;
        }

        static RunState SavedRun(int fightIndex, int retryCount)
        {
            var run = StartedRun(79);
            run.FightIndex = fightIndex;
            run.RetryCount = retryCount;
            return run;
        }

        static RunFlow FlowWithSave(ProgressData progress, RunState saved)
        {
            var store = new MemorySaveStore();
            store.Save(new SaveData { Progress = progress, HasRun = saved != null, Run = RunSave.From(saved) });
            var flow = new RunFlow(store);
            flow.Open();
            return flow;
        }

        static RunState FinishedRun(bool won, int retryAt, params string[] achievements)
        {
            var run = StartedRun(77);
            var last = won ? RunTuning.Fights : 6;
            for (var i = 0; i < last; i++)
            {
                if (i + 1 == retryAt) run.History.Add(new FightRecord { FightIndex = i, Won = false });
                run.History.Add(new FightRecord { FightIndex = i, Won = won || i < last - 1, Retry = i + 1 == retryAt });
            }
            run.RetryCount = retryAt > 0 || !won ? 1 : 0;
            run.FightIndex = won ? RunTuning.Fights : last - 1;
            run.Over = true;
            run.Won = won;
            run.Achievements.AddRange(achievements);
            return run;
        }

        static FightReport Report(FightOutcome outcome, bool runOver = false, bool runWon = false, int retriesAfter = 1)
        {
            return new FightReport
            {
                FightNumber = runOver ? RunTuning.Fights : 3, WasBoss = runOver, EnemyWeaponId = VolleyRule.WeaponId, Outcome = outcome,
                EndReason = MatchEndReason.Knockout, Seconds = 47.5f, HpLeft = outcome == FightOutcome.Win ? 41.5f : 0f, MaxHpInFight = 135f,
                DamageDealt = 188.4f, DamageTaken = 132.2f, EnemyHealed = 36.7f, CoinsGained = 3, CoinsTotal = 12, HpNext = 113.6f, MaxHpNext = 135f,
                RetriesBefore = 1, RetriesAfter = retriesAfter, PlayerSecondWindTriggered = true, IsRetry = false, RunOver = runOver, RunWon = runWon,
                NextFightNumber = 4, NextIsBoss = true, NextWeaponId = AegisRule.WeaponId, NextTraits = "SECOND WIND 2  THORNS 2  VAMPIRE",
                NextEnemyHp = 412.9f, NextArenaId = ArenaRegistry.All[ArenaRegistry.All.Count - 1].Id,
            };
        }

        List<Shot> Shots(Transform parent)
        {
            var list = new List<Shot>();
            void Add(string name, Transform screen, Action show, Transform audit = null) =>
                list.Add(new Shot { Name = name, Screen = screen, Audit = audit ?? screen, Show = show });

            // Main menu: hub (3 states), notice over the hub, Versus pick, Settings.
            var menu = MenuView.Create(parent, art);
            Add("menu-new", menu.transform, () => { menu.gameObject.SetActive(true); menu.ShowHub(); menu.SetRunInfo(new ProgressData(), null); });
            Add("menu-continue", menu.transform, () => { menu.gameObject.SetActive(true); menu.ShowHub(); menu.SetRunInfo(P(5, 0, UnlockRules.Boss1), SavedRun(5, 1)); });
            Add("menu-alldone", menu.transform, () =>
                { menu.gameObject.SetActive(true); menu.ShowHub(); menu.SetRunInfo(P(8, 3, UnlockRules.Boss1, UnlockRules.Boss2, UnlockRules.Win1, UnlockRules.Win3), null); });
            Add("menu-notice", menu.transform, () => { menu.gameObject.SetActive(true); menu.SetRunInfo(P(5, 0, UnlockRules.Boss1), null); menu.ShowOldRunNotice(null); }, menu.NoticePage);
            Add("menu-notice-hub", menu.transform, () => { menu.gameObject.SetActive(true); menu.SetRunInfo(P(5, 0, UnlockRules.Boss1), null); menu.ShowOldRunNotice(null); }, menu.Hub);
            Add("versus-pick", menu.transform, () => { menu.gameObject.SetActive(true); menu.ShowVersus(); menu.Select(0, VolleyRule.WeaponId); menu.Select(1, BrawlerRule.WeaponId); });
            Add("settings", menu.transform, () => { menu.gameObject.SetActive(true); menu.ShowSettings(); });

            // Run title (with a saved run: CONTINUE, stats, goal, rules band) and its ABANDON dialog.
            var title = RunTitleView.Create(parent, art);
            Add("runtitle", title.transform, () => title.Show(FlowWithSave(P(5, 1, UnlockRules.Boss1), SavedRun(4, 0))));
            Add("runtitle-new", title.transform, () => title.Show(FlowWithSave(new ProgressData(), null)));
            var titleDialog = title.transform.Find("ConfirmAbandon");
            Add("runtitle-abandon", title.transform, () => { title.Show(FlowWithSave(P(5, 1, UnlockRules.Boss1), SavedRun(4, 0))); title.PressNewRun(); }, titleDialog);

            // Pick weapon, start pick (start / retry).
            var pick = PickWeaponView.Create(parent, art);
            Add("pickweapon", pick.transform, () => pick.Show(RunState.StartChoices(7, UnlockRules.Weapons(P(8, 3, UnlockRules.Boss1, UnlockRules.Boss2, UnlockRules.Win1, UnlockRules.Win3)))));
            var start = StartPickView.Create(parent, art);
            Add("startpick", start.transform, () => start.Show(RunState.Start(53, FirstChoice(53), new ProgressData())));
            Add("startpick-retry", start.transform, () => { var r = RunState.Start(54, FirstChoice(54), new ProgressData()); r.IsRetry = true; start.Show(r); });

            // Prepare: fresh, boss, blocked/taken/wrapped titles, confirm dialogs.
            var prepare = PrepareView.Create(parent, art);
            Add("prepare", prepare.transform, () => prepare.Show(StartedRun()));
            Add("prepare-boss", prepare.transform, () => { var r = StartedRun(60); r.FightIndex = RunTuning.BossFights[0]; prepare.Show(r); });
            Add("prepare-blocked", prepare.transform, () =>
            {
                var r = StartedRun(61);
                r.OfferCards[0] = OfferSlot.From(new Card(CardKind.SwapWeapon, r.Build.WeaponId));
                r.OfferCards[1] = OfferSlot.From(new Card(CardKind.Trait, "second-wind", 2));
                r.OfferCards[2] = OfferSlot.From(new Card(CardKind.Heal));
                r.Coins = 0;
                prepare.Show(r);
            });
            Add("prepare-taken", prepare.transform, () =>
            {
                var r = StartedRun(62);
                r.Coins = 0;
                r.Pick(0);
                prepare.Show(r);
            });
            Add("prepare-reroll-again", prepare.transform, () => { var r = StartedRun(63); r.Coins = 9; r.Reroll(); prepare.Show(r); });
            var prepareDialog = prepare.transform.Find("Confirm");
            Add("prepare-skip-dialog", prepare.transform, () => { prepare.Show(StartedRun(64)); prepare.PressFight(); }, prepareDialog);
            Add("prepare-swap-dialog", prepare.transform, () =>
            {
                var r = StartedRun(65);
                r.OfferCards[0] = OfferSlot.From(new Card(CardKind.SwapWeapon, r.Build.WeaponId == VolleyRule.WeaponId ? BrawlerRule.WeaponId : VolleyRule.WeaponId));
                prepare.Show(r);
                prepare.PressCard(0);
            }, prepareDialog);

            // Fight result: win (all extra lines), lost with retry, run over, run won.
            var result = RunResultView.Create(parent, art);
            Add("result-win", result.transform, () => result.Show(Report(FightOutcome.Win)));
            Add("result-lost-retry", result.transform, () => result.Show(Report(FightOutcome.Lost, retriesAfter: 0)));
            Add("result-draw", result.transform, () => result.Show(Report(FightOutcome.Draw, retriesAfter: 0)));
            Add("result-runover", result.transform, () => result.Show(Report(FightOutcome.Lost, runOver: true, retriesAfter: 0)));
            Add("result-runwon", result.transform, () => result.Show(Report(FightOutcome.Win, runOver: true, runWon: true)));

            // Summary: win with unlocks, loss, win after retry, everything at once.
            var summary = RunSummaryView.Create(parent, art);
            Add("summary-win", summary.transform, () =>
                summary.Show(FinishedRun(true, 0, UnlockRules.Boss2, UnlockRules.Win1), P(8, 1, UnlockRules.Boss1, UnlockRules.Boss2, UnlockRules.Win1)));
            Add("summary-loss", summary.transform, () => summary.Show(FinishedRun(false, 0, UnlockRules.Boss1), P(6, 0, UnlockRules.Boss1)));
            Add("summary-win-retry", summary.transform, () =>
                summary.Show(FinishedRun(true, 3, UnlockRules.Boss1, UnlockRules.Boss2, UnlockRules.Win1), P(8, 1, UnlockRules.Boss1, UnlockRules.Boss2, UnlockRules.Win1)));
            Add("summary-everything", summary.transform, () =>
                summary.Show(FinishedRun(true, 3, UnlockRules.Boss1, UnlockRules.Boss2, UnlockRules.Win1, UnlockRules.Win3),
                             P(8, 3, UnlockRules.Boss1, UnlockRules.Boss2, UnlockRules.Win1, UnlockRules.Win3)));

            // Versus: HUD with both long names, result panel.
            var versusRoot = new GameObject("VersusFight").transform;
            versusRoot.SetParent(parent, false);
            var arena = versusRoot.gameObject.AddComponent<ArenaView>();
            arena.Art = art;
            arena.AutoRestart = false;
            var overlay = OverlayView.Create(versusRoot, art);
            var flow = new MatchFlow(3f, 2f);
            Add("versus-countdown", versusRoot, () =>
            {
                versusRoot.gameObject.SetActive(true);
                overlay.HideAll();
                arena.StartMatch(VolleyRule.WeaponId, BrawlerRule.WeaponId, 12345u);
                flow.Begin(VolleyRule.WeaponId, BrawlerRule.WeaponId, 12345u);
                overlay.Render(flow, 0f);
                arena.Advance(Frame);
            });
            Add("versus-banner", versusRoot, () =>
            {
                versusRoot.gameObject.SetActive(true);
                overlay.HideAll();
                arena.StartMatch(VolleyRule.WeaponId, BrawlerRule.WeaponId, 12345u);
                flow.Begin(VolleyRule.WeaponId, BrawlerRule.WeaponId, 12345u);
                flow.Tick(3.1f);
                flow.OnRoundEnded(0);
                overlay.Render(flow, 0f);
                arena.Advance(Frame);
            });
            Add("versus-result", versusRoot, () =>
            {
                versusRoot.gameObject.SetActive(true);
                overlay.HideAll();
                flow.Begin(VolleyRule.WeaponId, BrawlerRule.WeaponId, 12345u);
                for (var round = 0; round < 2; round++)
                {
                    overlay.Render(flow, 0f);
                    flow.Tick(3.1f);
                    flow.OnRoundEnded(0);
                    overlay.Render(flow, 0f);
                    flow.Tick(2.1f);
                }
                flow.Tick(2.1f);
                overlay.Render(flow, 0f);
            }, overlay.transform.Find("Result"));

            // Run fight through the real wiring: countdown + HUD (YOU arrow), x2 button, retry border, retry banner.
            var runRoot = new GameObject("RunFight").transform;
            runRoot.SetParent(parent, false);
            var runArena = runRoot.gameObject.AddComponent<ArenaView>();
            runArena.Art = art;
            runArena.AutoRestart = false;
            var rc = RunController.Create(runRoot, runArena, new MemorySaveStore());
            var runOverlay = runRoot.GetComponentInChildren<RunOverlayView>(true);
            void StartFight(bool retry)
            {
                runRoot.gameObject.SetActive(true);
                rc.Open();
                rc.Flow.NewRun(1791713962u);
                rc.Tick(Frame);
                rc.Flow.ChooseWeapon(FirstChoice(1791713962u));
                rc.Flow.PickStart(0);
                rc.Flow.Run.FightIndex = RunTuning.BossFights[0];
                rc.Flow.Run.IsRetry = retry;
                rc.Flow.Run.RetryCount = retry ? 1 : 0;
                rc.Tick(Frame);
                rc.Prepare.PressFight();
                if (rc.Prepare.ConfirmOpen) rc.Prepare.PressConfirm(true);
                rc.Tick(Frame);
                runArena.Advance(Frame);
                rc.Tick(Frame);
            }
            Add("run-countdown", runRoot, () => StartFight(false));
            Add("run-retry-countdown", runRoot, () => StartFight(true));
            Add("run-retry-banner", runRoot, () => { StartFight(true); runOverlay.ShowRetryBanner(); }, runOverlay.transform.Find("RetryBanner"));
            Add("run-playing", runRoot, () =>
            {
                StartFight(false);
                var guard = 0;
                while (rc.Flow.Current == RunFlow.State.Countdown && guard++ < 600) { rc.Tick(Frame); runArena.Advance(Frame); }
                for (var i = 0; i < 120; i++) { rc.Tick(Frame); runArena.Advance(Frame); }
            });
            Add("run-fightover", runRoot, () =>
            {
                StartFight(false);
                runArena.TimeScale = 4f;
                var guard = 0;
                while (rc.Flow.Current != RunFlow.State.FightOver && rc.Flow.Current != RunFlow.State.FightResult && guard++ < 60 * 400)
                { rc.Tick(Frame); runArena.Advance(Frame); }
                runArena.TimeScale = 1f;
            });

            foreach (var s in list) Hide(s);
            return list;
        }

        // ---- screenshots (BBFONT_CAPTURE=folder; skipped when unset) ----

        [Test]
        public void CaptureFontScreens()
        {
            var dir = Environment.GetEnvironmentVariable("BBFONT_CAPTURE");
            if (string.IsNullOrEmpty(dir)) Assert.Ignore("BBFONT_CAPTURE not set");
            System.IO.Directory.CreateDirectory(dir);
            var camGo = new GameObject("CaptureCam");
            try
            {
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 240f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Palette.Background;
                camGo.transform.position = new Vector3(0f, 0f, -10f);
                foreach (var lang in new[] { Lang.En, Lang.Vi })
                {
                    StringTable.Current = lang;
                    var holder = new GameObject("BbFont1Root");
                    holder.transform.SetParent(root.transform, false);
                    foreach (var shot in Shots(holder.transform))
                    {
                        shot.Show();
                        Shoot(cam, System.IO.Path.Combine(dir, $"{lang.ToString().ToLowerInvariant()}-{shot.Name}.png"));
                        HideAll(holder.transform);
                    }
                    Object.DestroyImmediate(holder);
                }
            }
            finally
            {
                Object.DestroyImmediate(camGo);
            }
        }

        static void HideAll(Transform holder)
        {
            foreach (Transform c in holder) c.gameObject.SetActive(false);
        }

        static void Shoot(Camera cam, string path)
        {
            var rt = new RenderTexture(270, 480, 24) { filterMode = FilterMode.Point };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(270, 480, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 270, 480), 0, 0);
            tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(tex);
            rt.Release();
        }
    }
}
