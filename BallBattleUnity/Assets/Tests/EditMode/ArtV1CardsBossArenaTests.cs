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
    /// <summary>Prompt 5b: trait icons, card frames and counter style, boss sprites, arena floor tiles / colors / spike teeth.</summary>
    public class ArtV1CardsBossArenaTests
    {
        GameObject root;
        ArtLibrary art;

        [SetUp]
        public void SetUp()
        {
            art = AssetDatabase.LoadAssetAtPath<ArtLibrary>(PlaceholderArtGenerator.LibraryPath);
            Assert.That(art, Is.Not.Null);
            root = new GameObject("ArtV1Root");
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        static void Draw(ArenaView view) =>
            typeof(ArenaView).GetMethod("Draw", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(view, new object[] { 0f, 0f });

        // ---- assets ----

        [Test]
        public void EveryTraitHasAnIcon()
        {
            foreach (var t in TraitRegistry.All)
            {
                var icon = art.TraitIcon(t.Id);
                Assert.That(icon, Is.Not.Null, $"icons/trait_{t.Id}.png");
                Assert.That(icon.rect.size, Is.EqualTo(new Vector2(14, 14)));
                Assert.That(icon.pixelsPerUnit, Is.EqualTo(1f));
                Assert.That(icon.texture.filterMode, Is.EqualTo(FilterMode.Point));
            }
            Assert.That(art.CardAntidote, Is.Not.Null, "icons/card_antidote.png");
        }

        [Test]
        public void EveryArenaHasAFloorTileAndColors()
        {
            var json = PlaceholderArtGenerator.ReadArenaColors(out var closing);
            Assert.That(closing.HasValue, Is.True, "closing_wall");
            Assert.That(art.ClosingWallColor, Is.EqualTo(closing.Value));
            foreach (var a in ArenaRegistry.All)
            {
                Assert.That(art.TryArena(a.Id, out var look), Is.True, a.Id);
                Assert.That(look.Floor, Is.Not.Null, $"arena/floor_{a.Id}.png");
                Assert.That(look.Floor.rect.size, Is.EqualTo(new Vector2(10, 10)));
                Assert.That(json.ContainsKey(a.Id), Is.True, $"arena_colors.json has {a.Id}");
                Assert.That(look.FloorColor, Is.EqualTo(json[a.Id].floor));
                Assert.That(look.WallColor, Is.EqualTo(json[a.Id].wall));
            }
            Assert.That(art.SpikeTooth, Is.Not.Null);
            Assert.That(art.SpikeTooth.rect.size, Is.EqualTo(new Vector2(6, 4)));
        }

        [Test]
        public void EveryBossStateHasASprite()
        {
            Assert.That(art.ShellKing, Is.Not.Null);
            Assert.That(art.ShellKingGlow, Is.Not.Null);
            foreach (ArtLibrary.MimicLook look in System.Enum.GetValues(typeof(ArtLibrary.MimicLook)))
            {
                var s = art.MimicSprite(look);
                Assert.That(s, Is.Not.Null, look.ToString());
                Assert.That(s.rect.size, Is.EqualTo(new Vector2(64, 64)));
                Assert.That(s.pivot, Is.EqualTo(new Vector2(32, 30)), "body centre (32,34) from the top-left");
            }
            foreach (var k in new[] { DamageKind.Weapon, DamageKind.Projectile, DamageKind.Status, DamageKind.Reflect })
                Assert.That(ArenaView.MimicLookFor(k), Is.Not.EqualTo(ArtLibrary.MimicLook.Idle));
        }

        [Test]
        public void CardFramesAreNineSliceWithFourPixelBorders()
        {
            foreach (ArtLibrary.CardFrame f in System.Enum.GetValues(typeof(ArtLibrary.CardFrame)))
            {
                var s = art.Frame(f);
                Assert.That(s, Is.Not.Null, f.ToString());
                Assert.That(s.rect.size, Is.EqualTo(new Vector2(16, 16)));
                Assert.That(s.border, Is.EqualTo(new Vector4(4, 4, 4, 4)));
            }
        }

        // ---- HUD badges ----

        [Test]
        public void BadgesUseTheTraitIcons()
        {
            var a = new BallLoadout(WeaponRegistry.Create(BladeRule.WeaponId)).With(TraitRegistry.Create(SecondWindTrait.TraitId, 1), TraitRegistry.Create(HeavyTrait.TraitId, 2));
            var b = new BallLoadout(WeaponRegistry.Create(FangRule.WeaponId)).With(TraitRegistry.Create(ThornsTrait.TraitId, 1));
            var sim = new MatchSim(ArenaRegistry.Create(ArenaRegistry.Classic), 2, new[] { a, b });
            var view = root.AddComponent<ArenaView>();
            view.Art = art;
            view.AutoRestart = false;
            view.StartMatch(sim);
            Assert.That(view.Badges.IconOf(0, SecondWindTrait.TraitId), Is.SameAs(art.TraitIcon(SecondWindTrait.TraitId)));
            Assert.That(view.Badges.IconOf(0, HeavyTrait.TraitId), Is.SameAs(art.TraitIcon(HeavyTrait.TraitId)));
            Assert.That(view.Badges.IconOf(1, ThornsTrait.TraitId), Is.SameAs(art.TraitIcon(ThornsTrait.TraitId)));
            view.Badges.SetSpent(0, SecondWindTrait.TraitId, true);
            var c = view.Badges.BoxColor(0, SecondWindTrait.TraitId).Value;
            Assert.That(c.r, Is.EqualTo((byte)(255 * TraitBadgeView.SpentBrightness)), "a spent icon is dimmed to 40%");
        }

        // ---- cards ----

        /// <summary>A weapon this seed really offers at the start (RunState.Start rejects others).</summary>
        static string FirstChoice(uint seed) => RunState.StartChoices(seed, UnlockRules.Weapons(new ProgressData()))[0];

        static RunState RunWith(params Card[] cards)
        {
            var run = RunState.Start(61, FirstChoice(61), new ProgressData());
            run.Coins = 10;
            run.OfferCards.Clear();
            foreach (var c in cards) run.OfferCards.Add(OfferSlot.From(c));
            return run;
        }

        [Test]
        public void CardsHaveRarityFrameTraitStripeIconAndGoldNumbers()
        {
            var run = RunWith(new Card(CardKind.Trait, VampireTrait.TraitId, 1), new Card(CardKind.Trait, GlassCannonTrait.TraitId, 2), new Card(CardKind.MaxHp));
            run.Build.Traits.Add(new TraitSlot(GlassCannonTrait.TraitId, 1));
            var v = PrepareView.Create(root.transform, art);
            v.Show(run);

            Assert.That(v.CardFrameRenderer(0).sprite, Is.SameAs(art.Frame(ArtLibrary.CardFrame.Uncommon)));
            Assert.That(v.CardFrameRenderer(1).sprite, Is.SameAs(art.Frame(ArtLibrary.CardFrame.Rare)), "trait upgrade: rare");
            Assert.That(v.CardFrameRenderer(2).sprite, Is.SameAs(art.Frame(ArtLibrary.CardFrame.Common)));
            Assert.That(v.CardFrameRenderer(0).drawMode, Is.EqualTo(SpriteDrawMode.Sliced));
            Assert.That(v.CardFrameRenderer(0).size, Is.EqualTo(new Vector2(PrepareView.CardArea(0).width, PrepareView.CardArea(0).height)));

            Assert.That((Color32)v.CardStripe(0).color, Is.EqualTo(TraitBadgeView.ColorOf(VampireTrait.TraitId)), "trait-colored stripe");
            Assert.That(v.CardStripe(0).bounds.size.x, Is.EqualTo(4f).Within(0.01f), "4 px stripe");
            Assert.That(v.CardStripe(0).bounds.min.x, Is.LessThan(v.CardIcon(0).bounds.min.x), "stripe on the left");

            Assert.That(v.CardIcon(0).sprite, Is.SameAs(art.TraitIcon(VampireTrait.TraitId)));
            Assert.That(v.CardIcon(0).bounds.size.x, Is.EqualTo(28f).Within(0.01f), "icon at 2x");
            Assert.That(v.CardIcon(0).bounds.max.x, Is.LessThan(v.CardTitleText(0).WorldBounds.xMin), "icon left of the title");
            Assert.That(v.CardIcon(2).sprite, Is.SameAs(art.CardMaxHp), "stat cards have their icon too (3d)");

            var detail = v.CardDetail(0, 1);
            var text = detail.Text;
            var digit = text.IndexOfAny("0123456789".ToCharArray());
            var letter = text.IndexOf('H');
            Assert.That(digit, Is.GreaterThanOrEqualTo(0));
            Assert.That(detail.GlyphColor(digit), Is.EqualTo(CardFace.NumberColor), "numbers in gold");
            Assert.That(detail.GlyphColor(letter), Is.Not.EqualTo(CardFace.NumberColor), "words not");
            Assert.That(PixelText.IsNumberChar("HITS X1.5 DMG", 5), Is.True, "X1.5");
            Assert.That(PixelText.IsNumberChar("MAX HP", 2), Is.False, "the X of MAX");

            AssertCardsInsideFrame(v);
        }

        [Test]
        public void CounterCardStyleIsReady()
        {
            var v = PrepareView.Create(root.transform, art);
            v.Show(RunWith(new Card(CardKind.DamageUp), new Card(CardKind.SpeedUp), new Card(CardKind.MaxHp)));
            v.ShowFace(1, CardFace.CounterSample(art));
            Assert.That(v.CardFrameRenderer(1).sprite, Is.SameAs(art.Frame(ArtLibrary.CardFrame.Counter)), "dashed frame");
            Assert.That(v.CardFrameRenderer(1).drawMode, Is.EqualTo(SpriteDrawMode.Tiled), "dashes keep their size");
            Assert.That(v.CardTag(1).Text, Is.EqualTo("1 FIGHT"), "in place of LV");
            Assert.That(v.CardDetail(1, 2).Text, Is.EqualTo("NOT A TRAIT SLOT"));
            Assert.That(v.CardIcon(1).sprite, Is.SameAs(art.CardAntidote));
            Assert.That(v.CardTitleText(1).Text, Is.EqualTo("ANTIDOTE"));
            AssertCardsInsideFrame(v);
        }

        [Test]
        public void LongestCardTextsStayInsideTheCard()
        {
            var v = PrepareView.Create(root.transform, art);
            foreach (var t in TraitRegistry.All)
            {
                var run = RunWith(new Card(CardKind.Trait, t.Id, 1), new Card(CardKind.Trait, t.Id, 2), new Card(CardKind.SwapWeapon, VolleyRule.WeaponId));
                run.Coins = 0;   // longest reasons: NOT ENOUGH COINS, ALREADY ...
                run.PicksTaken = 1;
                v.Show(run);
                AssertCardsInsideFrame(v);
            }
        }

        static void AssertCardsInsideFrame(PrepareView v)
        {
            for (var i = 0; i < RunTuning.OfferSize; i++)
            {
                var area = PrepareView.CardArea(i);
                var inner = new Rect(area.xMin + 2, area.yMin + 2, area.width - 4, area.height - 4);
                var texts = new[] { v.CardTitleText(i), v.CardTag(i), v.CardDetail(i, 1), v.CardDetail(i, 2) };
                foreach (var t in texts)
                {
                    if (string.IsNullOrEmpty(t.Text)) continue;
                    var b = t.WorldBounds;
                    Assert.That(b.xMin >= inner.xMin && b.xMax <= inner.xMax && b.yMin >= inner.yMin && b.yMax <= inner.yMax, Is.True, $"'{t.Text}' inside card {i}");
                    Assert.That(b.xMin >= -135f && b.xMax <= 135f, Is.True, "inside the 270 px screen");
                }
                var title = v.CardTitleText(i).WorldBounds;
                var tag = v.CardTag(i).WorldBounds;
                if (!string.IsNullOrEmpty(v.CardTag(i).Text)) Assert.That(title.xMax + 2f, Is.LessThanOrEqualTo(tag.xMin), $"card {i}: title and tag do not touch");
            }
        }

        // ---- bosses ----

        ArenaView BossFight(ArenaView.BossLook look)
        {
            var player = new BallLoadout(WeaponRegistry.Create(BladeRule.WeaponId));
            var boss = new BallLoadout(WeaponRegistry.Create(BrawlerRule.WeaponId)) { MaxHp = TFix.X(220f), Radius = RunTuning.BossRadius };
            var sim = new MatchSim(ArenaRegistry.Create(ArenaRegistry.Classic), 4, new[] { player, boss });
            sim.ApplySpawnHooks();
            var view = root.AddComponent<ArenaView>();
            view.Art = art;
            view.AutoRestart = false;
            view.Boss = look;
            view.StartMatch(sim, "YOU", "BOSS");
            Draw(view);
            return view;
        }

        [Test]
        public void ShellKingUsesItsSpriteAtItsOwnSizeAndGlows()
        {
            var view = BossFight(ArenaView.BossLook.ShellKing);
            var boss = view.GetBallView(1);
            Assert.That(boss.BodySprite, Is.SameAs(art.ShellKing));
            Assert.That(boss.Scale, Is.EqualTo(1f).Within(0.001f), "drawn big already: no x1.5");
            Assert.That(boss.NativeRadius * boss.Scale, Is.EqualTo(view.Sim.Balls[1].Radius.ToFloat()).Within(0.001f), "sim radius 24 = body radius 24");
            Assert.That(boss.UsesOutlineFlash, Is.True, "big ball: outline flash");
            Assert.That(boss.EdgeFlashRenderer.sprite, Is.SameAs(BallEdges.Source.Edge(art.ShellKing, 1)), "flash follows the boss sprite's rim");
            view.PlayBossGlow();
            Assert.That(boss.IsGlowing, Is.True);
            boss.Render(boss.transform.localPosition, 0f, 0f, 0f, 0f, true, 1.01f);
            Assert.That(boss.IsGlowing, Is.False, "glow lasts 1 s");
        }

        [Test]
        public void MimicStartsIdleAndChangesWithWhatItAdaptedTo()
        {
            var view = BossFight(ArenaView.BossLook.Mimic);
            var boss = view.GetBallView(1);
            Assert.That(boss.BodySprite, Is.SameAs(art.MimicSprite(ArtLibrary.MimicLook.Idle)));
            foreach (var k in new[] { DamageKind.Weapon, DamageKind.Projectile, DamageKind.Status, DamageKind.Reflect })
            {
                view.SetMimicLook(ArenaView.MimicLookFor(k));
                Assert.That(boss.BodySprite, Is.SameAs(art.MimicSprite(ArenaView.MimicLookFor(k))), k.ToString());
                Assert.That(boss.Scale, Is.EqualTo(1f).Within(0.001f));
            }
            Assert.That(view.GetBallView(0).BodySprite, Is.SameAs(art.Get(BladeRule.WeaponId).Ball), "the player keeps its ball");
        }

        [Test]
        public void RunPicksShellKingForEarlierBossesAndMimicForTheLast()
        {
            // ART-2 enables the dedicated boss bodies; the switch still supports disabling them.
            var run = RunState.Start(62, FirstChoice(62), new ProgressData());
            run.FightIndex = RunTuning.BossFights[0];
            Assert.That(RunController.BossLookFor(run), Is.EqualTo(ArenaView.BossLook.ShellKing), "ART-2 enabled");
            var was = RunController.BossArtEnabled;
            try
            {
                RunController.BossArtEnabled = true;
                run.FightIndex = 0;
                Assert.That(RunController.BossLookFor(run), Is.EqualTo(ArenaView.BossLook.None));
                run.FightIndex = RunTuning.BossFights[0];
                Assert.That(RunController.BossLookFor(run), Is.EqualTo(ArenaView.BossLook.ShellKing));
                run.FightIndex = RunTuning.BossFights[RunTuning.BossFights.Length - 1];
                Assert.That(RunController.BossLookFor(run), Is.EqualTo(ArenaView.BossLook.Mimic));
                RunController.BossArtEnabled = false;
                Assert.That(RunController.BossLookFor(run), Is.EqualTo(ArenaView.BossLook.None), "explicitly disabled art keeps the fallback");
            }
            finally { RunController.BossArtEnabled = was; }
        }
        // ---- arenas ----

        [Test]
        public void ArenaFloorIsTiledAndWallsUseTheJsonColors()
        {
            foreach (var a in ArenaRegistry.All)
            {
                var sim = new MatchSim(ArenaRegistry.Create(a.Id), 1, new[] { new BallLoadout(WeaponRegistry.Create(BladeRule.WeaponId)), new BallLoadout(WeaponRegistry.Create(FangRule.WeaponId)) });
                var view = root.AddComponent<ArenaView>();
                view.Art = art;
                view.AutoRestart = false;
                view.ArenaId = a.Id;
                view.StartMatch(sim);
                Draw(view);
                art.TryArena(a.Id, out var look);
                Assert.That(view.Frame.Floor.sprite, Is.SameAs(look.Floor), $"{a.Id}: floor_{a.Id}");
                Assert.That(view.Frame.Floor.drawMode, Is.EqualTo(SpriteDrawMode.Tiled));
                Assert.That(view.Frame.Floor.size, Is.EqualTo(new Vector2(Mathf.Round(sim.Arena.Right.ToFloat()) - Mathf.Round(sim.Arena.Left.ToFloat()), Mathf.Round(sim.Arena.Top.ToFloat()) - Mathf.Round(sim.Arena.Bottom.ToFloat()))));
                Assert.That(view.Frame.WallColor, Is.EqualTo(look.WallColor), $"{a.Id}: wall color from arena_colors.json");
                var spikes = sim.Config.Layout != null && sim.Config.Layout.WallDamage > TFix.X(0f);
                if (spikes)
                {
                    // BB-FIX1a: teeth only along the corner spike zones (1/5 of each wall at TL and BR).
                    var zones = ArenaFrameView.ZoneSegments(sim.Config.Layout.SpikeCorners, sim.Arena);
                    var expected = zones.Sum(z => Mathf.FloorToInt(z.Length / ArenaFrameView.ToothSpacing));
                    Assert.That(view.Frame.TeethShown, Is.EqualTo(expected), "teeth along the spike zones only");
                }
                else Assert.That(view.Frame.TeethShown, Is.Zero);
                Object.DestroyImmediate(view);
                foreach (Transform c in root.transform) Object.DestroyImmediate(c.gameObject);
            }
            Assert.That(ArenaRegistry.All.Any(x => x.Id == ArenaRegistry.SpikeWalls), Is.True);
        }

        [Test]
        public void ArenaWallsTurnTheClosingColorWhileClosing()
        {
            var config = ArenaRegistry.Create(ArenaRegistry.Classic);
            var sim = new MatchSim(config, 1, new[] { new BallLoadout(WeaponRegistry.Create(BladeRule.WeaponId)), new BallLoadout(WeaponRegistry.Create(FangRule.WeaponId)) });
            var frame = ArenaFrameView.Create(root.transform, art);
            frame.Render(config.ArenaAt(config.ShrinkStartTick + 10), config.ShrinkStartTick + 10, config, ArenaRegistry.Classic);
            Assert.That(frame.WallColor, Is.EqualTo(art.ClosingWallColor));
        }

        [Test]
        public void PrepareArenaPreviewUsesTheSameTileAndColors()
        {
            var run = RunState.Start(63, FirstChoice(63), new ProgressData());
            var v = PrepareView.Create(root.transform, art);
            v.Show(run);
            art.TryArena(run.ArenaId, out var look);
            Assert.That(v.PreviewFloor, Is.Not.Null);
            Assert.That(v.PreviewFloor.sprite, Is.SameAs(look.Floor));
            Assert.That(v.PreviewFloor.drawMode, Is.EqualTo(SpriteDrawMode.Tiled));
            Assert.That(v.PreviewFloor.transform.lossyScale.x, Is.EqualTo(PrepareView.PreviewTileScale), "tile drawn smaller");
        }
    }
}
