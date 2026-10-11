using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BallBattle.Sim;
using BallBattle.Sim.Run;
using BallBattle.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BallBattle.Tests
{
    /// <summary>View-only UI fixes: Prepare screen feedback, hitboxes, real HP, player marker, boss size and flash, popups, text fit.</summary>
    public class PrepareAndFightUiTests
    {
        const float HalfW = TextWrap.ScreenWidth / 2f;
        const float HalfH = TextWrap.ScreenHeight / 2f;

        GameObject root;
        ArtLibrary art;

        [SetUp]
        public void SetUp()
        {
            art = AssetDatabase.LoadAssetAtPath<ArtLibrary>("Assets/Art/ArtLibrary.asset");
            Assert.That(art, Is.Not.Null, "run BallBattle/Build Scenes");
            root = new GameObject("UiTestRoot");
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        static RunState NewRun(string weapon = "blade")
        {
            var run = RunState.Start(77, weapon, new ProgressData());
            run.Coins = 10;
            return run;
        }

        static void Offer(RunState run, params Card[] cards)
        {
            run.OfferCards.Clear();
            foreach (var c in cards) run.OfferCards.Add(OfferSlot.From(c));
        }

        PrepareView NewPrepare(RunState run)
        {
            var v = PrepareView.Create(root.transform, art);
            v.Show(run);
            return v;
        }

        // ---- 1. A tap on a card or REROLL that cannot be used shows why ----

        [Test]
        public void BlockedCardsAndRerollSayWhy()
        {
            var run = NewRun();
            run.Build.Traits.Add(new TraitSlot("heavy", 1));
            run.Build.Traits.Add(new TraitSlot("vampire", 1));
            run.Build.Traits.Add(new TraitSlot("spiky", 1));
            Offer(run, new Card(CardKind.Trait, "thorns", 1), new Card(CardKind.Heal), new Card(CardKind.SwapWeapon, run.Build.WeaponId));
            Assert.That(PrepareView.BlockReason(run, 0), Is.EqualTo("TRAITS FULL"));
            Assert.That(PrepareView.BlockReason(run, 1), Is.EqualTo("HP ALREADY FULL"));
            Assert.That(PrepareView.BlockReason(run, 2), Is.EqualTo("ALREADY YOUR WEAPON"));

            Offer(run, new Card(CardKind.DamageUp), new Card(CardKind.SpeedUp), new Card(CardKind.MaxHp));
            Assert.That(PrepareView.RerollBlockReason(run), Is.Null);
            run.Pick(0);
            Assert.That(PrepareView.BlockReason(run, 0), Is.Null, "taken is not 'blocked'");
            Assert.That(PrepareView.RerollBlockReason(run), Is.EqualTo("ONLY BEFORE FIRST PICK"));
            run.Coins = 0;
            Assert.That(PrepareView.BlockReason(run, 1), Is.EqualTo("NOT ENOUGH COINS"));
            run.Coins = 10;
            run.Pick(1);
            Assert.That(PrepareView.BlockReason(run, 2), Is.EqualTo("NO PICKS LEFT"));

            var fresh = NewRun();
            fresh.Coins = 0;
            Assert.That(PrepareView.RerollBlockReason(fresh), Is.EqualTo("NOT ENOUGH COINS"));
        }

        [Test]
        public void TappingABlockedCardShowsTheReasonInsteadOfNothing()
        {
            var run = NewRun();
            Offer(run, new Card(CardKind.Heal), new Card(CardKind.DamageUp), new Card(CardKind.SpeedUp));
            var v = NewPrepare(run);
            var picked = new List<int>();
            v.CardPicked += picked.Add;

            Assert.That(v.CardTag(0).Text, Is.EqualTo("HP ALREADY FULL"), "reason shown on the card before any tap");
            var before = v.CardTag(0).TextColor;
            v.PressCard(0);
            Assert.That(picked, Is.Empty);
            Assert.That(v.CardTag(0).TextColor, Is.Not.EqualTo(before), "the reason blinks when tapped");
            Assert.That(v.Card(0).BorderColor, Is.EqualTo((Color32)Palette.Hot));

            v.PressCard(1);
            Assert.That(picked, Is.EqualTo(new[] { 1 }));

            run.Pick(1);
            v.Show(run);
            Assert.That(v.RerollReason, Is.EqualTo("ONLY BEFORE FIRST PICK"));
            var rerolls = 0;
            v.RerollRequested += () => rerolls++;
            v.PressReroll();
            Assert.That(rerolls, Is.Zero);
        }

        // ---- 2. FIGHT with the free pick unused asks first ----

        [Test]
        public void FightWithAnUnusedFreePickAsksToConfirm()
        {
            var run = NewRun();
            Offer(run, new Card(CardKind.DamageUp), new Card(CardKind.SpeedUp), new Card(CardKind.MaxHp));
            var v = NewPrepare(run);
            var fights = 0;
            v.FightRequested += () => fights++;

            Assert.That(PrepareView.ShouldConfirmSkip(run), Is.True);
            v.PressFight();
            Assert.That(v.ConfirmOpen, Is.True);
            Assert.That(fights, Is.Zero);
            var picked = new List<int>();
            v.CardPicked += picked.Add;
            v.PressCard(0);
            Assert.That(picked, Is.Empty, "cards are behind the dialog");

            v.PressConfirm(false);
            Assert.That(v.ConfirmOpen, Is.False);
            Assert.That(fights, Is.Zero);

            v.PressFight();
            v.PressConfirm(true);
            Assert.That(fights, Is.EqualTo(1));

            run.Pick(0);
            v.Show(run);
            Assert.That(PrepareView.ShouldConfirmSkip(run), Is.False);
            v.PressFight();
            Assert.That(v.ConfirmOpen, Is.False);
            Assert.That(fights, Is.EqualTo(2), "free pick used: FIGHT starts at once");
        }

        [Test]
        public void NoConfirmWhenNoCardCanBeTaken()
        {
            var run = NewRun();
            Offer(run, new Card(CardKind.Heal), new Card(CardKind.Heal), new Card(CardKind.SwapWeapon, run.Build.WeaponId));
            Assert.That(PrepareView.ShouldConfirmSkip(run), Is.False);
        }

        // ---- 3. Hitboxes ----

        [Test]
        public void CardsNeverOverlapFightOrRerollAndAllButtonsAreOnScreen()
        {
            var buttons = new List<(string name, Rect r)>();
            for (var i = 0; i < RunTuning.OfferSize; i++) buttons.Add(("card" + i, PrepareView.CardArea(i)));
            buttons.Add(("reroll", PrepareView.RerollArea));
            buttons.Add(("fight", PrepareView.FightArea));
            var screen = new Rect(-HalfW, -HalfH, TextWrap.ScreenWidth, TextWrap.ScreenHeight);
            for (var a = 0; a < buttons.Count; a++)
            {
                Assert.That(Inside(screen, buttons[a].r), Is.True, $"{buttons[a].name} off screen");
                for (var b = a + 1; b < buttons.Count; b++)
                    Assert.That(buttons[a].r.Overlaps(buttons[b].r), Is.False, $"{buttons[a].name} overlaps {buttons[b].name}");
            }
            Assert.That(PrepareView.ConfirmYesArea.Overlaps(PrepareView.ConfirmNoArea), Is.False);
            Assert.That(Inside(PrepareView.ConfirmPanel, PrepareView.ConfirmYesArea) && Inside(PrepareView.ConfirmPanel, PrepareView.ConfirmNoArea), Is.True);

            // The built buttons use exactly these areas.
            var run = NewRun();
            var v = NewPrepare(run);
            for (var i = 0; i < RunTuning.OfferSize; i++) Assert.That(v.Card(i).Area, Is.EqualTo(PrepareView.CardArea(i)));
            var all = v.GetComponentsInChildren<PixelButton>(true);
            Assert.That(all.Any(b => b.Area == PrepareView.FightArea), Is.True);
            Assert.That(all.Any(b => b.Area == PrepareView.RerollArea), Is.True);
        }

        static bool Inside(Rect outer, Rect inner) => inner.xMin >= outer.xMin && inner.xMax <= outer.xMax && inner.yMin >= outer.yMin && inner.yMax <= outer.yMax;

        // ---- 4 / 11. Real HP, arena name and preview ----

        [Test]
        public void PrepareShowsTheHpTheFightReallyStartsWithBossIncluded()
        {
            var run = NewRun();
            run.Build.Traits.Add(new TraitSlot("heavy", 1));
            run.FightIndex = RunTuning.BossFights[0];
            var v = NewPrepare(run);

            var real = run.CreateMatch();   // what RunFlow.Fight() builds
            var you = real.Balls[0];
            var foe = real.Balls[1];
            Assert.That(you.MaxHp, Is.GreaterThan(run.Build.MaxHp), "Heavy adds HP at spawn");
            Assert.That(v.YouStats, Does.StartWith($"HP {Mathf.CeilToInt(you.Hp)}/{Mathf.CeilToInt(you.MaxHp)}"));
            Assert.That(v.EnemyStats, Is.EqualTo(PrepareView.EnemyHpText(foe.Hp, foe.MaxHp)));
            Assert.That(v.EnemyStats, Does.Contain(Mathf.CeilToInt(foe.MaxHp).ToString()));

            Assert.That(v.ArenaLabel, Is.EqualTo("ARENA " + ArenaRegistry.Get(run.ArenaId).DisplayName));
            Assert.That(v.ArenaPreviewPieces, Is.GreaterThanOrEqualTo(2), "floor + wall at least");
            Assert.That(v.EnemyIcon.bounds.size.x, Is.EqualTo(foe.Radius * 2f).Within(0.01f), "boss icon at its real size");
        }

        // ---- 10. Taken cards look different ----

        [Test]
        public void TakenCardHasItsOwnStyle()
        {
            var run = NewRun();
            Offer(run, new Card(CardKind.DamageUp), new Card(CardKind.DamageUp), new Card(CardKind.MaxHp));
            run.Pick(0);
            var v = NewPrepare(run);
            Assert.That(v.CardTag(0).Text, Is.EqualTo("TAKEN"));
            Assert.That(v.Card(0).FillColor, Is.Not.EqualTo(v.Card(1).FillColor));
            Assert.That(v.Card(0).BorderColor, Is.Not.EqualTo(v.Card(1).BorderColor));
            Assert.That(v.CardTag(1).Text, Is.Empty, "an available card has no tag");
        }

        // ---- 5 / 6 / 7 / 8. Fight view ----

        ArenaView NewArena(MatchSim sim, int marked)
        {
            var view = root.AddComponent<ArenaView>();
            view.Art = art;
            view.AutoRestart = false;
            view.MarkedBall = marked;
            view.StartMatch(sim, "YOU", "BOSS");
            return view;
        }

        static void Draw(ArenaView view, float dt) =>
            typeof(ArenaView).GetMethod("Draw", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(view, new object[] { 0f, dt });

        static MatchSim BossFight(string weapon = "blade")
        {
            var run = NewRun(weapon);
            run.FightIndex = RunTuning.BossFights[0];
            run.EnemyWeapons.Clear();
            run.EnemyWeapons.Add(weapon);   // same weapon both sides: the marker is the only difference
            return run.CreateMatch();
        }

        [Test]
        public void PlayerBallHasArrowAndRingTheEnemyDoesNot()
        {
            var sim = BossFight();
            Assert.That(sim.Balls[1].Weapon.Id, Is.EqualTo(sim.Balls[0].Weapon.Id));
            var view = NewArena(sim, 0);
            Draw(view, 0f);
            var you = view.GetBallView(0);
            var foe = view.GetBallView(1);
            Assert.That(you.IsMarked, Is.True);
            Assert.That(foe.IsMarked, Is.False);
            var top = you.transform.position.y + sim.Balls[0].Radius;
            Assert.That(you.MarkerArrowPosition.y, Is.GreaterThan(top), "arrow above the ball");
            Assert.That(you.MarkerArrowPosition.y, Is.LessThanOrEqualTo(top + 6f), "arrow close to the ball");
        }

        [Test]
        public void VersusHasNoMarker()
        {
            var sim = BossFight();
            var view = NewArena(sim, -1);
            Assert.That(view.GetBallView(0).IsMarked, Is.False);
            Assert.That(view.GetBallView(1).IsMarked, Is.False);
        }

        [Test]
        public void BossIsDrawnAtItsRealSizeAndFlashesOnlyItsOutline()
        {
            var sim = BossFight();
            Assert.That(sim.Balls[1].Radius, Is.EqualTo(RunTuning.BossRadius));
            var view = NewArena(sim, 0);
            Draw(view, 0f);
            var you = view.GetBallView(0);
            var boss = view.GetBallView(1);
            Assert.That(boss.GetComponent<SpriteRenderer>().bounds.size.x, Is.EqualTo(RunTuning.BossRadius * 2f).Within(0.01f));
            Assert.That(you.GetComponent<SpriteRenderer>().bounds.size.x, Is.EqualTo(sim.Balls[0].Radius * 2f).Within(0.01f));
            Assert.That(boss.Scale, Is.GreaterThan(you.Scale));

            boss.Flash(0.06f);
            you.Flash(0.06f);
            Draw(view, 0f);
            Assert.That(boss.UsesOutlineFlash, Is.True);
            Assert.That(boss.ActiveFlashRenderer.sprite, Is.SameAs(ProceduralSprites.BallOutline));
            Assert.That(boss.ActiveFlashRenderer.enabled, Is.True);
            Assert.That(boss.transform.Find("Flash").GetComponent<SpriteRenderer>().enabled, Is.False, "giant body does not flash whole");
            Assert.That(you.UsesOutlineFlash, Is.False);
            Assert.That(you.transform.Find("Flash").GetComponent<SpriteRenderer>().enabled, Is.True);
            Draw(view, 0.1f);
            Assert.That(boss.IsFlashing || boss.ActiveFlashRenderer.enabled, Is.False, "flash ends");
        }

        [Test]
        public void DamageNumbersDrawAboveEveryBallSprite()
        {
            var view = NewArena(BossFight(), 0);
            var ballOrders = new List<int>();
            for (var i = 0; i < 2; i++)
                ballOrders.AddRange(view.GetBallView(i).GetComponentsInChildren<SpriteRenderer>(true).Select(r => r.sortingOrder));
            Assert.That(DamagePopups.Order, Is.GreaterThan(ballOrders.Max()));
        }

        [Test]
        public void DamageNumbersStayFourPixelsInsideTheArena()
        {
            var arena = ArenaRect.Centered(230f, 230f);
            var points = new[] { new Vector2(-200, 0), new Vector2(200, 0), new Vector2(0, 200), new Vector2(0, -200), new Vector2(114, 114), new Vector2(-114, -114), Vector2.zero };
            foreach (var scale in new[] { 1, 2 })
            foreach (var text in new[] { "7", "12.5", "100" })
            foreach (var at in points)
            {
                var p = DamagePopups.ClampToArena(at, text, scale, arena);
                var w = PixelFontData.MeasureWidth(text) * scale;
                var left = p.x - w / 2;
                var right = left + w;
                var top = p.y + DamagePopups.Rise + PixelFontData.GlyphHeight * scale;
                Assert.That(left, Is.GreaterThanOrEqualTo(arena.Left + DamagePopups.WallMargin), $"{text} x{scale} at {at}");
                Assert.That(right, Is.LessThanOrEqualTo(arena.Right - DamagePopups.WallMargin), $"{text} x{scale} at {at}");
                Assert.That(p.y, Is.GreaterThanOrEqualTo(arena.Bottom + DamagePopups.WallMargin), $"{text} x{scale} at {at}");
                Assert.That(top, Is.LessThanOrEqualTo(arena.Top - DamagePopups.WallMargin), $"{text} x{scale} at {at}");
                Assert.That(p.x, Is.EqualTo(Mathf.Round(p.x)));
                Assert.That(p.y, Is.EqualTo(Mathf.Round(p.y)));
            }
            Assert.That(DamagePopups.ClampToArena(new Vector2(10, -20), "7", 1, arena), Is.EqualTo(new Vector2(10, -20)), "inside: unchanged");
        }

        // ---- 9. Text fits the 270x480 screen ----

        [Test]
        public void SummaryWithEveryUnlockFitsTheScreen()
        {
            var run = NewRun();
            run.Won = true;
            run.Achievements.AddRange(new[] { UnlockRules.Boss1, UnlockRules.Boss2, UnlockRules.Boss3, UnlockRules.Win1, UnlockRules.Win3 });
            var v = RunSummaryView.Create(root.transform, art);
            v.Show(run);
            AssertAllTextOnScreen(v.transform);

            var names = new List<string>();
            foreach (var a in run.Achievements) names.AddRange(UnlockRules.UnlockedNames(a));
            var lines = RunSummaryView.UnlockText(names);
            Assert.That(lines.Count, Is.InRange(1, RunSummaryView.UnlockLines));
            var joined = string.Join(" ", lines);
            foreach (var n in names) Assert.That(joined, Does.Contain(n), "every unlocked name is listed");
        }

        [Test]
        public void UnlockTextNeverCutsAndSaysHowManyMore()
        {
            var many = Enumerable.Range(0, 40).Select(i => "LONG NAME " + i).ToList();
            var lines = RunSummaryView.UnlockText(many);
            Assert.That(lines.Count, Is.LessThanOrEqualTo(RunSummaryView.UnlockLines));
            foreach (var l in lines) Assert.That(l.Length, Is.LessThanOrEqualTo(TextWrap.MaxCharsOnScreen(1)));
            Assert.That(lines[lines.Count - 1], Does.EndWith("MORE"));
        }

        [Test]
        public void RunScreensFitTheScreen()
        {
            var title = RunTitleView.Create(root.transform, art);
            var store = new MemorySaveStore();
            var flow = new RunFlow(store);
            flow.Open();
            flow.Progress.BestFight = 8;
            flow.Progress.RunsWon = 999;
            flow.Progress.RunsPlayed = 9999;
            title.Show(flow);
            AssertAllTextOnScreen(title.transform);
            title.Hide();

            var run = NewRun();
            run.FightIndex = RunTuning.BossFights[2];
            run.Build.Traits.Add(new TraitSlot("glass-cannon", 2));
            run.Build.Traits.Add(new TraitSlot("parry-master", 2));
            run.Build.Traits.Add(new TraitSlot("second-wind", 2));
            run.Build.DamagePct = 0.45f;
            run.Build.SpeedPct = 0.3f;
            var prepare = NewPrepare(run);
            AssertAllTextOnScreen(prepare.transform);
            prepare.PressFight();
            AssertAllTextOnScreen(prepare.transform);
        }

        static void AssertAllTextOnScreen(Transform screen)
        {
            var count = 0;
            foreach (var t in screen.GetComponentsInChildren<PixelText>(false))
            {
                if (string.IsNullOrEmpty(t.Text.Trim())) continue;
                count++;
                var b = t.WorldBounds;
                Assert.That(b.xMin, Is.GreaterThanOrEqualTo(-HalfW), $"'{t.Text}' cut on the left");
                Assert.That(b.xMax, Is.LessThanOrEqualTo(HalfW), $"'{t.Text}' cut on the right");
                Assert.That(b.yMin, Is.GreaterThanOrEqualTo(-HalfH), $"'{t.Text}' cut at the bottom");
                Assert.That(b.yMax, Is.LessThanOrEqualTo(HalfH), $"'{t.Text}' cut at the top");
            }
            Assert.That(count, Is.GreaterThan(0), "nothing drawn");
        }
    }
}
