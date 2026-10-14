using System.Collections.Generic;
using System.IO;
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
    /// 1 outline flash from x1.3 (Heavy LV2), 2 player marker per fix_marker_flash_v1.png, 3 swappable edge source,
    /// 4 Second Wind across a run, 5 Prepare HP = fight HP, 6 damage numbers never closer than 12 px, 7 Versus result.
    /// </summary>
    public class MarkerSecondWindAndPopupTests
    {
        GameObject root;
        ArtLibrary art;

        [SetUp]
        public void SetUp()
        {
            art = AssetDatabase.LoadAssetAtPath<ArtLibrary>("Assets/Art/ArtLibrary.asset");
            Assert.That(art, Is.Not.Null, "run BallBattle/Build Scenes");
            root = new GameObject("Prompt8Root");
        }

        [TearDown]
        public void TearDown()
        {
            PixelInput.SetTestClock(null);
            BallEdges.Source = BallEdges.Default();
            Object.DestroyImmediate(root);
        }

        static void Draw(ArenaView view, float dt) =>
            typeof(ArenaView).GetMethod("Draw", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(view, new object[] { 0f, dt });

        ArenaView Arena(MatchSim sim, int marked, string tagA = null, string tagB = null)
        {
            var view = root.AddComponent<ArenaView>();
            view.Art = art;
            view.AutoRestart = false;
            view.MarkedBall = marked;
            view.StartMatch(sim, tagA, tagB);
            Draw(view, 0f);
            return view;
        }

        static MatchSim Sim(BallLoadout a, BallLoadout b)
        {
            var sim = new MatchSim(ArenaRegistry.Create(ArenaRegistry.Classic), 4, new[] { a, b });
            sim.ApplySpawnHooks();
            return sim;
        }

        // ---- 1. Outline-only flash from x1.3 ----

        [Test]
        public void HeavyLevelTwoFlashesOnlyItsOutline()
        {
            var sim = Sim(new BallLoadout(WeaponRegistry.Create(BladeRule.WeaponId)).With(TraitRegistry.Create(HeavyTrait.TraitId, 2)),
                          new BallLoadout(WeaponRegistry.Create(FangRule.WeaponId)).With(TraitRegistry.Create(HeavyTrait.TraitId, 1)));
            var view = Arena(sim, -1);
            var heavy2 = view.GetBallView(0);
            var heavy1 = view.GetBallView(1);
            Assert.That(heavy2.NativeRadius * heavy2.Scale, Is.EqualTo(16f * 1.35f).Within(0.01f), "x1.35 on screen (pre-drawn _43 sprite at scale 1 since 3d)");
            Assert.That(BallView.OutlineFlashScale, Is.EqualTo(1.3f));
            heavy2.Flash(0.06f);
            heavy1.Flash(0.06f);
            Draw(view, 0f);
            Assert.That(heavy2.UsesOutlineFlash, Is.True);
            Assert.That(heavy2.FullFlashRenderer.enabled, Is.False, "Heavy LV2 body keeps its weapon color");
            Assert.That(heavy2.ActiveFlashRenderer.enabled, Is.True);
            Assert.That(heavy1.UsesOutlineFlash, Is.False, "Heavy LV1 (x1.2) still flashes whole");
            Assert.That(heavy1.FullFlashRenderer.enabled, Is.True);
        }

        // ---- 2. Player marker ----

        [Test]
        public void ArrowSpriteIsTheMockupArrow()
        {
            var tex = ProceduralSprites.DownArrow.texture;
            var rows = ProceduralSprites.ArrowRows;
            Assert.That(tex.width, Is.EqualTo(11));
            Assert.That(tex.height, Is.EqualTo(8));
            int minX = 99, maxX = -1, minY = 99, maxY = -1;
            for (var r = 0; r < rows.Length; r++)
                for (var x = 0; x < rows[r].Length; x++)
                {
                    var c = (Color32)tex.GetPixel(x, rows.Length - 1 - r);
                    switch (rows[r][x])
                    {
                        case 'W':
                            Assert.That(c, Is.EqualTo(new Color32(255, 255, 255, 255)));
                            minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, r); maxY = Mathf.Max(maxY, r);
                            break;
                        case 'o': Assert.That(c, Is.EqualTo(new Color32(20, 18, 28, 255))); break;
                        default: Assert.That(c.a, Is.Zero); break;
                    }
                }
            Assert.That(maxX - minX + 1, Is.EqualTo(9), "white part 9 px wide");
            Assert.That(maxY - minY + 1, Is.EqualTo(6), "white part 6 px tall");
            Assert.That(rows[maxY].IndexOf('W'), Is.EqualTo(ProceduralSprites.ArrowTipColumn), "tip centred");
        }

        [Test]
        public void MockupHasThisArrowFivePixelRowsAboveTheBall()
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../docs/design/art/fix_marker_flash_v1.png"));
            if (!File.Exists(path)) Assert.Ignore("mockup not found: " + path);
            var png = new Texture2D(2, 2);
            Assert.That(png.LoadImage(File.ReadAllBytes(path)), Is.True);
            const int k = 3;   // the mockup is drawn at 3x
            int w = png.width / k, h = png.height / k;
            var all = png.GetPixels32();
            Color32 At(int x, int yFromTop) => all[(png.height - 1 - (yFromTop * k + 1)) * png.width + x * k + 1];
            var rows = ProceduralSprites.ArrowRows;
            var found = new List<(int x, int y)>();
            for (var y = 0; y + rows.Length <= h; y++)
                for (var x = 0; x + rows[0].Length <= w; x++)
                {
                    var ok = true;
                    for (var r = 0; r < rows.Length && ok; r++)
                        for (var c = 0; c < rows[r].Length && ok; c++)
                        {
                            var ch = rows[r][c];
                            if (ch == '.') continue;
                            var px = At(x + c, y + r);
                            ok = ch == 'W' ? px.Equals(new Color32(255, 255, 255, 255)) : px.Equals(new Color32(20, 18, 28, 255));
                        }
                    if (ok) found.Add((x, y));
                }
            Assert.That(found.Count, Is.GreaterThanOrEqualTo(1), "the mockup contains exactly this arrow");
            // Over the ball: below the tip, past the ring, the first outline pixel is the ball's top row.
            var hits = 0;
            foreach (var (x, y) in found)
            {
                var tipX = x + ProceduralSprites.ArrowTipColumn;
                var tipRow = y + rows.Length - 1 - ProceduralSprites.ArrowRowsBelowTip;
                for (var r = tipRow + 2; r < Mathf.Min(h, tipRow + 12); r++)
                {
                    if (!At(tipX, r).Equals(new Color32(20, 18, 28, 255))) continue;
                    Assert.That(r - tipRow, Is.EqualTo(5), "tip 5 rows above the ball's top row");
                    hits++;
                    break;
                }
            }
            Assert.That(hits, Is.GreaterThanOrEqualTo(1), "one arrow sits over a ball");
        }

        [Test]
        public void ArrowTipIsFiveRowsAboveTheBallTopRowAndCentred()
        {
            foreach (var heavy in new[] { 0, 2 })
            {
                var a = new BallLoadout(WeaponRegistry.Create(BladeRule.WeaponId));
                if (heavy > 0) a = a.With(TraitRegistry.Create(HeavyTrait.TraitId, heavy));
                var sim = Sim(a, new BallLoadout(WeaponRegistry.Create(BladeRule.WeaponId)));
                var view = Arena(sim, 0, "YOU", "FOE");
                var you = view.GetBallView(0);
                Assert.That(you.IsMarked, Is.True);
                Assert.That(view.GetBallView(1).IsMarked, Is.False);
                // Topmost drawn row of the ball disc, read from the disc itself (independent of BallView's formula).
                var sprite = you.GetComponent<SpriteRenderer>().sprite;
                var disc = sprite.texture.isReadable ? sprite.texture : ProceduralSprites.BallOutline.texture;   // the ball's own pixels (real art is readable)
                var topRow = Enumerable.Range(0, disc.height).Where(y => Enumerable.Range(0, disc.width).Any(x => disc.GetPixel(x, y).a > 0.5f)).Max();
                var visibleTop = Mathf.RoundToInt((topRow + 1 - disc.height / 2f) * you.Scale);
                var centre = you.transform.position;
                var ballTopRowBottom = centre.y + visibleTop - 1f;
                var tipWhiteBottom = you.MarkerArrowPosition.y + ProceduralSprites.ArrowRowsBelowTip;
                Assert.That(tipWhiteBottom - ballTopRowBottom, Is.EqualTo(5f).Within(0.001f), $"heavy {heavy}");
                Assert.That(you.MarkerArrowPosition.x, Is.EqualTo(centre.x).Within(0.001f), "tip column on the ball centre");
                Assert.That(you.MarkerArrowRenderer.bounds.size.x, Is.EqualTo(11f).Within(0.001f), "arrow drawn at native size, not scaled with the ball");
                Assert.That(you.MarkerRingRenderer.sprite, Is.SameAs(ProceduralSprites.DashedRing(you.VisibleRadius)));
                Object.DestroyImmediate(view);
                foreach (Transform child in root.transform) Object.DestroyImmediate(child.gameObject);
            }
        }

        [Test]
        public void RingIsOnePixelDashedSixDegreesAndNeverRotates()
        {
            const float r = ProceduralSprites.BallDiscRadius;   // the drawn disc of a default ball
            var tex = ProceduralSprites.DashedRing(r).texture;
            var c = tex.width / 2f;
            var on = 0;
            for (var y = 0; y < tex.height; y++)
                for (var x = 0; x < tex.width; x++)
                {
                    if (tex.GetPixel(x, y).a < 0.5f) continue;
                    on++;
                    var d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c));
                    Assert.That(d, Is.InRange(r + 2f, r + 3f), "1 px band, 3 px out from the edge pixels (2 px gap)");
                }
            Assert.That(on, Is.GreaterThan(40));
            for (var deg = 0; deg < 360; deg++)
            {
                if (deg % 6 == 0 || deg % 6 == 5) continue;   // skip dash edges
                var a = deg * Mathf.Deg2Rad;
                var x = Mathf.FloorToInt(c + Mathf.Cos(a) * (r + 2.5f));
                var y = Mathf.FloorToInt(c + Mathf.Sin(a) * (r + 2.5f));
                var dc = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c));
                if (dc < r + 2f || dc >= r + 3f) continue;   // sampled pixel outside the 1 px band
                var drawn = tex.GetPixel(x, y).a > 0.5f;
                if (ProceduralSprites.IsDash(x + 0.5f - c, y + 0.5f - c) != ((deg / 6) % 2 == 0)) continue;   // pixel straddles a slice
                Assert.That(drawn, Is.EqualTo((deg / 6) % 2 == 0), $"angle {deg}");
            }

            var sim = Sim(new BallLoadout(WeaponRegistry.Create(FangRule.WeaponId)), new BallLoadout(WeaponRegistry.Create(BladeRule.WeaponId)));
            var view = Arena(sim, 0, "YOU", "FOE");
            for (var i = 0; i < 120; i++) { sim.Step(); Draw(view, 0.016f); }
            Assert.That(view.GetBallView(0).MarkerRingRenderer.transform.rotation, Is.EqualTo(Quaternion.identity));
        }

        [Test]
        public void HudShowsTheArrowBeforeYouOnlyInRunMode()
        {
            var sim = Sim(new BallLoadout(WeaponRegistry.Create(BladeRule.WeaponId)), new BallLoadout(WeaponRegistry.Create(BladeRule.WeaponId)));
            var view = Arena(sim, 0, HudView.PlayerTag, "BOSS");
            var hud = view.Hud;
            Assert.That(hud.ShowsPlayerArrow(0), Is.True);
            Assert.That(hud.ShowsPlayerArrow(1), Is.False, "the enemy has no mark");
            var arrow = hud.PlayerArrow(0).bounds;
            var name = hud.NameText(0).WorldBounds;
            Assert.That(name.xMin - arrow.max.x, Is.InRange(1f, 3f), "arrow just before YOU");
            Assert.That(arrow.min.y >= name.yMin && arrow.max.y <= name.yMax, Is.True, "inside the name line");
            Assert.That(hud.PlayerArrow(0).sprite, Is.SameAs(ProceduralSprites.DownArrow), "same arrow as over the ball");

            // Versus binds the HUD without tags (GameController -> ArenaView.StartMatch(a, b, seed)).
            hud.Bind(0, BladeRule.WeaponId);
            hud.Bind(1, FangRule.WeaponId);
            Assert.That(hud.ShowsPlayerArrow(0) || hud.ShowsPlayerArrow(1), Is.False, "Versus: no arrow");
            Assert.That(hud.NameText(0).WorldBounds.xMin, Is.EqualTo(name.xMin - 13f).Within(0.01f), "name back in place");
        }

        // ---- 3. Swappable edge source ----

        sealed class FixedSource : IBallEdgeSource
        {
            public Sprite Sprite;
            public Sprite Edge(Sprite ball, int thickness) => Sprite;
        }

        [Test]
        public void FlashUsesTheConfiguredEdgeSource()
        {
            var marker = ProceduralSprites.DashedRing(5);
            BallEdges.Source = new FixedSource { Sprite = marker };
            var sim = Sim(new BallLoadout(WeaponRegistry.Create(BladeRule.WeaponId)), new BallLoadout(WeaponRegistry.Create(FangRule.WeaponId)));
            var view = Arena(sim, -1);
            Assert.That(view.GetBallView(0).EdgeFlashRenderer.sprite, Is.SameAs(marker));
        }

        [Test]
        public void SpriteOutlineSourceLightsExactlyTheSpritesDarkRim()
        {
            var tex = new Texture2D(6, 6, TextureFormat.RGBA32, false);
            var px = new Color32[36];
            var rim = new[] { 1, 2, 3, 6, 11, 30, 35 };
            foreach (var i in rim) px[i] = Palette.Outline;
            px[14] = new Color32(200, 60, 60, 255);   // body color: not part of the rim
            tex.SetPixels32(px);
            tex.Apply();
            var ball = Sprite.Create(tex, new Rect(0, 0, 6, 6), new Vector2(0.5f, 0.5f), 1f);
            var edge = new SpriteOutlineEdgeSource().Edge(ball, 1);
            var mask = edge.texture.GetPixels32();
            for (var i = 0; i < 36; i++) Assert.That(mask[i].a > 0, Is.EqualTo(rim.Contains(i)), $"pixel {i}");
            Assert.That(edge.rect.size, Is.EqualTo(ball.rect.size));
            Assert.That(edge.pivot, Is.EqualTo(ball.pivot));

            tex.Apply(false, true);   // no longer readable: falls back to the procedural ring
            var unread = Sprite.Create(tex, new Rect(0, 0, 6, 6), new Vector2(0.5f, 0.5f), 1f);
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("not readable"));
            Assert.That(new SpriteOutlineEdgeSource().Edge(unread, 1), Is.SameAs(ProceduralSprites.BallOutlineThin));
        }

        // ---- 4. Second Wind across a run ----

        static readonly FieldInfo UsedField = typeof(SecondWindTrait).GetField("used", BindingFlags.NonPublic | BindingFlags.Instance);

        static SecondWindTrait SecondWindOf(BallState b) => b.Traits.OfType<SecondWindTrait>().FirstOrDefault();

        /// <summary>
        /// Steps a fight to the end checking, after every step, the rule the tester saw broken: a living ball at or under
        /// 30% HP has used Second Wind, and no ball dies with it unused. Returns true when it fired.
        /// </summary>
        static bool PlayCheckingSecondWind(MatchSim m, string label, List<SimEvent> sink = null)
        {
            var you = m.Balls[0];
            var sw = SecondWindOf(you);
            Assert.That(sw, Is.Not.Null, $"{label}: Second Wind is in the fight's loadout");
            Assert.That((bool)UsedField.GetValue(sw), Is.False, $"{label}: unused at the start");
            var guard = 0;
            var lowSince = -1;   // tick at whose end the ball was alive at <= 30%: by the end of the next tick it must have fired
            while (m.Outcome == MatchOutcome.Ongoing && guard++ < 200 * 60)
            {
                var wasAlive = you.Alive;
                m.Step();
                sink?.AddRange(m.Events);
                var used = (bool)UsedField.GetValue(sw);
                if (lowSince >= 0 && you.Alive)
                    Assert.That(used, Is.True, $"{label} tick {m.Tick}: at {you.Hp:0.#}/{you.MaxHp:0.#} since tick {lowSince}, Second Wind has not fired");
                lowSince = you.Alive && !used && you.HpFraction <= TraitTuning.SecondWindThreshold ? m.Tick : -1;
                if (wasAlive && !you.Alive)
                    Assert.That(used, Is.True, $"{label} tick {m.Tick}: died with Second Wind unused");
            }
            return (bool)UsedField.GetValue(sw);
        }

        [Test]
        public void SecondWindFiresFromFortyOneOfAHundredAgainstEveryEnemy()
        {
            foreach (var enemy in WeaponRegistry.All)
                foreach (var boss in new[] { false, true })
                {
                    var you = new BallLoadout(WeaponRegistry.Create(BladeRule.WeaponId)) { MaxHp = TFix.X(100f), Hp = TFix.X(41f) }.With(TraitRegistry.Create(SecondWindTrait.TraitId, 1));
                    var foe = new BallLoadout(WeaponRegistry.Create(enemy.Id));
                    if (boss) { foe.MaxHp = RunTuning.BossHp[1]; foe.Radius = RunTuning.BossRadius; }
                    var m = new MatchSim(ArenaRegistry.Create(ArenaRegistry.Classic), 9, new[] { you, foe });
                    m.ApplySpawnHooks();
                    Assert.That(m.Balls[0].Hp.ToFloat(), Is.EqualTo(41f));
                    PlayCheckingSecondWind(m, $"vs {enemy.Id}{(boss ? " boss" : "")}");
                }
        }

        static MatchSim lastMatch;

        static RunFlow OpenFlow(MemorySaveStore store, string force)
        {
            var f = new RunFlow(store, 2f, 2f) { ForceTraitId = force };
            f.MatchPrepared += m => lastMatch = m;
            f.Open();
            return f;
        }

        [TestCase(true)]
        [TestCase(false)]
        public void SecondWindStaysAndFiresThroughABossAndASaveLoad(bool forced)
        {
            var store = new MemorySaveStore();
            uint seed = 0;
            if (forced) seed = 31;
            else
                for (uint s = 1; s < 2000 && seed == 0; s++)
                {
                    var w = RunState.StartChoices(s, UnlockRules.Weapons(new ProgressData()))[0];
                    var probe = RunState.Start(s, w, new ProgressData());
                    if (Enumerable.Range(0, probe.OfferCount).Any(i => probe.OfferCard(i).Equals(new Card(CardKind.Trait, SecondWindTrait.TraitId, 1)))) seed = s;
                }
            Assert.That(seed, Is.Not.Zero, "a seed whose first offer has Second Wind");

            var f = OpenFlow(store, forced ? SecondWindTrait.TraitId : null);
            f.NewRun(seed);
            f.ChooseWeapon(f.StartChoices[0]);
            var sw = Enumerable.Range(0, f.Run.OfferCount).First(i => f.Run.OfferCard(i).Id == SecondWindTrait.TraitId);
            f.PickStart(sw);
            Assert.That(f.Run.Build.TraitLevel(SecondWindTrait.TraitId), Is.EqualTo(1));
            f.Run.Build.DamagePct = TFix.X(10f); // This fixture must reach Shell King to test trait persistence through the boss.

            var fired = new List<int>();
            var bossSeen = false;
            for (var fight = 0; fight < 6 && f.Current == RunFlow.State.Prepare; fight++)
            {
                var index = f.Run.FightIndex;
                bossSeen |= f.Run.IsBossFight;
                if (index > RunTuning.BossFights[0]) f.Run.Build.DamagePct = TFix.X(0f);
                if (fight > 0)
                {
                    if (f.Run.CanPick(0)) f.Pick(0);   // keep taking cards like a player (swap, heal, max HP, traits)
                    f.Run.Build.SetHp((index > RunTuning.BossFights[0] ? TFix.X(25f) : TFix.X(41f)) * f.Run.Build.MaxHp / TFix.X(100f));
                }
                // The run now has one retry; this sequence must win to advance.
                Assert.That(f.Run.Build.TraitLevel(SecondWindTrait.TraitId), Is.GreaterThanOrEqualTo(1), $"fight {index + 1}: still in the build");
                Assert.That(SecondWindOf(f.Run.CreateMatch().Balls[0]), Is.Not.Null, $"fight {index + 1}: in CreateMatch's loadout");

                f.Fight();
                f.Tick(2.1f);
                var m = lastMatch;
                if (PlayCheckingSecondWind(m, $"fight {index + 1}")) fired.Add(index + 1);
                f.OnMatchEnded(m);
                f.Tick(2.1f);
                Assert.That(f.Current, Is.EqualTo(RunFlow.State.FightResult));
                f.NextAfterResult();

                if (index == RunTuning.BossFights[0] && f.Current == RunFlow.State.Prepare)
                {
                    f.Close();   // save / load right after the boss
                    f = OpenFlow(store, forced ? SecondWindTrait.TraitId : null);
                    f.Continue();
                    Assert.That(f.Current, Is.EqualTo(RunFlow.State.Prepare));
                }
            }
            Assert.That(bossSeen, Is.True, "a boss fight was played");
            Assert.That(fired.Count(n => n >= 4), Is.GreaterThanOrEqualTo(1), $"Second Wind fired after the boss (fired in fights {string.Join(",", fired)})");
        }

        // ---- 5. Prepare HP = the fight's HP ----

        static (string you, string enemy) Expected(MatchSim m) =>
            ($"HP {HpText.Of(m.Balls[0].Hp.ToFloat())}/{HpText.Of(m.Balls[0].MaxHp.ToFloat())}", PrepareView.EnemyHpText(m.Balls[1].Hp.ToFloat(), m.Balls[1].MaxHp.ToFloat()));

        void AssertPrepareMatchesFight(PrepareView v, RunFlow f, string label)
        {
            v.Show(f.Run);
            var shownYou = v.YouStats;
            var shownEnemy = v.EnemyStats;
            var preview = Expected(f.Run.CreateMatch());
            Assert.That(shownYou, Does.StartWith(preview.you), label);
            Assert.That(shownEnemy, Is.EqualTo(preview.enemy), label);
        }

        [Test]
        public void PrepareHpEqualsTheHpTheFightStartsWith()
        {
            var store = new MemorySaveStore();
            var f = OpenFlow(store, null);
            f.NewRun(44);
            f.ChooseWeapon(f.StartChoices[0]);
            f.PickStart(0);
            var v = PrepareView.Create(root.transform, art);
            v.FightRequested += () => f.Fight();
            AssertPrepareMatchesFight(v, f, "no card yet");

            void TakeCard(Card c, string label)
            {
                f.Run.OfferCards[0] = OfferSlot.From(c);
                f.Run.PicksTaken = 0;
                f.Run.PickedMask = 0;
                f.Run.Coins = 10;
                f.Pick(0);
                AssertPrepareMatchesFight(v, f, label);
            }

            f.Run.Build.SetHp(TFix.X(40f));
            TakeCard(new Card(CardKind.Heal), "after HEAL");
            TakeCard(new Card(CardKind.MaxHp), "after MAX HP");
            TakeCard(new Card(CardKind.Trait, HeavyTrait.TraitId, 1), "after HEAVY");
            TakeCard(new Card(CardKind.Trait, GlassCannonTrait.TraitId, 1), "after GLASS CANNON");

            // SKIP FREE PICK? YES: the fight starts with what Prepare showed.
            var g = OpenFlow(new MemorySaveStore(), null);
            g.NewRun(45);
            g.ChooseWeapon(g.StartChoices[0]);
            g.PickStart(0);
            g.Run.OfferCards[0] = OfferSlot.From(new Card(CardKind.DamageUp));   // at least one card can be taken: the dialog shows
            var w = PrepareView.Create(root.transform, art);
            w.FightRequested += () => g.Fight();
            w.Show(g.Run);
            var before = (w.YouStats, w.EnemyStats);
            w.PressFight();
            Assert.That(w.ConfirmOpen, Is.True);
            w.PressConfirm(true);
            Assert.That(g.Current, Is.EqualTo(RunFlow.State.Countdown));
            var started = Expected(lastMatch);
            Assert.That(before.Item1, Does.StartWith(started.you), "skip: you");
            Assert.That(before.Item2, Is.EqualTo(started.enemy), "skip: enemy");

            // After a boss fight (with Heavy in the build and HP carried over).
            var h = OpenFlow(new MemorySaveStore(), null);
            h.NewRun(46);
            h.ChooseWeapon(h.StartChoices[0]);
            h.PickStart(0);
            h.Run.Build.Traits.Add(new TraitSlot(HeavyTrait.TraitId, 2));
            h.Run.Build.DamagePct = TFix.X(10f); // Reach the boss before checking carried HP after it.
            while (h.Run.FightIndex <= RunTuning.BossFights[0] && h.Current == RunFlow.State.Prepare)
            {
                var wasBoss = h.Run.IsBossFight;
                // The run now has one retry; this sequence must win to advance.
                AssertPrepareMatchesFight(v, h, $"before fight {h.Run.FightIndex + 1}");
                h.Fight();
                h.Tick(2.1f);
                var shown = Expected(lastMatch);
                Assert.That(v.YouStats, Does.StartWith(shown.you), $"fight {h.Run.FightIndex + 1} started as shown");
                while (lastMatch.Outcome == MatchOutcome.Ongoing) lastMatch.Step();
                h.OnMatchEnded(lastMatch);
                h.Tick(2.1f);
                h.NextAfterResult();
                if (wasBoss) AssertPrepareMatchesFight(v, h, "after the boss");
            }
        }

        [Test]
        public void RunStartLogsTheSeed()
        {
            UnityEngine.TestTools.LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(@"Run started: seed 12345, weapon \w+"));
            var f = OpenFlow(new MemorySaveStore(), null);
            f.NewRun(12345);
            f.ChooseWeapon(f.StartChoices[0]);
            Assert.That(RunFlow.RunStartLog(7, "blade", "second-wind"), Is.EqualTo("[BallBattle] Run started: seed 7, weapon blade, -bbForceTrait second-wind"));
        }

        // ---- 6. Damage numbers at least 12 px apart ----

        static void AssertApartNow(DamagePopups popups, string label)
        {
            var now = popups.ShowingNow();
            for (var i = 0; i < now.Count; i++)
                for (var j = i + 1; j < now.Count; j++)
                {
                    var a = DamagePopups.StartRect(new Vector2(now[i].start.x, now[i].y), now[i].value, now[i].scale);
                    var b = DamagePopups.StartRect(new Vector2(now[j].start.x, now[j].y), now[j].value, now[j].scale);
                    if (a.xMax <= b.xMin || b.xMax <= a.xMin) continue;   // different columns
                    Assert.That(Mathf.Abs(now[i].y - now[j].y), Is.GreaterThanOrEqualTo(DamagePopups.StackStep - 0.001f), $"{label}: '{now[i].value}' and '{now[j].value}'");
                }
        }

        [Test]
        public void TwoAndThreeNumbersInOneTickAtOnePointAreTwelvePixelsApart()
        {
            var arena = ArenaRect.Centered(TFix.X(230f), TFix.X(230f));
            foreach (var count in new[] { 2, 3 })
            {
                var popups = DamagePopups.Create(root.transform, art.Font, 8);
                for (var i = 0; i < count; i++) popups.Show(Vector2.zero, 0.2f, FxView.ReflectColor, arena, 2);   // Thorns, same tick
                var ys = popups.Showing().Select(p => p.start.y).OrderBy(y => y).ToArray();
                Assert.That(ys.Length, Is.EqualTo(count));
                for (var i = 1; i < count; i++) Assert.That(ys[i] - ys[i - 1], Is.EqualTo(DamagePopups.StackStep), $"{count} numbers");
                for (var t = 0; t < 45; t++) { popups.Tick(1f / 60f); AssertApartNow(popups, $"{count} numbers, frame {t}"); }
            }
        }

        [Test]
        public void NumbersAtTheTopWallStackDownAndStayInside()
        {
            var arena = ArenaRect.Centered(TFix.X(230f), TFix.X(230f));
            var popups = DamagePopups.Create(root.transform, art.Font, 8);
            for (var i = 0; i < 3; i++) popups.Show(new Vector2(10f, arena.Top.ToFloat() - 2f), 0.2f, FxView.ReflectColor, arena, 2);
            var shown = popups.Showing();
            foreach (var p in shown)
                Assert.That(p.start.y + DamagePopups.Rise + DamagePopups.InkTop(p.scale), Is.LessThanOrEqualTo(arena.Top.ToFloat() - DamagePopups.WallMargin), "never over the top wall");
            var ys = shown.Select(p => p.start.y).OrderByDescending(y => y).ToArray();
            for (var i = 1; i < ys.Length; i++) Assert.That(ys[i - 1] - ys[i], Is.GreaterThanOrEqualTo(DamagePopups.StackStep), "stacked downwards");
            for (var t = 0; t < 45; t++) { popups.Tick(1f / 60f); AssertApartNow(popups, $"top, frame {t}"); }
        }

        [Test]
        public void ANumberShownLaterNeverCatchesUpWithOneStillRising()
        {
            var arena = ArenaRect.Centered(TFix.X(230f), TFix.X(230f));
            var popups = DamagePopups.Create(root.transform, art.Font, 8);
            popups.Show(Vector2.zero, 1.4f, Palette.Text, arena);
            for (var t = 0; t < 6; t++) popups.Tick(1f / 60f);   // 0.1 s later: the first has risen ~7 px
            popups.Show(Vector2.zero, 0.2f, FxView.ReflectColor, arena, 2);
            for (var t = 0; t < 40; t++) { popups.Tick(1f / 60f); AssertApartNow(popups, $"frame {t}"); }
        }

        [Test]
        public void NumbersFollowAWallThatClosesIn()
        {
            var popups = DamagePopups.Create(root.transform, art.Font, 8);
            var wide = ArenaRect.Centered(TFix.X(230f), TFix.X(230f));
            popups.Show(new Vector2(0f, 80f), 3f, Palette.Text, wide);
            var tight = ArenaRect.Centered(TFix.X(110f), TFix.X(110f));
            popups.SetBounds(tight);
            popups.Tick(0.3f);
            var y = popups.ShowingNow()[0].y;
            Assert.That(y + DamagePopups.InkTop(1), Is.LessThanOrEqualTo(tight.Top.ToFloat() - DamagePopups.WallMargin));
        }

        // ---- 7. Versus overlay ----

        [Test]
        public void VersusHidesTheRoundLineOnceDecidedAndTheResultIgnoresEarlyTaps()
        {
            PixelInput.SetTestClock(0f);
            var overlay = OverlayView.Create(root.transform, art);
            var flow = new MatchFlow(3f, 2f);
            var actions = new List<string>();
            overlay.RematchRequested += () => actions.Add("rematch");
            overlay.NewMatchRequested += () => actions.Add("new");
            overlay.MenuRequested += () => actions.Add("menu");

            flow.Begin(BladeRule.WeaponId, FangRule.WeaponId, 3);
            for (var round = 0; round < 2; round++)
            {
                overlay.Render(flow, 0f);
                flow.Tick(3.1f);
                overlay.Render(flow, 0f);
                Assert.That(overlay.ScoreText, Does.StartWith("ROUND"), "shown while the series is on");
                flow.OnRoundEnded(0);
                overlay.Render(flow, 0f);
                if (round == 0) { Assert.That(overlay.ScoreText, Does.StartWith("ROUND")); flow.Tick(2.1f); }
            }
            Assert.That(flow.Series.IsOver, Is.True);
            Assert.That(overlay.ScoreText, Is.Empty, "no ROUND 3 / 2-0 line in the pause after the deciding round");

            flow.Tick(2.1f);
            overlay.Render(flow, 0f);
            Assert.That(flow.Current, Is.EqualTo(MatchFlow.State.Result));
            Assert.That(overlay.ResultShown, Is.True);
            Assert.That(overlay.ScoreText, Is.Empty);

            void Tap(Vector2 at)
            {
                PixelInput.InjectTap(at);
                PixelInput.Dispatch(() => root.GetComponentsInChildren<PixelButton>(false));
            }

            Tap(OverlayView.RematchArea.center);
            PixelInput.AdvanceTestClock(0.2f);
            Tap(OverlayView.BannerArea.center);
            Assert.That(actions, Is.Empty, "taps in the first 300 ms do nothing");

            PixelInput.AdvanceTestClock(0.2f);
            Tap(new Vector2(0f, 120f));        // the "BLADE WINS" title
            Tap(new Vector2(-120f, -200f));    // empty space
            Assert.That(actions, Is.Empty, "title and empty space do nothing");

            Tap(OverlayView.RematchArea.center);
            Assert.That(actions, Is.EqualTo(new[] { "rematch" }), "the three buttons still work");
        }
    }
}
