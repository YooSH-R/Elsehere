using System.Linq;
using A305.EditorTools.Common;
using A305.Learning;
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
    /// 장소마다 HUD 버튼(단어장·피드백 목록·내 정보·홈)은 오른쪽 위 메뉴(≡) 목록 안에 있고, 집에서만 [테마]가 켜져 테마 고르기를 연다.
    /// </summary>
    public sealed class HudMenuConnectionTests
    {
        private const string HudPrefabPath = "Assets/Prefabs/Learning/LearningHud.prefab";

        [TestCase("HomeScene")]
        [TestCase("TownScene")]
        [TestCase("CafeScene")]
        [TestCase("HotelScene")]
        [TestCase("ShopScene")]
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

            bool themeShown = items.transform.Find("ThemeButton").gameObject.activeSelf;
            Assert.That(
                themeShown,
                Is.EqualTo(name == "HomeScene"),
                name + ": [테마]는 집에서만 보입니다."
            );
        }

        [Test]
        public void HomeThemeMenuItemOpensThePicker()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Production/HomeScene.unity");
            var hud = BuilderTools.FindSingleInScene<LearningHudView>(scene);
            var picker = BuilderTools.FindSingleInScene<ThemePickerView>(scene);
            var open = (Button)
                new SerializedObject(picker).FindProperty("openButton").objectReferenceValue;

            Assert.That(open, Is.Not.Null);
            Assert.That(open.name, Is.EqualTo("ThemeButton"));
            Assert.That(open.transform.IsChildOf(hud.transform), Is.True);
            Assert.That(picker.transform.Find("OpenButton").gameObject.activeSelf, Is.False);
        }

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
    }
}
