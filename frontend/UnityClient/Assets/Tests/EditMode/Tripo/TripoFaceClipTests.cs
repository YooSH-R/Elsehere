using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Tripo.Tests
{
    public class TripoFaceClipTests
    {
        [Test]
        public void ExistingClip_MapsShapeCurveToLogicalControl_RejectsIncompatibleClip()
        {
            var clip = new AnimationClip();
            try
            {
                AnimationUtility.SetEditorCurve(
                    clip,
                    EditorCurveBinding.FloatCurve(
                        "OldFace",
                        typeof(SkinnedMeshRenderer),
                        "blendShape.SourceSmile"
                    ),
                    AnimationCurve.Linear(0f, 0f, 1f, 100f)
                );
                var bindings = new[]
                {
                    new TripoFaceBinding
                    {
                        control = "mouthSmileLeft",
                        shapeName = "SourceSmile",
                        rendererPath = "NewFace",
                    },
                };
                var weights = TripoFaceClip.Evaluate(clip, 0.5f, bindings);
                Assert.That(weights["mouthSmileLeft"], Is.EqualTo(50f).Within(0.001f));
                bindings[0].shapeName = "AnotherShape";
                Assert.Throws<System.InvalidOperationException>(() =>
                    TripoFaceClip.Evaluate(clip, 0.5f, bindings)
                );
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }
    }
}
