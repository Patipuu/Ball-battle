using BallBattle.Sim;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    /// <summary>Proves the Sim sources compile and run outside Unity.</summary>
    public class SmokeTests
    {
        [Test]
        public void SimSourcesAreLinked()
        {
            Assert.That(SimVersion.Rules, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void SimAssemblyHasNoUnityEngineReference()
        {
            foreach (var reference in typeof(SimVersion).Assembly.GetReferencedAssemblies())
                Assert.That(reference.Name, Does.Not.StartWith("UnityEngine"));
        }
    }
}
