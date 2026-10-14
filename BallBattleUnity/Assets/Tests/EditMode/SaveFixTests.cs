using System.Text;
using BallBattle.Sim;
using BallBattle.Sim.Run;
using BallBattle.View;
using NUnit.Framework;

namespace BallBattle.Tests
{
    /// <summary>
    /// 6a.6b/c: the save keeps its JSON field names (version 2; BB-04 made it 3 by adding boss fields, the fixture below is a v3 save
    /// with the old float values, so the double → Fix conversion is still exercised); RunSave / RunBuildSave / FightRecordSave hold the converted
    /// fields as double. double → Fix on load: nearest, ties away from zero (SaveFix.ToFix); Fix → double on save: exact.
    /// Tests use MemorySaveStore only (never the real save file).
    /// </summary>
    public class SaveFixTests
    {
        /// <summary>A save as the float build wrote it (JsonUtility prints floats in their shortest form), at version 3 with BB-04's boss slots.</summary>
        const string V2FloatSave =
            "{\"Version\":3,\"Progress\":{\"Achievements\":[\"boss1\"],\"RunsPlayed\":3,\"RunsWon\":0,\"BestFight\":4}," +
            "\"HasRun\":true,\"Run\":{\"SeedBits\":-123456789,\"FightIndex\":2,\"RetryCount\":0,\"IsRetry\":false,\"StartPickPending\":false,\"Coins\":7," +
            "\"Build\":{\"WeaponId\":\"blade\",\"Traits\":[{\"Id\":\"vampire\",\"Level\":2}],\"DamagePct\":0.15,\"SpeedPct\":0.1,\"MaxHp\":120.0,\"Hp\":63.27,\"Radius\":0.0}," +
            "\"RerollCount\":1,\"PicksTaken\":0,\"PickedMask\":0,\"OfferCards\":[{\"Kind\":1,\"Id\":\"\",\"Level\":0}]," +
            "\"Weapons\":[\"blade\",\"fang\"],\"Traits\":[\"vampire\"],\"EnemyWeapons\":[\"blade\",\"fang\"],\"EnemyTraits\":[\"vampire\"],\"Achievements\":[]," +
            "\"History\":[{\"FightIndex\":0,\"Won\":true,\"Draw\":false,\"Retry\":false,\"EnemyWeapon\":\"fang\",\"HpAfter\":37.5}," +
            "{\"FightIndex\":1,\"Won\":true,\"Draw\":false,\"Retry\":false,\"EnemyWeapon\":\"blade\",\"HpAfter\":0.1}]," +
            "\"FightLocked\":false,\"Over\":false,\"Won\":false,\"BossIds\":[\"shellking\",\"mimic\"],\"ForcedBossId\":\"\",\"ForcedBossFight\":-1}," +
            "\"musicVol\":6,\"sfxVol\":8,\"oldRunNotice\":false}";

        [Test]
        public void OldFloatV2SaveLoadsToNearestFix()
        {
            var d = new MemorySaveStore { Json = V2FloatSave }.Load();
            Assert.That(d.Version, Is.EqualTo(4));
            Assert.That(d.HasRun, Is.True);
            var run = d.Run.ToRun();
            Assert.That(run.Build.DamagePct.Raw, Is.EqualTo(9830), "0.15 × 65536 = 9830.4");
            Assert.That(run.Build.SpeedPct.Raw, Is.EqualTo(6554), "0.1 × 65536 = 6553.6");
            Assert.That(run.Build.MaxHp.Raw, Is.EqualTo(120 << 16));
            Assert.That(run.Build.Hp.Raw, Is.EqualTo(4146463), "63.27 × 65536 = 4146462.72");
            Assert.That(run.Build.Radius, Is.EqualTo(Fix.Zero));
            Assert.That(run.History[0].HpAfter.Raw, Is.EqualTo(2457600), "37.5 exactly");
            Assert.That(run.History[1].HpAfter.Raw, Is.EqualTo(6554));
            Assert.That((run.SeedBits, run.FightIndex, run.Coins, run.RerollCount), Is.EqualTo((-123456789, 2, 7, 1)));
            Assert.That(run.Build.Traits[0].Id + run.Build.Traits[0].Level, Is.EqualTo("vampire2"));
            Assert.That(run.OfferCards[0].Kind, Is.EqualTo((CardKind)1));
            Assert.That((d.musicVol, d.sfxVol), Is.EqualTo((6, 8)));
        }

