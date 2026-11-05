using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace A305.Npc.Tests
{
    /// <summary>
    /// NPC 머리 위 이름표는 대화 제안 화면 프리팹에 함께 들어 있어, 행인·직원이 있는 장면이면 따로 연결하지 않아도 나온다.
    /// 단어 카드(90)·HUD(95)보다 아래에 그려 이름표가 그 위를 덮지 않는다.
    /// </summary>
    public sealed class NpcNameTagPrefabTests
    {
        private const string PrefabPath = "Assets/Prefabs/Npc/PasserbyOffer.prefab";
        private const int WordCardSortingOrder = 90;

        [Test]
        public void OfferPrefab_HasNameTagLayerBelowWordCardsAndHud()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            NpcNameTagView view = prefab.GetComponentInChildren<NpcNameTagView>(true);

            Assert.That(view, Is.Not.Null, "대화 제안 화면에 이름표 층이 없습니다.");
            var canvas = view.GetComponent<Canvas>();
            Assert.That(canvas, Is.Not.Null);
            Assert.That(canvas.overrideSorting, Is.True);
            Assert.That(canvas.sortingOrder, Is.LessThan(WordCardSortingOrder));
        }

        [Test]
        public void NameTagLayer_ConnectsNamesLifeDataAndHiddenTemplate()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var view = new SerializedObject(prefab.GetComponentInChildren<NpcNameTagView>(true));

            Assert.That(view.FindProperty("names").objectReferenceValue, Is.Not.Null);
            Assert.That(view.FindProperty("lifeData").objectReferenceValue, Is.Not.Null);
            var template = (RectTransform)view.FindProperty("tagTemplate").objectReferenceValue;
            Assert.That(template, Is.Not.Null);
            Assert.That(template.gameObject.activeSelf, Is.False, "본보기 이름표는 숨겨 둡니다.");
            Assert.That(template.Find("Name")?.GetComponent<TMP_Text>(), Is.Not.Null);
            Assert.That(template.Find("RoleChip/Label")?.GetComponent<TMP_Text>(), Is.Not.Null);
        }
    }
}
