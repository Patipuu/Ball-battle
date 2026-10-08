using TeamNet.Multiplayer.Core;
using NUnit.Framework;

namespace TeamNet.Multiplayer.Core.Tests
{
    /// <summary>
    /// Unit tests for invariant #1 (GenerationGuard). One test per stale condition
    /// (generation / teardown / scene-transition) plus the state-machine transitions,
    /// matching Godot MultiplayerManagerEOS.gd:39-82. Pure EditMode; no device/EOS.
    /// </summary>
    public class GenerationGuardTests
    {
        [Test]
        public void Fresh_operation_with_current_generation_is_not_stale()
        {
            var g = new GenerationGuard();
            int token = g.BeginOperation("create");

            Assert.IsFalse(g.IsStale(token, "create-result"));
        }

        [Test]
        public void Condition1_older_generation_is_stale_after_a_newer_op()
        {
            var g = new GenerationGuard();
            int firstToken = g.BeginOperation("op-a");
            g.BeginOperation("op-b"); // supersedes op-a

            Assert.IsTrue(g.IsStale(firstToken, "op-a-result"),
                "an op from a superseded generation must be dropped");
        }

        [Test]
        public void Condition2_teardown_active_makes_current_generation_stale()
        {
            var g = new GenerationGuard();
            int token = g.BeginOperation("op");
            g.BeginTeardown();

            Assert.IsTrue(g.IsStale(token, "op-result"),
                "matching generation is still stale while teardown is active");
        }

        [Test]
        public void Condition3_scene_transition_makes_current_generation_stale()
        {
            var g = new GenerationGuard();
            g.BeginOperation("op");
            g.PrepareSceneTransition("to-match"); // bumps generation + sets flag
            int currentToken = g.Generation;

            Assert.IsTrue(g.IsStale(currentToken, "op-result"),
                "matching generation is still stale while scene is transitioning");
        }

        [Test]
        public void BeginOperation_clears_a_prior_teardown()
        {
            var g = new GenerationGuard();
            g.BeginTeardown();
            int token = g.BeginOperation("fresh-op");

            Assert.IsFalse(g.TeardownActive, "teardown flag cleared by BeginOperation");
            Assert.IsFalse(g.IsStale(token, "fresh-op-result"));
        }

        [Test]
        public void CompleteSceneTransition_clears_flag_and_is_idempotent()
        {
            var g = new GenerationGuard();
            g.PrepareSceneTransition("to-match");

            Assert.IsTrue(g.CompleteSceneTransition(), "first complete returns true (was transitioning)");
            Assert.IsFalse(g.SceneTransitioning, "flag cleared");
            Assert.IsFalse(g.CompleteSceneTransition(), "second complete is a no-op (returns false)");
            Assert.IsFalse(g.IsStale(g.Generation, "op-after-transition"),
                "ops are accepted again once the transition completes");
        }

        [Test]
        public void PrepareSceneTransition_is_idempotent_and_bumps_generation_once()
        {
            var g = new GenerationGuard();
            int before = g.Generation;
            g.PrepareSceneTransition("to-match");
            int afterFirst = g.Generation;
            g.PrepareSceneTransition("to-match-again"); // already transitioning → no-op

            Assert.AreEqual(before + 1, afterFirst, "entering a transition bumps generation once");
            Assert.AreEqual(afterFirst, g.Generation, "a redundant PrepareSceneTransition does not bump again");
        }

        [Test]
        public void BumpGeneration_is_monotonic()
        {
            var g = new GenerationGuard();
            int a = g.BumpGeneration("a");
            int b = g.BumpGeneration("b");
            int c = g.BumpGeneration("c");

            Assert.AreEqual(a + 1, b);
            Assert.AreEqual(b + 1, c);
        }

        [Test]
        public void Invalidate_bumps_generation_and_is_terminal()
        {
            var g = new GenerationGuard();
            int oldToken = g.BeginOperation("old");
            int before = g.Generation;

            g.Invalidate("destroy");
            int attemptedFreshToken = g.BeginOperation("must-not-reopen");

            Assert.AreEqual(before + 1, g.Generation);
            Assert.AreEqual(g.Generation, attemptedFreshToken);
            Assert.IsTrue(g.Invalidated);
            Assert.IsTrue(g.IsStale(oldToken, "old-result"));
            Assert.IsTrue(g.IsStale(attemptedFreshToken, "new-result"),
                "BeginOperation must not reopen a terminally invalidated EOS lifecycle");
        }

        [Test]
        public void Injected_logger_receives_stale_diagnostics()
        {
            string last = null;
            var g = new GenerationGuard(msg => last = msg);
            int token = g.BeginOperation("op");
            g.BeginOperation("op2"); // supersede

            g.IsStale(token, "op-result");
            StringAssert.Contains("stale lobby operation", last,
                "stale detections are reported through the injected sink");
        }
    }
}
