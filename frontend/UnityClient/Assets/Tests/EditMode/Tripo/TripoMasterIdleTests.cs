using A305.EditorTools.Player;
using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Tripo.Tests
{
    public class TripoMasterIdleTests
    {
        [Test]
        public void MasterPipelineUsesPreparedWeightsAndIdleInPreviewAndAudit()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Characters/Tripo/LittleMung_Casual_Male_41b6e942_Review/Source.fbx"
            );
            var master = AssetDatabase.LoadAssetAtPath<GameObject>(TripoMasterRig.MasterPath);
            var model = TripoMasterRig.Build(source, master, Folder);
            var controller = model.GetComponentInChildren<Animator>().runtimeAnimatorController;
            Assert.That(controller, Is.TypeOf<AnimatorOverrideController>());
            var expected = TripoTorsoWeights.Prepare(
                source.GetComponentInChildren<SkinnedMeshRenderer>(),
                source.GetComponentInChildren<Animator>()
            );
            Assert.That(
                model.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh.boneWeights,
                Is.EqualTo(expected)
            );
            var visual = TripoMasterWorkflow.BuildVisual(model, Folder + "/Visual.prefab");
            Assert.That(TripoMasterRig.Inspect(visual, master).Errors, Is.Empty);
            var instance = Object.Instantiate(visual);
            try
            {
                using var preview = new TripoMotionPreview(instance);
                preview.Apply(TripoReviewMotion.Idle, .5f);
                Assert.That(
                    preview.ActiveClip,
                    Is.SameAs(((AnimatorOverrideController)controller)["Idle"])
                );
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
            Assert.That(TripoMasterMotionAudit.Inspect(visual).Errors, Is.Empty);
        }

        private const string Folder = "Assets/TripoIdleTest";

        [TearDown]
        public void Cleanup() => AssetDatabase.DeleteAsset(Folder);

        [Test]
        public void PreparedIdleRelaxesShouldersWithoutChangingSourceOrLocomotion()
        {
            AssetDatabase.CreateFolder("Assets", "TripoIdleTest");
            var original = TripoMotionPreview.LoadPlayerClip(TripoReviewMotion.Idle);
            var binding = EditorCurveBinding.FloatCurve(
                "",
                typeof(Animator),
                "Left Shoulder Down-Up"
            );
            float before = AnimationUtility.GetEditorCurve(original, binding).Evaluate(.5f);
            var controller = TripoMasterAnimation.Build(Folder);
            Assert.That(controller, Is.TypeOf<AnimatorOverrideController>());
            var prepared = ((AnimatorOverrideController)controller)[original];
            Assert.That(prepared, Is.Not.SameAs(original));
            Assert.That(
                AnimationUtility.GetEditorCurve(prepared, binding).Evaluate(.5f),
                Is.EqualTo(0f).Within(.001f)
            );
            Assert.That(
                AnimationUtility.GetEditorCurve(original, binding).Evaluate(.5f),
                Is.EqualTo(before)
            );
            foreach (
                var motion in new[]
                {
                    TripoReviewMotion.Walk,
                    TripoReviewMotion.Run,
                    TripoReviewMotion.Rise,
                    TripoReviewMotion.Fall,
                    TripoReviewMotion.Land,
                }
            )
            {
                var clip = TripoMotionPreview.LoadPlayerClip(motion);
                Assert.That(((AnimatorOverrideController)controller)[clip], Is.SameAs(clip));
            }
        }
    }
}
