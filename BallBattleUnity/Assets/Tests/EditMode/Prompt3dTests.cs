using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BallBattle.EditorTools;
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
    /// Prompt 3d: DMG DEALT/TAKEN = HP really removed (through the real Run wiring), ScreenShake with a destroyed camera,
    /// HP shown rounded down (alive >= 1), pre-drawn x1.2/x1.35/x1.5 balls, boss = its weapon's _48 ball, stat card icons
    /// (cards + HUD), WINS w/p on the Run title.
    /// </summary>
    public class Prompt3dTests
    {
        const float Frame = 1f / 60f;
        GameObject root;
        ArtLibrary art;

        [SetUp]
        public void SetUp()
        {
            art = AssetDatabase.LoadAssetAtPath<ArtLibrary>(PlaceholderArtGenerator.LibraryPath);
            Assert.That(art, Is.Not.Null);
            root = new GameObject("P3dRoot");
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        static void Draw(ArenaView view) =>
            typeof(ArenaView).GetMethod("Draw", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(view, new object[] { 0f, 0f });

        static string FirstChoice(uint seed) => RunState.StartChoices(seed, UnlockRules.Weapons(new ProgressData()))[0];

        // ---- 1. DMG DEALT / TAKEN through the real wiring ----

        (ArenaView arena, RunController run, List<SimEvent> events) Rig()
        {
            var arena = root.AddComponent<ArenaView>();
            arena.Art = art;
            arena.AutoRestart = false;
            var rc = RunController.Create(root.transform, arena, new MemorySaveStore());
            var events = new List<SimEvent>();
            arena.SimEventRaised += events.Add;
            rc.Open();
            return (arena, rc, events);
        }

        static void Step(ArenaView arena, RunController rc)
        {
            rc.Tick(Frame);
            arena.Advance(Frame);
        }

        static IEnumerable<object[]> FightOneCases()
        {
            foreach (var w in RunState.StartChoices(1791713962u, UnlockRules.Weapons(new ProgressData())))
                yield return new object[] { 1791713962u, w };
            foreach (var s in new uint[] { 7, 4242, 90001, 123456789 })
                yield return new object[] { s, FirstChoice(s) };
        }

        [TestCaseSource(nameof(FightOneCases))]
        public void ResultDamageEqualsTheHpBarsOfFightOne(uint seed, string weapon)
        {
            var (arena, rc, events) = Rig();
            var flow = rc.Flow;
            flow.NewRun(seed);
            Step(arena, rc);
            flow.ChooseWeapon(weapon);
            Step(arena, rc);
            rc.Prepare.PressFight();
            if (rc.Prepare.ConfirmOpen) rc.Prepare.PressConfirm(true);
            Step(arena, rc);
            var match = flow.CurrentMatch;
            Assert.That(arena.Sim, Is.SameAs(match));
            var start = match.Balls.Select(b => b.Hp).ToArray();
            arena.TimeScale = 4f;
            var guard = 0;
            while (flow.Current != RunFlow.State.FightResult && guard++ < 60 * 300) Step(arena, rc);
            Assert.That(flow.Current, Is.EqualTo(RunFlow.State.FightResult));

            var heal = new float[2];
            foreach (var e in events) if (e.Type == SimEventType.Heal && e.A >= 0 && e.A < 2) heal[e.A] += e.Value;
            var lost = new float[2];
            for (var i = 0; i < 2; i++) lost[i] = start[i] + heal[i] - Mathf.Max(0f, match.Balls[i].Hp);

            var stats = flow.Stats;
            Assert.That(stats.Dealt(0), Is.EqualTo(lost[1]).Within(0.01f), "DMG DEALT = enemy start HP + heals - end HP");
            Assert.That(stats.Taken(0), Is.EqualTo(lost[0]).Within(0.01f), "DMG TAKEN = your start HP + heals - end HP");
            var report = flow.LastReport;
            Assert.That(report.DamageDealt, Is.EqualTo(lost[1]).Within(0.01f));
            Assert.That(report.DamageTaken, Is.EqualTo(lost[0]).Within(0.01f));
            if (match.WinnerIndex == 0) Assert.That(report.DamageDealt, Is.LessThanOrEqualTo(start[1] + heal[1] + 0.01f), "never more than the KO'd enemy had");
            Debug.Log($"[3d] seed {seed} {weapon} vs {match.Balls[1].Weapon.Id}: enemy start {start[1]:0.##} healed {heal[1]:0.##} end {match.Balls[1].Hp:0.##} -> DMG DEALT {stats.Dealt(0):0.##}; you start {start[0]:0.##} healed {heal[0]:0.##} end {match.Balls[0].Hp:0.##} -> DMG TAKEN {stats.Taken(0):0.##}");
        }

        // ---- 2. ScreenShake with a destroyed camera ----

        [Test]
        public void ScreenShakeStopsSafelyWhenTheCameraIsGone()
        {
            var cam = new GameObject("Cam");
            var shake = new ScreenShake(cam.transform);
            shake.Add(3f, 0.5f);
            shake.Tick(0.05f);
            Object.DestroyImmediate(cam);   // game closing mid-fight
            Assert.DoesNotThrow(() => shake.Stop());
            Assert.DoesNotThrow(() => shake.Tick(0.05f));
            Assert.DoesNotThrow(() => shake.Add(2f, 0.2f));
        }

        // ---- 3. HP rounded down, alive >= 1 ----

        [Test]
        public void HpIsShownRoundedDownAndAtLeastOneWhileAlive()
        {
            Assert.That(HpText.Of(97.2f), Is.EqualTo(97));
            Assert.That(HpText.Of(97.9f), Is.EqualTo(97));
            Assert.That(HpText.Of(0.4f), Is.EqualTo(1));
            Assert.That(HpText.Of(0f), Is.EqualTo(0));
            Assert.That(HpText.Of(-3f), Is.EqualTo(0));
            Assert.That(HpText.Of(100f * 1.35f), Is.EqualTo(135), "float noise of 100 x 1.35");
            Assert.That(HpText.Pair(40.6f, 135f), Is.EqualTo("40/135"));

            // HUD
            var sim = new MatchSim(ArenaRegistry.Create(ArenaRegistry.Classic), 1,
                new[] { new BallLoadout(WeaponRegistry.Create(BladeRule.WeaponId)) { MaxHp = 100f, Hp = 97.2f }, new BallLoadout(WeaponRegistry.Create(FangRule.WeaponId)) { MaxHp = 100f, Hp = 0.4f } });
            var view = root.AddComponent<ArenaView>();
            view.Art = art;
            view.AutoRestart = false;
            view.StartMatch(sim);
            Draw(view);
            Assert.That(view.Hud.HpText(0), Is.EqualTo("97"));
            Assert.That(view.Hud.HpText(1), Is.EqualTo("1"), "alive with 0.4 HP shows 1");

            // Prepare, result screen, HP NEXT FIGHT
            Assert.That(PrepareView.EnemyHpText(97.2f, 100f), Is.EqualTo("HP 97/100"));
            Assert.That(CardText.Stats(new RunBuild(), 97.2f, 100f), Does.StartWith("HP 97/100"));
            var r = new FightReport { EnemyWeaponId = FangRule.WeaponId, FightNumber = 1, HpLeft = 0.4f, MaxHpInFight = 100f, HpNext = 113.6f, MaxHpNext = 135f, Outcome = FightOutcome.Win, NextArenaId = ArenaRegistry.Classic, NextWeaponId = BladeRule.WeaponId, NextEnemyHp = 160.9f };
            Assert.That(r.CarryLine, Does.Contain("113/135"));
            Assert.That(r.NextStats, Does.StartWith("HP 160 "));
            var rv = RunResultView.Create(root.transform, art);
            rv.Show(r);
            Assert.That(rv.Value(0), Is.EqualTo("1/100"));
        }

        // ---- 4. Pre-drawn ball sizes ----

        ArenaView Arena(BallLoadout a, BallLoadout b)
        {
            var sim = new MatchSim(ArenaRegistry.Create(ArenaRegistry.Classic), 3, new[] { a, b });
            sim.ApplySpawnHooks();
            var view = root.AddComponent<ArenaView>();
            view.Art = art;
            view.AutoRestart = false;
            view.MarkedBall = 0;
            view.StartMatch(sim, "YOU", "FOE");
            Draw(view);
            return view;
        }

        [Test]
        public void SizedBallsUseTheirPreDrawnSpriteAtScaleOne()
        {
            foreach (var (level, expected) in new[] { (1, 38), (2, 43) })
            {
                var view = Arena(new BallLoadout(WeaponRegistry.Create(PikeRule.WeaponId)).With(TraitRegistry.Create(HeavyTrait.TraitId, level)),
                                 new BallLoadout(WeaponRegistry.Create(BladeRule.WeaponId)) { Radius = RunTuning.BossRadius });
                var heavy = view.GetBallView(0);
                var w = art.Get(PikeRule.WeaponId);
                var sprite = expected == 38 ? w.Ball38 : w.Ball43;
                Assert.That(sprite, Is.Not.Null, $"ball_pike_{expected}.png");
                Assert.That(heavy.BodySprite, Is.SameAs(sprite), $"Heavy {level}");
                Assert.That(heavy.UsesSizedSprite, Is.True);
                Assert.That(heavy.Scale, Is.EqualTo(1f).Within(0.001f), "drawn at scale 1");
                Assert.That(heavy.GetComponent<SpriteRenderer>().bounds.size.x, Is.EqualTo((float)expected).Within(0.01f));
                Assert.That(heavy.EdgeFlashRenderer.sprite, Is.SameAs(BallEdges.Source.Edge(sprite, 1)), "outline flash from the shown sprite");
                Assert.That(heavy.UsesOutlineFlash, Is.EqualTo(level == 2), "x1.35 still counts as a big ball");

                var boss = view.GetBallView(1);
                Assert.That(boss.BodySprite, Is.SameAs(art.Get(BladeRule.WeaponId).Ball48), "x1.5 uses _48");
                Assert.That(boss.Scale, Is.EqualTo(1f).Within(0.001f));

                // Marker: the arrow tip stays 5 rows above the top row of the sprite actually drawn.
                var disc = BallView.DiscOf(sprite);
                var top = (sprite.rect.height - sprite.pivot.y) / sprite.pixelsPerUnit - disc.topInset;
                var tipWhiteBottom = heavy.MarkerArrowPosition.y + ProceduralSprites.ArrowRowsBelowTip;
                Assert.That(tipWhiteBottom - (heavy.transform.position.y + Mathf.Round(top) - 1f), Is.EqualTo(5f).Within(0.001f));
                Object.DestroyImmediate(view);
                foreach (Transform c in root.transform) Object.DestroyImmediate(c.gameObject);
            }
        }

        [Test]
        public void OtherSizesScaleTheBaseSpriteAndMissingSizesFallBack()
        {
            var view = Arena(new BallLoadout(WeaponRegistry.Create(BladeRule.WeaponId)) { Radius = 20f }, new BallLoadout(WeaponRegistry.Create(FangRule.WeaponId)));
            var ball = view.GetBallView(0);
            Assert.That(ball.BodySprite, Is.SameAs(art.Get(BladeRule.WeaponId).Ball), "x1.25 has no pre-drawn sprite");
            Assert.That(ball.UsesSizedSprite, Is.False);
            Assert.That(ball.Scale, Is.EqualTo(20f / 16f).Within(0.001f));

            var bare = ScriptableObject.CreateInstance<ArtLibrary>();
            bare.Weapons = new[] { new ArtLibrary.WeaponArt { Id = BladeRule.WeaponId, Ball = art.Get(BladeRule.WeaponId).Ball } };
            Assert.That(bare.BallForSize(BladeRule.WeaponId, 1.5f, out var pre), Is.SameAs(art.Get(BladeRule.WeaponId).Ball));
            Assert.That(pre, Is.False, "no _48 file: base sprite, scaled");
            Object.DestroyImmediate(bare);
        }

        [Test]
        public void EveryWeaponHasTheThreeSizes()
        {
            foreach (var w in WeaponRegistry.All)
            {
                var a = art.Get(w.Id);
                foreach (var (s, n) in new[] { (a.Ball38, 38), (a.Ball43, 43), (a.Ball48, 48) })
                {
                    Assert.That(s, Is.Not.Null, $"ball_sizes/ball_{w.Id}_{n}.png");
                    Assert.That(s.rect.width, Is.EqualTo((float)n));
                    Assert.That(s.pixelsPerUnit, Is.EqualTo(1f));
                    Assert.That(s.texture.filterMode, Is.EqualTo(FilterMode.Point));
                    Assert.That(s.texture.isReadable, Is.True);
                }
            }
        }

        // ---- 5. Boss before v4 = its weapon's _48 ball, same on Prepare and in the arena ----

        [Test]
        public void BossIsItsWeaponsPreDrawnBallOnPrepareAndInTheArena()
        {
            var run = RunState.Start(64, FirstChoice(64), new ProgressData());
            run.FightIndex = RunTuning.BossFights[0];
            Assert.That(RunController.BossLookFor(run), Is.EqualTo(ArenaView.BossLook.None), "Shell King / Mimic off until v4");
            run.FightIndex = RunTuning.BossFights[RunTuning.BossFights.Length - 1];
            Assert.That(RunController.BossLookFor(run), Is.EqualTo(ArenaView.BossLook.None));
            run.FightIndex = RunTuning.BossFights[0];

            var prepare = PrepareView.Create(root.transform, art);
            prepare.Show(run);
            var enemy = run.Enemy.WeaponId;
            var expected = art.Get(enemy).Ball48;
            Assert.That(expected, Is.Not.Null);
            Assert.That(prepare.EnemyIcon.sprite, Is.SameAs(expected), "Prepare: ball_<id>_48");
            Assert.That(prepare.EnemyIcon.transform.localScale.x, Is.EqualTo(1f), "drawn at its own size");

            var view = root.AddComponent<ArenaView>();
            view.Art = art;
            view.AutoRestart = false;
            view.Boss = RunController.BossLookFor(run);
            view.StartMatch(run.CreateMatch(), "YOU", "BOSS");
            Draw(view);
            Assert.That(view.GetBallView(1).BodySprite, Is.SameAs(expected), "arena: the same sprite");
            Assert.That(view.GetBallView(1).UsesOutlineFlash, Is.True);
        }

        // ---- 6. Stat card icons ----

        /// <summary>Card kinds the pool deals that have no icon file yet (reported in docs/claude-runs/3d.md).</summary>
        static readonly CardKind[] KindsWithoutIconArt = { CardKind.Heal, CardKind.SwapWeapon };

        [Test]
        public void EveryCardInThePoolHasAnIcon()
        {
            var cards = new List<Card> { new Card(CardKind.DamageUp), new Card(CardKind.SpeedUp), new Card(CardKind.MaxHp), new Card(CardKind.Heal), new Card(CardKind.SwapWeapon, BladeRule.WeaponId) };
            foreach (var t in TraitRegistry.All) { cards.Add(new Card(CardKind.Trait, t.Id, 1)); cards.Add(new Card(CardKind.Trait, t.Id, 2)); }
            foreach (var c in cards)
            {
                var icon = art.CardIcon(c);
                if (KindsWithoutIconArt.Contains(c.Kind)) { Assert.That(icon, Is.Null, $"{c}: art arrived? remove it from KindsWithoutIconArt"); continue; }
                Assert.That(icon, Is.Not.Null, c.ToString());
                Assert.That(icon.rect.size, Is.EqualTo(new Vector2(14, 14)), c.ToString());
            }
            Assert.That(art.CardIcon(new Card(CardKind.SpeedUp)), Is.SameAs(art.CardSpeedCap));
            Assert.That(art.CardIcon(new Card(CardKind.MaxHp)), Is.SameAs(art.CardMaxHp));
            Assert.That(art.CardIcon(new Card(CardKind.DamageUp)), Is.SameAs(art.CardDamage));
        }

        [Test]
        public void StatCardsShowTheirIconOnPrepareAndAsHudBadges()
        {
            var run = RunState.Start(65, FirstChoice(65), new ProgressData());
            run.OfferCards.Clear();
            foreach (var c in new[] { new Card(CardKind.SpeedUp), new Card(CardKind.MaxHp), new Card(CardKind.DamageUp) }) run.OfferCards.Add(OfferSlot.From(c));
            var prepare = PrepareView.Create(root.transform, art);
            prepare.Show(run);
            Assert.That(prepare.CardIcon(0).sprite, Is.SameAs(art.CardSpeedCap));
            Assert.That(prepare.CardIcon(1).sprite, Is.SameAs(art.CardMaxHp));
            Assert.That(prepare.CardIcon(2).sprite, Is.SameAs(art.CardDamage));
            for (var i = 0; i < 3; i++) Assert.That(prepare.CardIcon(i).enabled, Is.True);

            // HUD through the Run wiring: take the three cards, start the fight.
            var arena = root.AddComponent<ArenaView>();
            arena.Art = art;
            arena.AutoRestart = false;
            var rc = RunController.Create(root.transform, arena, new MemorySaveStore());
            rc.Open();
            rc.Flow.NewRun(65);
            rc.Flow.ChooseWeapon(FirstChoice(65));
            var b = rc.Flow.Run.Build;
            b.SpeedPct += RunTuning.SpeedCardPct;
            b.DamagePct += RunTuning.DamageCardPct;
            b.MaxHp += RunTuning.MaxHpCardBonus;
            rc.Flow.Fight();
            Assert.That(arena.Badges.IconOf(0, TraitBadgeView.SpeedCapBadge), Is.SameAs(art.CardSpeedCap), "SPEED CAP badge");
            Assert.That(arena.Badges.IconOf(0, TraitBadgeView.MaxHpBadge), Is.SameAs(art.CardMaxHp));
            Assert.That(arena.Badges.IconOf(0, TraitBadgeView.DamageBadge), Is.SameAs(art.CardDamage));
            Assert.That(arena.Badges.IconOf(1, TraitBadgeView.SpeedCapBadge), Is.Null, "the enemy took no cards");
        }

        // ---- 8. WINS w/p ----

        [Test]
        public void RunTitleShowsWinsOutOfPlayed()
        {
            Assert.That(RunTitleView.StatsLine(new ProgressData { BestFight = 5, RunsWon = 3, RunsPlayed = 12 }), Is.EqualTo("BEST FIGHT 5/8   WINS 3/12"));
            var huge = RunTitleView.StatsLine(new ProgressData { BestFight = 8, RunsWon = 123456789, RunsPlayed = 2000000000 });
            Assert.That(huge.Length, Is.LessThanOrEqualTo(TextWrap.MaxCharsOnScreen(1)), "fits 270 px");

            // Old saves without RunsWon read 0; a run only counts as won when it beat the last boss.
            var old = JsonUtility.FromJson<ProgressData>("{\"RunsPlayed\":4,\"BestFight\":3}");
            Assert.That(old.RunsWon, Is.Zero);
            Assert.That(old.RunsPlayed, Is.EqualTo(4));
            var p = new ProgressData();
            var lost = RunState.Start(66, FirstChoice(66), p);
            lost.Over = true;
            UnlockRules.Record(p, lost);
            Assert.That((p.RunsWon, p.RunsPlayed), Is.EqualTo((0, 1)));
            var won = RunState.Start(67, FirstChoice(67), p);
            won.Over = true;
            won.Won = true;
            UnlockRules.Record(p, won);
            Assert.That((p.RunsWon, p.RunsPlayed), Is.EqualTo((1, 2)));
            Assert.That(SaveData.CurrentVersion, Is.EqualTo(1), "no save version bump");
        }
    }
}
