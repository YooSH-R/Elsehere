using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Npc.Tests
{
    /// <summary>
    /// NPC 클릭·탭으로 말 걸기는 NPC가 있는 장소마다 하나씩 놓이는 대화 제안 화면 프리팹에 붙어 있어,
    /// 장면을 고치지 않고도 모든 장소에서 동작한다.
    /// </summary>
    public sealed class NpcClickTalkPrefabTests
    {
        private const string PrefabPath = "Assets/Prefabs/Npc/PasserbyOffer.prefab";

        [Test]
        public void OfferPrefab_HasClickTalkWithHoverOutlineMaterial()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var clickTalk = prefab.GetComponent<NpcClickTalk>();

            Assert.That(clickTalk, Is.Not.Null, "제안 화면 프리팹에 NpcClickTalk가 없습니다.");
            var outline = (Material)
                new SerializedObject(clickTalk)
                    .FindProperty("outlineMaterial")
                    .objectReferenceValue;
            Assert.That(outline, Is.Not.Null, "커서를 올린 NPC에 그릴 테두리 재질이 없습니다.");
            Assert.That(outline.shader.name, Is.EqualTo("A305/NpcHoverOutline"));
            Assert.That(outline.shader.isSupported, Is.True);
        }
    }
}
