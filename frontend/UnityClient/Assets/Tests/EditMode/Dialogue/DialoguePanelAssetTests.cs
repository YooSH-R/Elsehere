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
        // 추천 문장은 [힌트]를 누르면 입력창에 바로 들어가므로 따로 카드를 두지 않는다.
        [Test]
        public void HelpColumn_HasOnlyTheWordCard()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath);
            Transform content = prefab.transform.Find("HelpPanel/Viewport/Content");

            Assert.That(content.Find("Guide"), Is.Null);
            Assert.That(content.Find("WordsCard"), Is.Not.Null);
            Assert.That(content.Find("HintCard"), Is.Null);
        }

        // 영어 표현 카드의 고친 문장 줄에는 문장 글자와 스피커가 하나씩만 있다. Builder를 다시 돌려도 글자가 늘지 않는다.
        [Test]
        public void ExpressionRow_HasOneSentenceBesideOneSpeaker()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath);
            Transform row = prefab.transform.Find(
                "Panel/UserTranslation/Viewport/Body/ExpressionRow"
            );

            int sentences = 0;
            int speakers = 0;
            foreach (Transform child in row)
            {
                sentences += child.name == "Expression" ? 1 : 0;
                speakers += child.name == "SpeakButton" ? 1 : 0;
            }
            Assert.That(sentences, Is.EqualTo(1));
            Assert.That(speakers, Is.EqualTo(1));
        }

        // 이름표 글자는 줄 높이가 아니라 글자 모양 기준(Midline)으로 가운데에 둔다. 줄 높이 기준이면 Jua 글자가 이름표 위로 뜬다.
        [Test]
        public void NpcName_IsCenteredOnItsChipByTheGlyphs()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath);
            var view = prefab.GetComponentInChildren<DialoguePanelView>(true);
            var name = (TMP_Text)
                new SerializedObject(view).FindProperty("npcNameText").objectReferenceValue;

            Assert.That(name.alignment, Is.EqualTo(TextAlignmentOptions.Midline));
            Assert.That(name.margin.y, Is.EqualTo(0f));
            Assert.That(name.margin.w, Is.EqualTo(0f));
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
        [Test]
        public void WordCard_KeepsTextAwayFromTheCardEdge()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath);
            Transform card = prefab.transform.Find("HelpPanel/Viewport/Content/WordsCard");

            RectOffset padding = card.GetComponent<VerticalLayoutGroup>().padding;
            Assert.That(padding.left, Is.GreaterThanOrEqualTo(40));
            Assert.That(padding.right, Is.GreaterThanOrEqualTo(40));
            Assert.That(padding.top, Is.GreaterThanOrEqualTo(30));
        }
    }
}
