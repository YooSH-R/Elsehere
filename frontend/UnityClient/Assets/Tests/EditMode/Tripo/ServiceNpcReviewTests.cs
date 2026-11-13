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
        // 별도 준비 경로를 쓰는 Business와 공통 생성 경로의 여성 캐릭터를 대표로 검사한다.
        [TestCase("LittleMung_Business_Male")]
        [TestCase("LittleMung_HotelStaff_Female")]
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
    }
}
