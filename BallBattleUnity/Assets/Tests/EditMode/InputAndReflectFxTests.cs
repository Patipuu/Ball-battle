using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BallBattle.Sim;
using BallBattle.Sim.Run;
using BallBattle.Sim.Traits;
using BallBattle.Sim.Weapons;
using BallBattle.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BallBattle.Tests
{
    /// <summary>
    /// 12: no click-through into Prepare (entry block, press and release on the same button, press after the screen opened).
    /// 13: Versus taps are applied in the order they happened (BLADE then START in one frame).
    /// 14: reflect damage FX (1 px edge on the ball hit back, 2x number, stacked numbers, thorn spikes).
    /// 15: -bbForceTrait debug offer.
    /// </summary>
    public class InputAndReflectFxTests
    {
        GameObject root;
        ArtLibrary art;

        [SetUp]
        public void SetUp()
        {
            art = AssetDatabase.LoadAssetAtPath<ArtLibrary>("Assets/Art/ArtLibrary.asset");
            Assert.That(art, Is.Not.Null, "run BallBattle/Build Scenes");
            root = new GameObject("InputTestRoot");
            PixelInput.SetTestClock(0f);
        }

        [TearDown]
        public void TearDown()
        {
            PixelInput.SetTestClock(null);
            Object.DestroyImmediate(root);
        }

        // ---- helpers ----

        void Dispatch() => PixelInput.Dispatch(() => root.GetComponentsInChildren<PixelButton>(false));

        void Tap(Vector2 at)
        {
            PixelInput.InjectTap(at);
            Dispatch();
        }

        void Press(Vector2 at) { PixelInput.Inject(true, at); Dispatch(); }
        void Release(Vector2 at) { PixelInput.Inject(false, at); Dispatch(); }
        static void Wait(float s) => PixelInput.AdvanceTestClock(s);

        static RunState NewRun()
        {
            var run = RunState.Start(77, "blade", new ProgressData());
            run.Coins = 10;
            run.OfferCards.Clear();
            foreach (var c in new[] { new Card(CardKind.DamageUp), new Card(CardKind.SpeedUp), new Card(CardKind.MaxHp) })
                run.OfferCards.Add(OfferSlot.From(c));
            return run;
        }

        PrepareView Prepare(RunState run, List<int> picks, List<string> other)
        {
            var v = PrepareView.Create(root.transform, art);
            v.CardPicked += picks.Add;
            v.FightRequested += () => other.Add("fight");
            v.RerollRequested += () => other.Add("reroll");
            v.Show(run);
            return v;
        }

        static Vector2 Center(Rect r) => r.center;

        // ---- 12. Click-through into Prepare ----

        [Test]
        public void PrepareDropsEveryTapForThreeHundredMillisecondsAfterOpening()
        {
            var picks = new List<int>();
            var other = new List<string>();
            Prepare(NewRun(), picks, other);

            Wait(0.1f);
            Tap(Center(PrepareView.CardArea(0)));
            Tap(Center(PrepareView.FightArea));
            Tap(Center(PrepareView.RerollArea));
            Wait(0.15f);
            Tap(Center(PrepareView.CardArea(1)));
            Assert.That(picks, Is.Empty, "taps in the first 300 ms do nothing");
            Assert.That(other, Is.Empty);

            Wait(0.1f);   // 0.35 s after opening
            Tap(Center(PrepareView.CardArea(0)));
            Assert.That(picks, Is.EqualTo(new[] { 0 }));
        }

        [Test]
        public void APressFromBeforeTheScreenOpenedNeverPicksACard()
        {
            var picks = new List<int>();
            var other = new List<string>();
            var run = NewRun();
            var v = Prepare(run, picks, other);
            v.Hide();

            Press(Center(PrepareView.CardArea(0)));   // the click that dismissed "LOST -1 LIFE": pressed while Prepare is hidden
            Wait(0.5f);
            v.Show(run);                               // Prepare opens while the button is still down
            Wait(0.5f);
            Release(Center(PrepareView.CardArea(0)));  // released on a card after the entry block
            Assert.That(picks, Is.Empty, "press before the screen opened does not count");

            Tap(Center(PrepareView.CardArea(0)));
            Assert.That(picks, Is.EqualTo(new[] { 0 }), "a fresh tap works");
        }

        [Test]
        public void PressAndReleaseMustBeOnTheSameCardOrButton()
        {
            var picks = new List<int>();
            var other = new List<string>();
            var run = NewRun();
            run.Pick(0);   // free pick used: FIGHT does not ask, REROLL is locked, so test REROLL on a fresh run below
            Prepare(run, picks, other);
            Wait(0.4f);

            Press(Center(PrepareView.CardArea(1)));
            Release(Center(PrepareView.CardArea(2)));
            Assert.That(picks, Is.Empty, "press on card 1, release on card 2");

            Press(Center(PrepareView.FightArea));
            Release(Center(PrepareView.CardArea(2)));
            Press(Center(PrepareView.CardArea(2)));
            Release(Center(PrepareView.FightArea));
            Assert.That(picks, Is.Empty);
            Assert.That(other, Is.Empty, "slid on or off FIGHT: nothing");

            Press(Center(PrepareView.CardArea(1)));
            Release(Center(PrepareView.CardArea(1)) + new Vector2(30, 5));
            Assert.That(picks, Is.EqualTo(new[] { 1 }), "same card: picked");

            Tap(Center(PrepareView.FightArea));
            Assert.That(other, Is.EqualTo(new[] { "fight" }));
        }

        [Test]
        public void RerollNeedsPressAndReleaseOnIt()
        {
            var picks = new List<int>();
            var other = new List<string>();
            Prepare(NewRun(), picks, other);
            Wait(0.4f);
            Press(Center(PrepareView.RerollArea));
            Release(Center(PrepareView.FightArea));
            Assert.That(other, Is.Empty);
            Tap(Center(PrepareView.RerollArea));
            Assert.That(other, Is.EqualTo(new[] { "reroll" }));
        }

        [Test]
        public void TheTapOnYesNeverAlsoPicksTheCardUnderIt()
        {
            var picks = new List<int>();
            var other = new List<string>();
            var v = Prepare(NewRun(), picks, other);
            Wait(0.4f);
            Tap(Center(PrepareView.FightArea));
            Assert.That(v.ConfirmOpen, Is.True, "free pick unused: asks first");

            var yesOverCard = new Vector2(PrepareView.ConfirmYesArea.center.x, -40f);
            Assert.That(PrepareView.ConfirmYesArea.Contains(yesOverCard) && PrepareView.CardArea(1).Contains(yesOverCard), Is.True);
            Tap(yesOverCard);
            Assert.That(other, Is.EqualTo(new[] { "fight" }));
            Assert.That(picks, Is.Empty, "the card under YES got nothing");
        }

        [Test]
        public void AButtonIgnoresAPressFromBeforeItWasShown()
        {
            var b = PixelButton.Create(root.transform, "B", art, new Rect(0, 0, 20, 20), "B", 1, Palette.Floor, Palette.Wall, Palette.Text, 0);
            var clicks = 0;
            b.Clicked += () => clicks++;
            PixelInput.Inject(true, new Vector2(5, 5));
            b.NoteShown();
            PixelInput.Inject(false, new Vector2(5, 5));
            Dispatch();
            Assert.That(clicks, Is.Zero);
            Tap(new Vector2(5, 5));
            Assert.That(clicks, Is.EqualTo(1));
        }

        // ---- 13. Versus: taps in order ----

        [Test]
        public void VersusPickThenStartInOneFrameStartsWithThePickedWeapon()
        {
            var menu = MenuView.Create(root.transform, art);
            menu.Show(true);
            var started = new List<(string a, string b)>();
            menu.StartRequested += (a, b) => started.Add((a, b));
            Assert.That(menu.Selected(1), Is.Not.EqualTo(BladeRule.WeaponId));

            // Both taps arrive before the next frame (fast tap / automation): they must apply in that order.
            PixelInput.InjectTap(menu.Tile(1, BladeRule.WeaponId).Area.center);
            PixelInput.InjectTap(menu.StartButton.Area.center);
            Dispatch();

            Assert.That(menu.Selected(1), Is.EqualTo(BladeRule.WeaponId));
            Assert.That(started, Is.EqualTo(new[] { (menu.Selected(0), BladeRule.WeaponId) }));
        }

        [Test]
        public void EveryVersusTileIsReachableAndOnlyItself()
        {
            var menu = MenuView.Create(root.transform, art);
            menu.Show(true);
            foreach (var side in new[] { 0, 1 })
                foreach (var w in WeaponRegistry.All)
                {
                    var tile = menu.Tile(side, w.Id);
                    Assert.That(Mathf.Abs(tile.Area.xMin) <= 135f && tile.Area.xMax <= 135f, Is.True, $"{w.Id} tile on screen");
                    Tap(tile.Area.center);
                    Assert.That(menu.Selected(side), Is.EqualTo(w.Id), $"side {side} tap {w.Id}");
                    var hits = root.GetComponentsInChildren<PixelButton>(false).Count(b => b.Area.Contains(tile.Area.center));
                    Assert.That(hits, Is.EqualTo(1), $"{w.Id} tile overlaps another button");
                }
        }

        // ---- 14. Reflect FX ----

        (ArenaView view, FxView fx, MatchSim sim) ThornsFight()
        {
            var a = new BallLoadout(WeaponRegistry.Create(BladeRule.WeaponId)).With(TraitRegistry.Create(ThornsTrait.TraitId, 1));
            var b = new BallLoadout(WeaponRegistry.Create(FangRule.WeaponId));
            var sim = new MatchSim(ArenaRegistry.Create(ArenaRegistry.Classic), 5, new[] { a, b });
            sim.ApplySpawnHooks();
            var view = root.AddComponent<ArenaView>();
            view.Art = art;
            view.AutoRestart = false;
            view.StartMatch(sim);
            var fx = root.AddComponent<FxView>();
            typeof(FxView).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(fx, null);
            return (view, fx, sim);
        }

        static void Feed(FxView fx, params SimEvent[] events)
        {
            var on = typeof(FxView).GetMethod("OnSimEvent", BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var e in events) on.Invoke(fx, new object[] { e });
            typeof(FxView).GetMethod("FlushReflects", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(fx, null);
        }

        static void Draw(ArenaView view, float dt) =>
            typeof(ArenaView).GetMethod("Draw", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(view, new object[] { 0f, dt });

        [Test]
        public void ReflectFlashesOnlyTheOuterPixelOfTheBallHitBack()
        {
            var (view, fx, sim) = ThornsFight();
            var hitPoint = sim.Balls[0].Pos;
            Feed(fx,
                new SimEvent { Type = SimEventType.Hit, Tick = 9, A = 1, B = 0, Value = 6f, Point = hitPoint },
                new SimEvent { Type = SimEventType.Damage, Tick = 9, A = 0, B = 1, Value = 0.9f, Point = sim.Balls[1].Pos, Variant = (byte)DamageKind.Reflect });
            Draw(view, 0f);

            var hurt = view.GetBallView(1);   // d.B: the attacker the thorns hit back
            var thorny = view.GetBallView(0);
            Assert.That(hurt.IsEdgeFlashing, Is.True);
            Assert.That(hurt.EdgeFlashRenderer.enabled, Is.True);
            Assert.That(hurt.EdgeFlashRenderer.sprite, Is.SameAs(ProceduralSprites.BallOutlineThin));
            Assert.That((Color32)hurt.EdgeFlashRenderer.color, Is.EqualTo(FxView.ReflectColor));
            Assert.That(hurt.FullFlashRenderer.enabled, Is.False, "body keeps its weapon color");
            Assert.That(thorny.IsEdgeFlashing, Is.False, "the reflecting ball does not show the reflect flash");

            Draw(view, FxView.ReflectFlashSeconds * 0.5f);
            Assert.That(hurt.EdgeFlashRenderer.enabled, Is.True, "still on at 0.06 s");
            Draw(view, FxView.ReflectFlashSeconds);
            Assert.That(hurt.EdgeFlashRenderer.enabled, Is.False, "off after 0.12 s");

            var reflectNumbers = fx.Popups.Showing().Where(p => p.value == "0.9").ToList();
            Assert.That(reflectNumbers.Count, Is.EqualTo(1));
            Assert.That(reflectNumbers[0].scale, Is.EqualTo(2), "reflect numbers are 2x");
        }

        [Test]
        public void EdgeSpriteIsExactlyOnePixelThick()
        {
            var tex = ProceduralSprites.BallOutlineThin.texture;
            var row = Enumerable.Range(0, tex.width).Select(x => tex.GetPixel(x, tex.height / 2).a > 0.5f).ToArray();
            Assert.That(row.Count(on => on), Is.EqualTo(2), "a horizontal line through the centre crosses the ring twice, 1 px each");
            Assert.That(tex.GetPixel(tex.width / 2, tex.height / 2).a, Is.Zero, "centre empty");
        }

        [Test]
        public void NumbersShownTogetherStackAndStayInTheArena()
        {
            var popups = DamagePopups.Create(root.transform, art.Font, 8);
            var arena = ArenaRect.Centered(230f, 230f);
            var at = new Vector2(0f, 0f);
            popups.Show(at, 1.4f, Palette.Text, arena);
            popups.Tick(0.05f);
            popups.Show(at, 3f, FxView.ReflectColor, arena, 2);
            popups.Tick(0.05f);
            popups.Show(at, 2f, Palette.Text, arena);
            var shown = popups.Showing().OrderBy(p => p.start.y).ToList();
            Assert.That(shown.Count, Is.EqualTo(3));
            for (var i = 1; i < shown.Count; i++) Assert.That(shown[i].start.y - shown[i - 1].start.y, Is.GreaterThanOrEqualTo(DamagePopups.StackStep));
            AssertNoOverlap(shown);

            popups.Clear();
            popups.Show(new Vector2(60, 0), 5f, Palette.Text, arena);
            Assert.That(popups.Showing().Any(p => p.value == "5" && p.start == new Vector2(60, 0)), Is.True, "alone: not moved");

            // Against the top wall: stacked below instead, still inside.
            var top = new Vector2(0f, arena.Top);
            popups.Clear();
            for (var i = 0; i < 3; i++) { popups.Show(top, 1.4f + i, Palette.Text, arena); popups.Tick(0.02f); }
            var high = popups.Showing();
            AssertNoOverlap(high);
            foreach (var p in high)
            {
                Assert.That(p.start.y + DamagePopups.Rise + PixelFontData.GlyphHeight * p.scale, Is.LessThanOrEqualTo(arena.Top - DamagePopups.WallMargin));
                Assert.That(p.start.y, Is.GreaterThanOrEqualTo(arena.Bottom + DamagePopups.WallMargin));
            }
        }
        static void AssertNoOverlap(List<(string value, int scale, Vector2 start)> shown)
        {
            for (var i = 0; i < shown.Count; i++)
                for (var j = i + 1; j < shown.Count; j++)
                {
                    var a = DamagePopups.StartRect(shown[i].start, shown[i].value, shown[i].scale);
                    var b = DamagePopups.StartRect(shown[j].start, shown[j].value, shown[j].scale);
                    Assert.That(a.Overlaps(b), Is.False, $"'{shown[i].value}' and '{shown[j].value}' overlap");
                }
        }

        [Test]
        public void ThornSpikesAreTwoPixelsThickAndFiveLong()
        {
            foreach (var dir in new[] { Vector2.right, Vector2.up, new Vector2(1, 1), new Vector2(-3, 1) })
            {
                var offsets = FxView.ThornSpikeOffsets(dir);
                var length = (offsets[0] - offsets[offsets.Length - 1]).magnitude + FxView.ThornSpikeThickness;
                Assert.That(length, Is.EqualTo(5f).Within(0.001f));
                Assert.That(offsets[0], Is.EqualTo(Vector2.zero), "tip first");
                Assert.That(Vector2.Dot(offsets[1], dir), Is.LessThan(0f), "tail behind the tip, pointing from A to B");
            }
            Assert.That(FxView.ThornSpikeThickness, Is.EqualTo(2));
        }

        // ---- 15. -bbForceTrait ----

        [Test]
        public void ForceTraitArgumentIsParsedAndOffByDefault()
        {
            Assert.That(DebugOptions.ParseForceTrait(new[] { "game.exe" }), Is.Null);
            Assert.That(DebugOptions.ParseForceTrait(new[] { "game.exe", "-bbForceTrait", "secondwind" }), Is.EqualTo(SecondWindTrait.TraitId));
            Assert.That(DebugOptions.ParseForceTrait(new[] { "-bbforcetrait", "SECOND_WIND" }), Is.EqualTo(SecondWindTrait.TraitId));
            Assert.That(DebugOptions.ParseForceTrait(new[] { "-bbForceTrait", "not-a-trait" }), Is.Null);
            Assert.That(DebugOptions.ParseForceTrait(new[] { "-bbForceTrait" }), Is.Null);
        }

        [Test]
        public void ForcedTraitIsInTheFirstOfferOnlyAndNormalPlayIsUntouched()
        {
            uint seed = 0;
            for (uint s = 1; s < 200 && seed == 0; s++)
            {
                var weapon = RunState.StartChoices(s, UnlockRules.Weapons(new ProgressData()))[0];
                var probe = RunState.Start(s, weapon, new ProgressData());
                var has = Enumerable.Range(0, probe.OfferCount).Any(i => probe.OfferCard(i).Id == SecondWindTrait.TraitId);
                if (!has) seed = s;
            }
            Assert.That(seed, Is.Not.Zero);

            var plain = new RunFlow(new MemorySaveStore());
            plain.Open();
            plain.NewRun(seed);
            plain.ChooseWeapon(plain.StartChoices[0]);
            var reference = RunState.Start(seed, plain.StartChoices[0], new ProgressData());
            Assert.That(Enumerable.Range(0, plain.Run.OfferCount).Select(plain.Run.OfferCard),
                        Is.EqualTo(Enumerable.Range(0, reference.OfferCount).Select(reference.OfferCard)), "no flag: offer unchanged");

            var debug = new RunFlow(new MemorySaveStore()) { ForceTraitId = SecondWindTrait.TraitId };
            debug.Open();
            debug.NewRun(seed);
            debug.ChooseWeapon(debug.StartChoices[0]);
            Assert.That(debug.Run.FightIndex, Is.Zero);
            Assert.That(Enumerable.Range(0, debug.Run.OfferCount).Count(i => debug.Run.OfferCard(i).Equals(new Card(CardKind.Trait, SecondWindTrait.TraitId, 1))), Is.EqualTo(1));
            Assert.That(debug.Run.OfferCount, Is.EqualTo(RunTuning.OfferSize), "no extra card screen, same 3 cards");

            debug.Run.Coins = 5;
            debug.Reroll();
            Assert.That(Enumerable.Range(0, debug.Run.OfferCount).Any(i => debug.Run.OfferCard(i).Id == SecondWindTrait.TraitId), Is.True, "still there after a reroll");

            var later = debug.Run;
            later.FightIndex = 1;
            later.OfferCards.Clear();
            later.OfferCards.Add(OfferSlot.From(new Card(CardKind.Heal)));
            Assert.That(DebugOptions.ApplyForcedTrait(later, SecondWindTrait.TraitId), Is.False, "only the offer before fight 1");
        }
    }
}
