using System.Linq;
using A305.EditorTools.Common;
using A305.Onboarding;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.World.Tests
{
    /// <summary>
    /// HUD 프리팹의 문구·메뉴 순서·접속 만료 안내 연결을 확인한다.
    /// </summary>
    public sealed class HudMenuConnectionTests
    {
        private const string HudPrefabPath = "Assets/Prefabs/Learning/LearningHud.prefab";

        // 이 창은 지난 대화를 고르고 그때 받은 피드백을 다시 여는 곳이라 메뉴와 창 제목을 [피드백 목록]으로 부른다.
        [TestCase("Panel/Menu/HistoryButton/Label")]
        [TestCase("History/Title")]
        public void HudNamesThePastFeedbackWindowFeedbackList(string path)
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);

            var label = hud.transform.Find(path).GetComponent<TMP_Text>();

            Assert.That(label.text, Is.EqualTo("피드백 목록"));
        }

        [Test]
        public void HudKeepsOneSaveMessage()
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);

            int messages = hud.GetComponentsInChildren<Transform>(true)
                .Count(t => t.name == "Message");

            Assert.That(messages, Is.EqualTo(1));
        }

        // 내 정보 창은 선택 동의 제목과 목록을 버튼 줄 위에 두며, HUD Builder를 다시 실행해도 이 순서를 지킨다.
        [Test]
        public void ProfileKeepsConsentsAboveItsButtons()
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            Transform window = hud.transform.Find("MyPage/Window");

            int label = window.Find("ConsentsLabel").GetSiblingIndex();
            int consents = window.Find("Consents").GetSiblingIndex();
            int buttons = window.Find("Buttons").GetSiblingIndex();

            Assert.That(label, Is.LessThan(consents));
            Assert.That(consents, Is.LessThan(buttons));
        }

        // 메뉴를 펼치면 바깥 영역이 카드를 덮어 카드를 눌러도 메뉴가 접히고, 피드백 목록·내 정보 창은 카드 위에 보인다.
        [Test]
        public void ControlsGuideSitsBehindTheMenuBackdropAndWindows()
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath).transform;

            int card = hud.Find("ControlsGuide").GetSiblingIndex();

            Assert.That(card, Is.LessThan(hud.Find("MenuBackdrop").GetSiblingIndex()));
            Assert.That(card, Is.LessThan(hud.Find("History").GetSiblingIndex()));
            Assert.That(card, Is.LessThan(hud.Find("MyPage").GetSiblingIndex()));
        }

        // 로그인이 풀리면(서버 재배포·오래 쉼) 어느 창 위에서도 보이도록, 안내 창은 수첩(1000)보다 높은 순서의 캔버스에 두고 닫힌 채 시작한다.
        [Test]
        public void HudTellsWhenTheLoginSessionExpires()
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);

            var view = hud.GetComponent<SessionExpiredView>();

            Assert.That(view, Is.Not.Null, "접속이 끊겼을 때 알리는 창이 없습니다.");
            Assert.DoesNotThrow(() => BuilderTools.RequireReferences(view));
            var overlay = (GameObject)
                new SerializedObject(view).FindProperty("overlay").objectReferenceValue;
            Assert.That(overlay.activeSelf, Is.False);
            var canvas = overlay.GetComponent<Canvas>();
            Assert.That(canvas, Is.Not.Null);
            Assert.That(canvas.overrideSorting, Is.True);
            Assert.That(canvas.sortingOrder, Is.GreaterThan(1000));
            Assert.That(overlay.GetComponent<GraphicRaycaster>(), Is.Not.Null);
        }
    }
}
