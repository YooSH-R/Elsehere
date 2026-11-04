using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Learning.Tests
{
    /// <summary>
    /// 설문 카드는 장소마다 들어 있는 HUD 프리팹에 있어 씬마다 배치하지 않는다. 화면 위 가운데에 붙고, 가입 안내 창 같은 덮개(대화 제안 화면 110)보다
    /// 위, 로그인 끊김 안내(2000)보다 아래에 그린다.
    /// </summary>
    public sealed class SurveyPromptPrefabTests
    {
        private const string PrefabPath = "Assets/Prefabs/Learning/LearningHud.prefab";

        [Test]
        public void Hud_HasHiddenSurveyCardAtTheTopCenter()
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Transform prompt = hud.transform.Find("SurveyPrompt");
            Assert.That(prompt, Is.Not.Null, "HUD에 설문 카드가 없습니다.");
            var view = prompt.GetComponent<SurveyPromptView>();
            Assert.That(view, Is.Not.Null);

            var fields = new SerializedObject(view);
            var card = (RectTransform)fields.FindProperty("card").objectReferenceValue;
            Assert.That(card, Is.Not.Null);
            Assert.That(card.gameObject.activeSelf, Is.False, "설문 카드는 숨긴 채로 시작합니다.");
            Assert.That(card.anchorMin, Is.EqualTo(new Vector2(0.5f, 1f)));
            Assert.That(card.anchorMax, Is.EqualTo(new Vector2(0.5f, 1f)));
            Assert.That(fields.FindProperty("openButton").objectReferenceValue, Is.Not.Null);
            Assert.That(fields.FindProperty("closeButton").objectReferenceValue, Is.Not.Null);
        }

        [Test]
        public void SurveyCard_DrawsAboveDialogOverlaysAndBelowSessionNotice()
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var canvas = hud.transform.Find("SurveyPrompt").GetComponent<Canvas>();

            Assert.That(canvas, Is.Not.Null);
            Assert.That(canvas.overrideSorting, Is.True);
            Assert.That(canvas.sortingOrder, Is.GreaterThan(110));
            Assert.That(canvas.sortingOrder, Is.LessThan(2000));
        }
    }
}