        [Test]
        public void ResavingIsStableAfterTheFirstLoad()
        {
            var fixture = new MemorySaveStore { Json = V2FloatSave };
            var a = new MemorySaveStore();
            a.Save(fixture.Load());
            var b = new MemorySaveStore();
            b.Save(new MemorySaveStore { Json = a.Json }.Load());
            Assert.That(Encoding.UTF8.GetBytes(b.Json), Is.EqualTo(Encoding.UTF8.GetBytes(a.Json)), "A and B byte-identical");
            StringAssert.Contains("\"DamagePct\":", a.Json);
            StringAssert.Contains("\"HpAfter\":37.5", a.Json);
            StringAssert.Contains("\"Version\":4", a.Json);
        }

        [Test]
        public void RunContinuedAfterSaveLoadMatchesAnUninterruptedRun()
        {
            var progress = new ProgressData();
            const uint seed = 4242u;
            RunState Fresh() => RunState.Start(seed, RunState.StartChoices(seed, UnlockRules.Weapons(progress))[0], progress);

            var straight = Fresh();
            var saved = Fresh();
            foreach (var r in new[] { straight, saved })
            {
                for (var i = 0; i < r.OfferCount; i++) if (r.CanPick(i)) { r.Pick(i); break; }
                var m = r.CreateMatch();
                while (m.Outcome == MatchOutcome.Ongoing) m.Step();
                r.ApplyResult(m);
            }

            var store = new MemorySaveStore();
            store.Save(new SaveData { Progress = progress, HasRun = true, Run = RunSave.From(saved) });
            var resumed = store.Load().Run.ToRun();
            Assert.That(resumed.Build.Hp, Is.EqualTo(straight.Build.Hp));
            Assert.That(resumed.Build.MaxHp, Is.EqualTo(straight.Build.MaxHp));

            var x = straight.CreateMatch();
            var y = resumed.CreateMatch();
            while (x.Outcome == MatchOutcome.Ongoing) x.Step();
            while (y.Outcome == MatchOutcome.Ongoing) y.Step();
            Assert.That(y.ComputeHash(), Is.EqualTo(x.ComputeHash()), "same hash after save/load");
            Assert.That(y.Tick, Is.EqualTo(x.Tick));
        }

        [Test]
        public void FixDoubleFixRoundTripIsIdentity()
        {
            var v = Fix.FromRaw((1000 << 16) + 1);   // 1000 + 1/65536
            Assert.That(SaveFix.ToFix(SaveFix.ToDouble(v)), Is.EqualTo(v));
            foreach (var raw in new[] { 1, -1, 32767 << 16, int.MaxValue, int.MinValue + 1, 6554, 4146463 })
                Assert.That(SaveFix.ToFix(SaveFix.ToDouble(Fix.FromRaw(raw))).Raw, Is.EqualTo(raw));

            // Through the JSON too.
            var run = new RunState();
            run.Build.WeaponId = "blade";
            run.Build.MaxHp = Fix.FromInt(1200);
            run.Build.Hp = v;
            run.History.Add(new FightRecord { HpAfter = v });
            var store = new MemorySaveStore();
            store.Save(new SaveData { HasRun = true, Run = RunSave.From(run) });
            var back = store.Load().Run.ToRun();
            Assert.That(back.Build.Hp, Is.EqualTo(v));
            Assert.That(back.History[0].HpAfter, Is.EqualTo(v));
        }

        [Test]
        public void ToFixRoundsHalfAwayFromZero()
        {
            Assert.That(SaveFix.ToFix(0.5 / 65536).Raw, Is.EqualTo(1));
            Assert.That(SaveFix.ToFix(-0.5 / 65536).Raw, Is.EqualTo(-1));
            Assert.That(SaveFix.ToFix(1.4 / 65536).Raw, Is.EqualTo(1));
            Assert.That(SaveFix.ToFix(double.NaN), Is.EqualTo(Fix.Zero));
            Assert.That(SaveFix.ToFix(1e9), Is.EqualTo(Fix.Zero), "out of range: a corrupted save");
        }
    }
}
