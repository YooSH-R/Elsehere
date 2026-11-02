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
