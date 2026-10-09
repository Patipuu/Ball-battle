using System.Collections.Generic;
using BallBattle.View;
using NUnit.Framework;
using UnityEngine;

namespace BallBattle.Tests
{
    public class MatchFlowTests
    {
        [Test]
        public void FullSeriesWalksEveryState()
        {
            var flow = new MatchFlow(3f, 2f);
            var prepared = new List<uint>();
            int began = 0, finished = 0;
            flow.RoundPrepared += (a, b, seed) => prepared.Add(seed);
            flow.RoundBegan += () => began++;
            flow.SeriesFinished += () => finished++;

            Assert.That(flow.Current, Is.EqualTo(MatchFlow.State.Menu));
            flow.Begin("blade", "fang", 99);
            Assert.That(flow.Current, Is.EqualTo(MatchFlow.State.Countdown));
            Assert.That(flow.CountdownNumber, Is.EqualTo(3));
            flow.Tick(1.2f);
            Assert.That(flow.CountdownNumber, Is.EqualTo(2));
            flow.Tick(2f);
            Assert.That(flow.Current, Is.EqualTo(MatchFlow.State.Playing));
            Assert.That(began, Is.EqualTo(1));

            flow.OnRoundEnded(0);
            Assert.That(flow.Current, Is.EqualTo(MatchFlow.State.RoundOver));
            flow.OnRoundEnded(1);   // ignored outside Playing
            Assert.That(flow.Series.WinsB, Is.EqualTo(0));
            flow.Tick(2.1f);
            Assert.That(flow.Current, Is.EqualTo(MatchFlow.State.Countdown));

            flow.Tick(3.1f);
            flow.OnRoundEnded(-1);  // draw: replayed
            flow.Tick(2.1f);
            flow.Tick(3.1f);
            flow.OnRoundEnded(0);
            flow.Tick(2.1f);
            Assert.That(flow.Current, Is.EqualTo(MatchFlow.State.Result));
            Assert.That(finished, Is.EqualTo(1));
            Assert.That(flow.Series.Winner, Is.EqualTo(0));
            Assert.That(prepared.Count, Is.EqualTo(3));
            Assert.That(new HashSet<uint>(prepared).Count, Is.EqualTo(3), "each round (incl. replayed draw) gets its own seed");
        }

        [Test]
        public void RematchReusesSeedsNewMatchDoesNot()
        {
            var flow = new MatchFlow();
            var seeds = new List<uint>();
            flow.RoundPrepared += (a, b, seed) => seeds.Add(seed);

            flow.Begin("pike", "brawler", 1234);
            var firstRoundSeed = seeds[0];
            FinishSeries(flow);

            flow.Rematch();
            Assert.That(seeds[seeds.Count - 1], Is.EqualTo(firstRoundSeed), "rematch starts from the same round seed");
            FinishSeries(flow);

            flow.NewMatch(5678);
            Assert.That(seeds[seeds.Count - 1], Is.Not.EqualTo(firstRoundSeed), "new match uses a new seed");
            Assert.That(flow.Series.WeaponA, Is.EqualTo("pike"));
            Assert.That(flow.Series.MatchSeed, Is.EqualTo(5678u));
        }

        [Test]
        public void ToMenuRaisesEvent()
        {
            var flow = new MatchFlow();
            var shown = 0;
            flow.MenuShown += () => shown++;
            flow.Begin("blade", "pike", 1);
            flow.ToMenu();
            Assert.That(flow.Current, Is.EqualTo(MatchFlow.State.Menu));
            Assert.That(shown, Is.EqualTo(1));
        }

        [Test]
        public void ScreenToNativeMatchesWindowboxZoom()
        {
            // Exact fit 1080x1920 → zoom 4, centre maps to (0,0), corners to ±(135,240).
            Assert.That(PixelInput.Zoom(1080, 1920), Is.EqualTo(4));
            Assert.That(PixelInput.ScreenToNative(new Vector2(540, 960), 1080, 1920), Is.EqualTo(Vector2.zero));
            Assert.That(PixelInput.ScreenToNative(new Vector2(0, 0), 1080, 1920), Is.EqualTo(new Vector2(-135, -240)));

            // Tall phone 1080x2400 → zoom 4 with 240 px bands top and bottom.
            Assert.That(PixelInput.ScreenToNative(new Vector2(540, 1200), 1080, 2400), Is.EqualTo(Vector2.zero));
            Assert.That(PixelInput.ScreenToNative(new Vector2(0, 240), 1080, 2400), Is.EqualTo(new Vector2(-135, -240)));

            // Landscape editor window 1920x1080 → zoom 2, pillarboxed.
            Assert.That(PixelInput.Zoom(1920, 1080), Is.EqualTo(2));
            Assert.That(PixelInput.ScreenToNative(new Vector2(960, 540), 1920, 1080), Is.EqualTo(Vector2.zero));
            Assert.That(PixelInput.ScreenToNative(new Vector2(960 + 2, 540 + 4), 1920, 1080), Is.EqualTo(new Vector2(1, 2)));
        }

        static void FinishSeries(MatchFlow flow)
        {
            for (var guard = 0; guard < 10 && flow.Current != MatchFlow.State.Result; guard++)
            {
                flow.Tick(10f);              // countdown -> playing
                flow.OnRoundEnded(0);
                flow.Tick(10f);              // round over -> next countdown or result
            }
            Assert.That(flow.Current, Is.EqualTo(MatchFlow.State.Result));
        }

        [Test]
        public void CommandsInTheWrongStateAreIgnored()
        {
            var flow = new MatchFlow();
            var prepared = 0;
            flow.RoundPrepared += (a, b, s) => prepared++;
            flow.Rematch();                       // nothing to rematch
            flow.NewMatch(1);
            Assert.That(prepared, Is.EqualTo(0));

            flow.Begin("blade", "fang", 3);
            Assert.That(prepared, Is.EqualTo(1));
            flow.Begin("pike", "pike", 4);        // stale START while counting down
            flow.Rematch();                       // stale REMATCH mid-series
            Assert.That(prepared, Is.EqualTo(1));
            Assert.That(flow.Series.WeaponA, Is.EqualTo("blade"));
        }
    }
}