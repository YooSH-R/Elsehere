using System.Linq;
using A305.EditorTools.Common;
using A305.Learning;
using A305.Onboarding;
using A305.Theme;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace A305.World.Tests
{
    /// <summary>
    /// 장소마다 HUD 버튼(단어장·대화 노트·내 정보·홈)은 오른쪽 위 메뉴(≡) 목록 안에 있고, 집에서만 [테마]가 켜져 테마 고르기를 연다.
    /// </summary>
    public sealed class HudMenuConnectionTests
    {
        private const string HudPrefabPath = "Assets/Prefabs/Learning/LearningHud.prefab";

        [TestCase("PoliceStationScene")]
        public void ProductionSceneHudKeepsItsButtonsInTheClosedMenu(string name)
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Production/" + name + ".unity");
            var hud = BuilderTools.FindSingleInScene<LearningHudView>(scene);
            Assert.That(hud, Is.Not.Null);
            var menuView = hud.GetComponent<HudMenuView>();
            Assert.That(menuView, Is.Not.Null, name + ": 메뉴(≡)가 없습니다.");
            Assert.DoesNotThrow(() => BuilderTools.RequireReferences(menuView));

            var items = (GameObject)
                new SerializedObject(menuView).FindProperty("items").objectReferenceValue;
            Assert.That(items.activeSelf, Is.False, name + ": 메뉴는 접힌 채 시작해야 합니다.");
            var fields = new SerializedObject(hud);
            foreach (string button in new[] { "vocabularyButton", "historyButton", "homeButton" })
            {
                var target = (Button)fields.FindProperty(button).objectReferenceValue;
                Assert.That(
                    target.transform.parent,
                    Is.EqualTo(items.transform),
                    name + ": " + button
                );
            }
            Assert.That(items.transform.Find("MyPageButton"), Is.Not.Null, name);
            Assert.That(
                hud.transform.Find("Panel/Buttons"),
                Is.Null,
                name + ": 예전 버튼 줄이 남아 있습니다."
            );

            Assert.That(
                items.transform.Find("ThemeButton"),
                Is.Null,
                name + ": 화면 디자인은 하나라 메뉴에 [테마]를 두지 않습니다."
            );
        }

        // 이 창은 지난 대화의 피드백을 다시 열고 같은 상황을 다시 연습하는 곳이라(피드백 목록과 복습장을 합침) 앱과 창 제목을 [대화 노트]로 부른다.
        [TestCase("Panel/Menu/HistoryButton/Label")]
        [TestCase("History/Title")]
        public void HudNamesThePastConversationWindowConversationNote(string path)
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);

            var label = hud.transform.Find(path).GetComponent<TMP_Text>();

            Assert.That(label.text, Is.EqualTo("대화 노트"));
        }

        [Test]
        public void HudKeepsOneSaveMessage()
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);

            int messages = hud.GetComponentsInChildren<Transform>(true)
                .Count(t => t.name == "Message");

            Assert.That(messages, Is.EqualTo(1));
        }

        // 설정 창은 선택 동의 제목 바로 아래에 목록을 두며, HUD Builder를 다시 실행해도 이 순서를 지킨다.
        [Test]
        public void SettingsKeepTheConsentListBelowItsLabel()
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            Transform window = hud.transform.Find("Settings/Window");

            int label = window.Find("ConsentsLabel").GetSiblingIndex();
            int consents = window.Find("Consents").GetSiblingIndex();

            Assert.That(consents, Is.EqualTo(label + 1));
        }

        // 조작법 카드는 오른쪽 아래에 두고, X로 닫은 뒤에는 메뉴(≡)의 [조작법]으로 다시 연다.
        [TestCase("PoliceStationScene")]
        public void ProductionSceneHudShowsTheControlsGuideAtTheBottomRight(string name)
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Production/" + name + ".unity");
            var hud = BuilderTools.FindSingleInScene<LearningHudView>(scene);
            var guide = hud.GetComponent<ControlsGuideView>();
            Assert.That(guide, Is.Not.Null, name + ": 조작법 카드가 없습니다.");
            Assert.DoesNotThrow(() => BuilderTools.RequireReferences(guide));
            Assert.That(
                new SerializedObject(hud).FindProperty("controlsGuide").objectReferenceValue,
                Is.EqualTo(guide),
                name
            );

            var fields = new SerializedObject(guide);
            var card = (RectTransform)
                ((GameObject)fields.FindProperty("card").objectReferenceValue).transform;
            Assert.That(card.anchorMin, Is.EqualTo(new Vector2(1f, 0f)), name);
            Assert.That(card.anchorMax, Is.EqualTo(new Vector2(1f, 0f)), name);

            var open = (Button)fields.FindProperty("openButton").objectReferenceValue;
            var items = (GameObject)
                new SerializedObject(hud.GetComponent<HudMenuView>())
                    .FindProperty("items")
                    .objectReferenceValue;
            Assert.That(open.transform.parent, Is.EqualTo(items.transform), name);
            Assert.That(
                open.GetComponentInChildren<TMP_Text>(true).text,
                Is.EqualTo(ControlsGuideText.MenuLabel),
                name
            );
        }

        // 메뉴를 펼치면 바깥 영역이 카드를 덮어 카드를 눌러도 메뉴가 접히고, 대화 노트·내 정보 창은 카드 위에 보인다.
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
