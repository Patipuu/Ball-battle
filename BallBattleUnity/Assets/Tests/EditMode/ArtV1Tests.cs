using System.IO;
using System.Linq;
using System.Reflection;
using BallBattle.EditorTools;
using BallBattle.Sim;
using BallBattle.Sim.Traits;
using BallBattle.Sim.Weapons;
using BallBattle.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BallBattle.Tests
{
    /// <summary>Prompt 5: real sprites (v1) for the 8 weapons, blade 9-slice, display names, HUD sprites, outline flash from the sprite rim.</summary>
    public class ArtV1Tests
    {
        GameObject root;
        ArtLibrary art;

        [SetUp]
        public void SetUp()
        {
            art = AssetDatabase.LoadAssetAtPath<ArtLibrary>(PlaceholderArtGenerator.LibraryPath);
            Assert.That(art, Is.Not.Null, "run BallBattle/Generate Placeholder Art");
            root = new GameObject("ArtRoot");
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        static TextureImporter Importer(Sprite s) => (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(s));

        [Test]
        public void EveryWeaponUsesTheRealSpritesImportedAsPixelArt()
        {
            var borders = PlaceholderArtGenerator.ReadBladeBorders();
            foreach (var w in WeaponRegistry.All)
            {
                var a = art.Get(w.Id);
                Assert.That(AssetDatabase.GetAssetPath(a.Ball), Is.EqualTo($"{PlaceholderArtGenerator.SpritesFolder}/ball_{w.Id}.png"), w.Id);
                var ball = Importer(a.Ball);
                Assert.That(ball.spritePixelsPerUnit, Is.EqualTo(1f));
                Assert.That(ball.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(ball.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
                Assert.That(ball.isReadable, Is.True, $"{w.Id}: readable for the outline flash");

                var hasBlade = w.Create().HasBlade;
                Assert.That(a.Blade != null, Is.EqualTo(hasBlade), $"{w.Id}: blade sprite iff the weapon has a blade");
                if (!hasBlade) continue;
                Assert.That(AssetDatabase.GetAssetPath(a.Blade), Is.EqualTo($"{PlaceholderArtGenerator.SpritesFolder}/blade_{w.Id}.png"));
                Assert.That(a.Blade.rect.height, Is.EqualTo(BallView.MinBladePixels), "7 px tall: outline 1 + core 5 + outline 1");
                Assert.That(borders.ContainsKey(w.Id), Is.True, $"{w.Id} in blade_borders.json");
                Assert.That(a.Blade.border, Is.EqualTo(borders[w.Id]), $"{w.Id}: 9-slice borders from blade_borders.json");
                Assert.That(a.Blade.border.y, Is.EqualTo(1f));
                Assert.That(a.Blade.border.w, Is.EqualTo(1f));
                Assert.That(a.Blade.pivot.y / a.Blade.rect.height, Is.EqualTo(BallView.BladePivotY).Within(0.0001f));
                var blade = Importer(a.Blade);
                Assert.That(blade.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(blade.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            }
            // The placeholders stay as the fallback.
            Assert.That(File.Exists($"{PlaceholderArtGenerator.Folder}/ball_blade.png"), Is.True);
        }

        [Test]
        public void BossAndHudSpritesAreImported()
        {
            foreach (var file in new[] { "hud/retry_token_on.png", "hud/retry_token_off.png", "hud/icon_antidote.png" })
            {
                var s = AssetDatabase.LoadAssetAtPath<Sprite>($"{PlaceholderArtGenerator.SpritesFolder}/{file}");
                Assert.That(s, Is.Not.Null, file);
                Assert.That(s.pixelsPerUnit, Is.EqualTo(1f));
                Assert.That(s.texture.filterMode, Is.EqualTo(FilterMode.Point));
            }
            foreach (var path in Directory.GetFiles($"{PlaceholderArtGenerator.SpritesFolder}/boss", "*.png"))
            {
                var imp = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
                Assert.That(imp.isReadable, Is.True, path);
                Assert.That(imp.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            }
        }

        static ArenaView Arena(GameObject root, ArtLibrary art, BallLoadout a, BallLoadout b)
        {
            var sim = new MatchSim(ArenaRegistry.Create(ArenaRegistry.Classic), 3, new[] { a, b });
            sim.ApplySpawnHooks();
            var view = root.AddComponent<ArenaView>();
            view.Art = art;
            view.AutoRestart = false;
            view.StartMatch(sim, "YOU", "FOE");
            typeof(ArenaView).GetMethod("Draw", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(view, new object[] { 0f, 0f });
            return view;
        }

        static SpriteRenderer BladeOf(BallView v) => v.transform.Find("BladePivot/Blade").GetComponent<SpriteRenderer>();

        [Test]
        public void BladesStretchOnlyTheCoreAndAreAtLeastSevenPixels()
        {
            var view = Arena(root, art, new BallLoadout(WeaponRegistry.Create(BladeRule.WeaponId)), new BallLoadout(WeaponRegistry.Create(AegisRule.WeaponId)));
            var ember = BladeOf(view.GetBallView(0));
            var mirror = BladeOf(view.GetBallView(1));
            Assert.That(ember.drawMode, Is.EqualTo(SpriteDrawMode.Sliced), "9-slice: the outline is never stretched");
            Assert.That(ember.size.y, Is.EqualTo(BallView.MinBladePixels), "thin sim blade (3) drawn 7 px: never squeezes the outline away");
            Assert.That(ember.size.x, Is.GreaterThanOrEqualTo(ember.sprite.border.x + ember.sprite.border.z), "long enough for both end caps");
            Assert.That(mirror.size.y, Is.EqualTo(Mathf.Max(BallView.MinBladePixels, Mathf.RoundToInt(view.Sim.Balls[1].Weapon.BladeThickness))), "thickness read from the sim");

            // A thicker shield: whole pixels, core stretched.
            view.Sim.Balls[1].Weapon.BladeThickness = 11.4f;
            typeof(ArenaView).GetMethod("Draw", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(view, new object[] { 0f, 0f });
            Assert.That(mirror.size.y, Is.EqualTo(11f));
            var bottom = mirror.transform.localPosition.y - mirror.sprite.pivot.y / mirror.sprite.rect.height * mirror.size.y;
            Assert.That(bottom, Is.EqualTo(Mathf.Round(bottom)).Within(0.001f), "blade edges on whole pixels");
            foreach (var h in new[] { 7, 8, 9, 11, 14 })
            {
                var b = BallView.BladePivotY * h - BallView.PivotOffset(h);   // distance from the axis down to the blade's bottom edge
                Assert.That(b, Is.EqualTo(Mathf.Round(b)).Within(0.001f), $"height {h}");
            }
        }

        [Test]
        public void MantisHasOneBladeUnlessTwinBlade()
        {
            var view = Arena(root, art, new BallLoadout(WeaponRegistry.Create(FangRule.WeaponId)),
                new BallLoadout(WeaponRegistry.Create(FangRule.WeaponId)).With(TraitRegistry.Create(TwinBladeTrait.TraitId, 1)));
            Assert.That(view.GetBallView(0).transform.Find("TwinPivot"), Is.Null, "one claw: no second blade is added by the art");
            Assert.That(view.GetBallView(1).transform.Find("TwinPivot"), Is.Not.Null, "Twin Blade still adds its second blade");
        }

        [Test]
        public void DisplayNamesChangeButIdsAndSimNamesDoNot()
        {
            var expected = new[] { ("blade", "EMBER"), ("fang", "MANTIS"), ("pike", "ANGLER"), ("brawler", "WRECKER"), ("volley", "SPARKLER"), ("venom", "SPORE"), ("aegis", "MIRROR"), ("rig", "HIVE") };
            foreach (var (id, name) in expected)
            {
                Assert.That(WeaponNames.Of(id), Is.EqualTo(name));
                Assert.That(WeaponRegistry.Get(id).Id, Is.EqualTo(id), "ids unchanged");
                foreach (var c in name) Assert.That(PixelFontData.IndexOf(c), Is.GreaterThanOrEqualTo(0));
            }
            Assert.That(WeaponNames.CoversAll(), Is.True);
            Assert.That(WeaponRegistry.Get("blade").DisplayName, Is.EqualTo("BLADE"), "the sim's own name is untouched");
            var view = Arena(root, art, new BallLoadout(WeaponRegistry.Create(BladeRule.WeaponId)), new BallLoadout(WeaponRegistry.Create(RigRule.WeaponId)));
            Assert.That(view.Hud.NameText(0).Text, Is.EqualTo("YOU EMBER"));
            Assert.That(view.Hud.NameText(1).Text, Is.EqualTo("FOE HIVE"));
        }

        [Test]
        public void OutlineFlashRecolorsExactlyTheSpritesOwnRim()
        {
            Assert.That(BallEdges.Source, Is.InstanceOf<SpriteOutlineEdgeSource>());
            var rimColor = new Color32(20, 18, 28, 255);
            foreach (var w in WeaponRegistry.All)
            {
                var ball = art.Get(w.Id).Ball;
                var src = ball.texture.GetPixels32();
                var mask = BallEdges.Source.Edge(ball, 1).texture.GetPixels32();
                Assert.That(mask.Length, Is.EqualTo(src.Length));
                var rim = 0;
                for (var i = 0; i < src.Length; i++)
                {
                    var isRim = src[i].a > 0 && src[i].r == rimColor.r && src[i].g == rimColor.g && src[i].b == rimColor.b;
                    if (isRim) rim++;
                    Assert.That(mask[i].a > 0, Is.EqualTo(isRim), $"{w.Id} pixel {i}: only the dark 1 px rim lights up, nothing outside the ball");
                }
                Assert.That(rim, Is.GreaterThan(60), $"{w.Id} has a rim");
                Assert.That(BallEdges.Source.Edge(ball, 2), Is.SameAs(BallEdges.Source.Edge(ball, 1)), "big-ball hit flash uses the same rim");
            }

            // Reflect: magenta on the rim of the ball hit back; body keeps its color.
            var view = Arena(root, art, new BallLoadout(WeaponRegistry.Create(BladeRule.WeaponId)), new BallLoadout(WeaponRegistry.Create(FangRule.WeaponId)));
            var v = view.GetBallView(1);
            v.FlashEdge(FxView.ReflectFlashSeconds, FxView.ReflectColor);
            typeof(ArenaView).GetMethod("Draw", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(view, new object[] { 0f, 0f });
            Assert.That(v.EdgeFlashRenderer.enabled, Is.True);
            Assert.That(v.EdgeFlashRenderer.sprite, Is.SameAs(BallEdges.Source.Edge(art.Get(FangRule.WeaponId).Ball, 1)));
            Assert.That((Color32)v.EdgeFlashRenderer.color, Is.EqualTo(FxView.ReflectColor));
            Assert.That(v.FullFlashRenderer.enabled, Is.False);
            Assert.That(v.EdgeFlashRenderer.bounds.size.x, Is.EqualTo(32f).Within(0.01f), "drawn over the ball, not a ring outside it");
        }
    }
}
