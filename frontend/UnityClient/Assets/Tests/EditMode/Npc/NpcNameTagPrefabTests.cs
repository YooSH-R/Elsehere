using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Npc.Tests
{
    /// <summary>
    /// NPC 머리 위 이름표는 대화 제안 화면 프리팹에 함께 들어 있어, 행인·직원이 있는 장면이면 따로 연결하지 않아도 나온다.
    /// 단어 카드(90)·HUD(95)보다 아래에 그려 이름표가 그 위를 덮지 않는다.
    /// 이름표 바탕 전체가 클릭·탭을 받아 그 NPC에게 말을 걸고(기능 명세 F-22), 휴대폰 터치 조작판보다 아래에 있어
    /// 이름표 위에서 시작한 끌기는 조작판이 받는다.
    /// </summary>
    public sealed class NpcNameTagPrefabTests
    {
        private const string PrefabPath = "Assets/Prefabs/Npc/PasserbyOffer.prefab";
        private const string TouchControlsPath = "Assets/Prefabs/Player/TouchControls.prefab";
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

        [Test]
        public void NameTagLayer_CatchesClicksOnTheWholeTag()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            NpcNameTagView layer = prefab.GetComponentInChildren<NpcNameTagView>(true);
            var view = new SerializedObject(layer);
            var template = (RectTransform)view.FindProperty("tagTemplate").objectReferenceValue;

            Assert.That(
                layer.GetComponent<GraphicRaycaster>(),
                Is.Not.Null,
                "따로 정렬하는 이름표 층은 자기 캔버스에서 클릭을 찾아야 합니다."
            );
            Assert.That(
                template.GetComponent<Image>().raycastTarget,
                Is.True,
                "이름표 바탕 전체가 클릭을 받아야 합니다."
            );
        }

        [Test]
        public void NameTagLayer_IsBelowTouchControls_SoDragsStartingOnATagStillTurnTheView()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var touchControls = AssetDatabase.LoadAssetAtPath<GameObject>(TouchControlsPath);
            Canvas nameTags = prefab
                .GetComponentInChildren<NpcNameTagView>(true)
                .GetComponent<Canvas>();

            Assert.That(
                nameTags.sortingOrder,
                Is.LessThan(touchControls.GetComponent<Canvas>().sortingOrder)
            );
        }
    }
}
