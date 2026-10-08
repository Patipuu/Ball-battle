using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace BallBattle.Tests
{
    /// <summary>Guards the Phase 1 project configuration so later changes cannot silently undo it.</summary>
    public class ProjectSetupTests
    {
        [Test]
        public void UrpIsActiveRenderPipeline()
        {
            Assert.IsNotNull(GraphicsSettings.defaultRenderPipeline, "No default render pipeline assigned");
            StringAssert.Contains("Universal", GraphicsSettings.defaultRenderPipeline.GetType().Name);
        }

        [Test]
        public void EveryQualityLevelUsesUrp()
        {
            // Read each level without switching the active one (no editor-state side effects).
            for (var i = 0; i < QualitySettings.names.Length; i++)
            {
                var rp = QualitySettings.GetRenderPipelineAssetAt(i);
                Assert.IsTrue(rp == null || rp == GraphicsSettings.defaultRenderPipeline,
                    $"Quality level '{QualitySettings.names[i]}' overrides the render pipeline");
            }
        }

        [Test]
        public void SimAssemblyHasNoUnityEngineReference()
        {
            var sim = typeof(BallBattle.Sim.SimVersion).Assembly;
            foreach (var reference in sim.GetReferencedAssemblies())
                StringAssert.DoesNotStartWith("UnityEngine", reference.Name,
                    "BallBattle.Sim must stay pure C# (testable from dotnet SimTests)");
        }
    }
}
