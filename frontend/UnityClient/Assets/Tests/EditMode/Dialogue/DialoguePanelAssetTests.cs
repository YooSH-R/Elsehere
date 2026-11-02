using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Dialogue.Tests
{
    public class DialoguePanelAssetTests
    {
        private const string PanelPrefabPath = "Assets/Prefabs/Dialogue/DialoguePanel.prefab";

        // 도움 칸은 배경 없이 장면 위에 놓여, 카드 밖의 안내 글씨는 읽을 수 없다. 할 일은 튜토리얼 설명이 위쪽에서 알린다.
        [Test]
        public void HelpColumn_HasOnlyTheWordAndSentenceCards()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath);
            Transform content = prefab.transform.Find("HelpPanel/Viewport/Content");

            Assert.That(content.Find("Guide"), Is.Null);
            Assert.That(content.Find("WordsCard"), Is.Not.Null);
            Assert.That(content.Find("HintCard"), Is.Not.Null);
        }

        // 줄바꿈이 꺼져 있으면 긴 NPC 대사가 대화창 오른쪽 끝에서 잘린다.
        [Test]
        public void LongNpcLines_WrapInsideThePanel()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath);
            var view = prefab.GetComponentInChildren<DialoguePanelView>(true);

            var lines = (TMP_Text)
                new SerializedObject(view).FindProperty("linesText").objectReferenceValue;

            Assert.That(lines.textWrappingMode, Is.EqualTo(TextWrappingModes.Normal));
        }

        // 대화창 Builder만 다시 돌리면 승인된 여백(44·36)이 14·16으로 줄어 글자가 카드 테두리에 붙어 보였다.
        [TestCase("WordsCard")]
        [TestCase("HintCard")]
        public void HelpCards_KeepTextAwayFromTheCardEdge(string cardName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath);
            Transform card = prefab.transform.Find("HelpPanel/Viewport/Content/" + cardName);

            RectOffset padding = card.GetComponent<VerticalLayoutGroup>().padding;
            Assert.That(padding.left, Is.GreaterThanOrEqualTo(40));
            Assert.That(padding.right, Is.GreaterThanOrEqualTo(40));
            Assert.That(padding.top, Is.GreaterThanOrEqualTo(30));
        }
    }
}
