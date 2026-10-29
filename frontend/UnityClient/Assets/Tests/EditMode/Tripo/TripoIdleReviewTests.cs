using System;
using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Tripo.Tests
{
    public class TripoIdleReviewTests
    {
        [Test]
        public void ActualIdleClipIsAvailableInPreviewAndAutomaticAudit()
        {
            Assert.That(
                Enum.TryParse<TripoReviewMotion>("Idle", out var idle),
                Is.True,
                "Rest pose is not the actual standing animation."
            );
            Assert.That(
                TripoMotionPreview.LoadPlayerClip(idle),
                Is.EqualTo(
                    AssetDatabase.LoadAssetAtPath<AnimationClip>(
                        "Assets/Art/Player/LittleMung/Idle.anim"
                    )
                )
            );
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(
                TripoMasterWorkflow.VisualTemplate
            );
            var report = TripoMasterMotionAudit.Inspect(model);
            Assert.That(report.ManualChecks, Has.Some.Contains("Idle"));
        }
    }
}
