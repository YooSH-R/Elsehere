using A305.Characters;
using A305.EditorTools.Characters;
using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Tripo.Tests
{
    public sealed class ServiceNpcReviewTests
    {
        public static string[] NpcNames => ServiceNpcReviewBuilder.Names;

        [TestCaseSource(nameof(NpcNames))]
        public void ReviewedNpcKeepsMasterProportionsFaceAndMotion(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Npc/Review/" + name + ".prefab"
            );
            var master = TripoMasterWorkflow.RequireMaster();
            Assert.That(prefab, Is.Not.Null);
            Assert.That(
                CharacterHeadProportions.Ratio(prefab),
                Is.EqualTo(CharacterHeadProportions.Ratio(master)).Within(.001f)
            );
            Assert.That(CharacterFaceController.MissingChannels(prefab.transform, true), Is.Empty);
            Assert.That(TripoMasterRig.Inspect(prefab, master).Errors, Is.Empty);
            Assert.That(TripoMasterMotionAudit.Inspect(prefab).Errors, Is.Empty);
        }

        [TestCase("LittleMung_Cafe_Male")]
        [TestCase("LittleMung_Grandfather")]
        [TestCase("LittleMung_HotelStaff_Female")]
        [TestCase("LittleMung_Police_Female")]
        [TestCase("LittleMung_Police_Male")]
        public void GeneratedNpcIsIncludedInFaceAndMotionReview(string name)
        {
            Assert.That(ServiceNpcReviewBuilder.Names, Does.Contain(name));
        }
    }
}
