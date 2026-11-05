#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using A305.Api;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace A305.Learning.PlayModeTests
{
    /// <summary>
    /// 설문을 요청하면(<see cref="SurveyPrompt.Request"/>) HUD의 설문 카드가 화면 위에서 내려와 제자리에 멈추고, [닫기]로 사라진다.
    /// HUD 전체 대신 설문 카드 부분만 띄워 확인한다. [설문 하러 가기]는 브라우저를 열므로 누르지 않는다.
    /// </summary>
    public class SurveyPromptTests
    {
        private GameObject prompt;

        [SetUp]
        public void PlaceSurveyCard()
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Learning/LearningHud.prefab"
            );
            prompt = Object.Instantiate(hud.transform.Find("SurveyPrompt").gameObject);
        }

        [TearDown]
        public void RemoveSurveyCard() => Object.Destroy(prompt);

        [UnityTest]
        public IEnumerator Request_SlidesTheCardDownToItsPlace()
        {
            yield return null;
            var view = prompt.GetComponent<SurveyPromptView>();
            Assert.That(view.IsShowing, Is.False);

            SurveyPrompt.Request();

            Assert.That(view.IsShowing, Is.True);
            Assert.That(view.CardOffset, Is.GreaterThan(0f), "화면 위에서 시작합니다.");
            yield return new WaitForSecondsRealtime(SurveyPromptSlide.Duration + 0.1f);
            Assert.That(view.CardOffset, Is.EqualTo(0f));
        }

        [UnityTest]
        public IEnumerator Close_HidesTheCard()
        {
            yield return null;
            var view = prompt.GetComponent<SurveyPromptView>();
            SurveyPrompt.Request();
            yield return null;

            prompt
                .GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "CloseButton")
                .onClick.Invoke();

            Assert.That(view.IsShowing, Is.False);
        }
    }
}
#endif
