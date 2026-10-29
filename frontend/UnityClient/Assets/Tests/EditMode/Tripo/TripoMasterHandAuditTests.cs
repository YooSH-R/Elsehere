using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Tripo.Tests
{
    public class TripoMasterHandAuditTests
    {
        private const string Source =
            "Assets/Art/Characters/Tripo/LittleMung_HotelStaff_male_336ca328/Source.fbx";

        [Test]
        public void PreviousHotelPalmStretchFailsEvenWhenSkeletonMatches()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            var old = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Characters/Tripo/LittleMung_HotelStaff_IntegratedReview/MasterRig/Visual.prefab"
            );
            Assert.That(
                TripoMasterRig
                    .Inspect(
                        old,
                        AssetDatabase.LoadAssetAtPath<GameObject>(TripoMasterRig.MasterPath)
                    )
                    .Passed,
                Is.True
            );
            Assert.That(
                TripoMasterHandAudit.Inspect(source, old).Errors,
                Has.Some.Contains("손바닥")
            );
        }

        [Test]
        public void PreparedHotelPalmIsPreservedAndSourceRemainsUnchanged()
        {
            const string folder = "Assets/__TripoHandAuditTest";
            byte[] original = System.IO.File.ReadAllBytes(Source);
            try
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
                var model = TripoMasterRig.Build(
                    source,
                    AssetDatabase.LoadAssetAtPath<GameObject>(TripoMasterRig.MasterPath),
                    folder
                );
                Assert.That(TripoMasterHandAudit.Inspect(source, model).Errors, Is.Empty);
                Assert.That(System.IO.File.ReadAllBytes(Source), Is.EqualTo(original));
            }
            finally
            {
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
