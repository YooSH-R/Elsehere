#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace A305.Dialogue.PlayModeTests
{
    /// <summary>
    /// 튜토리얼 길 안내처럼 정해진 대사를 따라가는 대화에서, 실제 대화창이 말할 대사를 빈 입력칸에 회색 안내로 미리 보이고
    /// [말하기]도 두는지 확인한다.
    /// </summary>
    public class DialoguePanelGuidedTests
    {
        private const string PanelPrefabPath = "Assets/Prefabs/Dialogue/DialoguePanel.prefab";

        private GameObject panelObject;
        private GameObject eventSystemObject;

        [TearDown]
        public void DestroyPanel()
        {
            Object.Destroy(panelObject);
            Object.Destroy(eventSystemObject);
        }

        [UnityTest]
        public IEnumerator GuidedLine_IsTheGreyGuideOfTheEmptyInput_AndCanBeSpoken()
        {
            var conversation = new DialogueConversation(
                "TUTORIAL_PASSERBY",
                GuidedScript(),
                hasRecorder: true
            );
            DialoguePanelView view = Show(conversation);
            yield return null;

            TMP_InputField input = panelObject
                .GetComponentsInChildren<TMP_InputField>(true)
                .Single();
            Assert.That(input.text, Is.Empty, "입력칸은 비워 두고 안내로만 보입니다.");
            Assert.That(((TMP_Text)input.placeholder).text, Is.EqualTo("Go straight ahead."));
            Assert.That(ButtonNamed("TalkButton").gameObject.activeSelf, Is.True);

            conversation.Send("Go straight ahead.");
            view.Refresh();
            Assert.That(
                ((TMP_Text)input.placeholder).text,
                Is.EqualTo("Turn left at the corner."),
                "다음 턴의 대사로 바뀝니다."
            );
        }

        private DialoguePanelView Show(IDialogueContent content)
        {
            eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            panelObject = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath)
            );
            var view = panelObject.GetComponentInChildren<DialoguePanelView>(true);
            view.Show(content);
            return view;
        }

        private Button ButtonNamed(string name) =>
            panelObject.GetComponentsInChildren<Button>(true).Single(button => button.name == name);

        private static DialogueScript GuidedScript()
        {
            return new DialogueScript(
                "Ben",
                DialogueLine.FromNpc(
                    "Excuse me! Where is the café?",
                    "실례합니다! 카페가 어디예요?"
                ),
                new List<GuidedTurn>
                {
                    new GuidedTurn(
                        "Go straight ahead.",
                        "앞으로 곧장 가세요.",
                        DialogueLine.FromNpc("And then?", "그다음엔요?")
                    ),
                    new GuidedTurn(
                        "Turn left at the corner.",
                        "모퉁이에서 왼쪽으로 도세요.",
                        DialogueLine.FromNpc("Thank you so much!", "정말 고마워요!")
                    ),
                },
                new List<KeywordReply>(),
                DialogueLine.FromNpc("I see.", "그렇군요."),
                ""
            );
        }
    }
}
#endif
