using System.Collections;
using System.Linq;
using A305.Dialogue;
using A305.Learning;
using A305.Tutorial;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace A305.Npc.PlayModeTests
{
    /// <summary>
    /// 대화를 다 쓴 게스트에게 가입을 권하되, 튜토리얼 피드백에서는 설문을 권하지 않는다.
    /// 설문 안내 기회는 다음 일반 대화 피드백을 닫을 때까지 남겨 둔다.
    /// </summary>
    /// <remarks>
    /// 서버 없이 피드백까지 가는 대화는 튜토리얼의 Ben 길 안내뿐이라 그 대화로 확인한다.
    /// 남은 대화 수는 대화가 끝날 때 서버(AI-01)에서 다시 받으므로, 테스트에서는 받은 것처럼 넣는다.
    /// </remarks>
    public class GuestSignupAfterFeedbackTests
    {
        private const float WaitTimeoutSeconds = 15f;
        private const int MaxGuidedTurns = 20;
        private bool hadSurveyInvitation;
        private int savedSurveyInvitation;

        [SetUp]
        public void StartWithNoSurveyInvitation()
        {
            hadSurveyInvitation = PlayerPrefs.HasKey(A305.Api.SurveyPrompt.InvitationKey);
            savedSurveyInvitation = PlayerPrefs.GetInt(A305.Api.SurveyPrompt.InvitationKey);
            PlayerPrefs.DeleteKey(A305.Api.SurveyPrompt.InvitationKey);
        }

        [TearDown]
        public void RestoreSurveyInvitation()
        {
            if (hadSurveyInvitation)
                PlayerPrefs.SetInt(A305.Api.SurveyPrompt.InvitationKey, savedSurveyInvitation);
            else
                PlayerPrefs.DeleteKey(A305.Api.SurveyPrompt.InvitationKey);
        }

        [SetUp]
        [TearDown]
        public void StartTutorialFromTheBeginning()
        {
            PasserbyRestRecord.Clear();
            PasserbyOfferRecord.Clear();
            TutorialState.Clear();
        }

        [UnityTest]
        public IEnumerator GuestOutOfConversations_TutorialFeedbackOpensSignupWithoutSurvey()
        {
            yield return FinishConversationWithBen();
            Single<DialogueController>().Allowance.Apply(GuestStatus(remaining: 0));

            yield return CloseFeedback();

            Assert.That(Single<GuestSignupDialogView>().IsVisible, Is.True);
            Assert.That(
                Single<SurveyPromptView>().IsShowing,
                Is.False,
                "튜토리얼 피드백은 설문 안내 횟수에 포함하지 않습니다."
            );
            Assert.That(PlayerPrefs.HasKey(A305.Api.SurveyPrompt.InvitationKey), Is.False);
            Assert.That(A305.Api.SurveyPrompt.RequestAfterFeedback(true), Is.True);
            yield return null;
            Assert.That(Single<SurveyPromptView>().IsShowing, Is.True);
            Assert.That(A305.Api.SurveyPrompt.RequestAfterFeedback(true), Is.False);
        }

        [UnityTest]
        public IEnumerator GuestWithConversationsLeft_ClosingFeedback_DoesNotOpenSignup()
        {
            yield return FinishConversationWithBen();
            Single<DialogueController>().Allowance.Apply(GuestStatus(remaining: 1));

            yield return CloseFeedback();

            Assert.That(Single<GuestSignupDialogView>().IsVisible, Is.False);
        }

        private static IEnumerator FinishConversationWithBen()
        {
            yield return SceneManager.LoadSceneAsync("TownScene", LoadSceneMode.Single);
            yield return null;
            PasserbyOfferView offer = Single<PasserbyOfferView>();
            yield return WaitUntil(() => offer.IsShowing);
            Click(offer, "AcceptButton");
            yield return null;

            DialoguePanelView panel = Single<DialoguePanelView>();
            DialogueController dialogue = Single<DialogueController>();
            for (int turn = 0; turn < MaxGuidedTurns && !dialogue.IsGuidedCompleted; turn++)
            {
                Click(panel, "HintButton");
                Click(panel, "UseHintButton");
                Click(panel, "SendButton");
                yield return null;
            }

            Assert.That(dialogue.IsGuidedCompleted, Is.True, "준비: 길 안내를 마칩니다.");
            Click(panel, "EndButton");
            yield return null;
            yield return null;
            Assert.That(Single<FeedbackView>().IsVisible, Is.True, "준비: 피드백");
        }

        private static IEnumerator CloseFeedback()
        {
            Single<FeedbackView>()
                .GetComponentsInChildren<Button>()
                .First(button => button.isActiveAndEnabled && button.name.Contains("Close"))
                .onClick.Invoke();
            yield return null;
            yield return null;
        }

        private static AiStatusData GuestStatus(int remaining)
        {
            return new AiStatusData
            {
                Status = remaining > 0 ? "AI_AVAILABLE" : "AI_EXHAUSTED",
                Capabilities = new AiCapabilities { CanStartConversation = remaining > 0 },
                RemainingConversations = remaining,
            };
        }

        private static IEnumerator WaitUntil(System.Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + WaitTimeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Assert.Fail("기다리는 상태가 되지 않았습니다.");
                }

                yield return null;
            }
        }

        private static void Click(Component view, string buttonName)
        {
            Button button = view.GetComponentsInChildren<Button>(true)
                .Single(candidate => candidate.name == buttonName);
            Assert.That(
                button.gameObject.activeInHierarchy,
                Is.True,
                $"{buttonName}이 보이지 않습니다."
            );
            button.onClick.Invoke();
        }

        private static T Single<T>()
            where T : Object
        {
            T[] found = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            Assert.That(found, Has.Length.EqualTo(1), $"{typeof(T).Name} 개수");
            return found[0];
        }
    }
}
