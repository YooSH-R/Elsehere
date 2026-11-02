using System.Collections;
using System.Linq;
using A305.Dialogue;
using A305.Npc;
using A305.ObjectHover;
using A305.Player;
using A305.World;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace A305.Tutorial.PlayModeTests
{
    /// <summary>
    /// 배포용 숙소·마을 씬을 실제로 불러와 튜토리얼이 단계마다 장소 이동을 잠그고, 코치 마크로 문·Ben·버튼을 가리키며,
    /// Ben 길 안내 대화는 정해진 대사로만 넘어가는지 확인한다.
    /// </summary>
    public class TutorialFlowTests
    {
        private const string BenCode = "TOWN_PASSERBY";
        private const float WaitTimeoutSeconds = 15f;

        [SetUp]
        public void StartTutorialFromTheBeginning()
        {
            PasserbyRestRecord.Clear();
            TutorialState.Clear();
        }

        [TearDown]
        public void ClearTutorial()
        {
            TutorialState.Clear();
        }

        [UnityTest]
        public IEnumerator Skipping_AsksFirst_AndOnlyConfirmingEndsTheTutorial()
        {
            // 잘못 눌러도 튜토리얼이 바로 끝나지 않도록 한 번 더 묻는다.
            yield return SceneManager.LoadSceneAsync("HomeScene", LoadSceneMode.Single);
            yield return null;
            TutorialGuideView guideView = Single<TutorialGuideView>();
            TutorialProgress progress = TutorialState.GetOrCreate(BenCode);

            Click(guideView, "SkipButton");
            yield return null;

            Assert.That(guideView.IsSkipConfirmShowing, Is.True);
            Assert.That(progress.Step, Is.EqualTo(TutorialStep.Arrival));

            Click(guideView, "KeepGoingButton");
            yield return null;

            Assert.That(guideView.IsSkipConfirmShowing, Is.False);
            Assert.That(progress.Step, Is.EqualTo(TutorialStep.Arrival));

            Click(guideView, "SkipButton");
            Click(guideView, "ConfirmSkipButton");
            yield return null;

            Assert.That(progress.Step, Is.EqualTo(TutorialStep.Completed));
        }

        [UnityTest]
        public IEnumerator SaveWord_PinnedCardWithoutSaveButton_DoesNotSkipTheStep()
        {
            // 서버에 연결되지 않아 단어 카드에 [저장]이 없어도, 저장하지 않았으니 넘어가지 않는다.
            yield return SceneManager.LoadSceneAsync("HomeScene", LoadSceneMode.Single);
            yield return null;
            TutorialProgress progress = TutorialState.GetOrCreate(BenCode);
            progress.Continue();
            progress.Report(TutorialAction.Walk);
            progress.Report(TutorialAction.Run);
            progress.Continue();
            progress.Report(TutorialAction.Rotate);
            progress.Report(TutorialAction.Zoom);
            progress.Report(TutorialAction.ToggleView);
            progress.Continue();
            Assert.That(progress.Step, Is.EqualTo(TutorialStep.SaveWord), "준비: 단어 모으기");

            Single<WordTooltipView>()
                .ShowPinned(
                    new ObjectWord("table", "탁자", "Put it on the table."),
                    new Vector2(300f, 200f),
                    ScriptableObject.CreateInstance<ObjectHoverSettings>(),
                    WordCardButtons.None
                );
            for (int i = 0; i < 5; i++)
            {
                yield return null;
            }

            Assert.That(progress.Step, Is.EqualTo(TutorialStep.SaveWord));
        }

        [UnityTest]
        public IEnumerator MeetBen_HoldsThePlayerWhileBenOffers_ThenTheDialogueTakesOver()
        {
            // 튜토리얼에서 Ben이 [대화하기]를 제안하면 답할 때까지 멀어지지 않게 이동을 잠근다.
            yield return SceneManager.LoadSceneAsync("TownScene", LoadSceneMode.Single);
            yield return null;
            PlayerMover player = Single<PlayerMover>();
            yield return StandInFrontOf(FindBen());
            Assert.That(player.IsMovementLocked, Is.False, "Ben이 다가오는 동안에는 걸을 수 있습니다.");

            PasserbyOfferView offer = Single<PasserbyOfferView>();
            yield return WaitUntil(() => offer.IsShowing);
            yield return null;
            Assert.That(player.IsMovementLocked, Is.True);

            Click(offer, "AcceptButton");
            yield return null;
            yield return null;
            Assert.That(player.IsMovementLocked, Is.True, "대화가 잠금을 이어받습니다.");

            Click(Single<DialoguePanelView>(), "EndButton");
            yield return null;
            yield return null;
            Assert.That(player.IsMovementLocked, Is.False);
        }

        [UnityTest]
        public IEnumerator Lodging_KeepsTheDoorLockedUntilTheGuideIsDone_ThenPointsAtIt()
        {
            yield return SceneManager.LoadSceneAsync("HomeScene", LoadSceneMode.Single);
            yield return null;
            CoachMarkView coachMark = Single<CoachMarkView>();

            Assert.That(SpaceTravel.IsLocked, Is.True);
            Assert.That(SpaceTravel.TryTravel("TownScene", "HomeDoor"), Is.False);
            Assert.That(
                coachMark.SpotlightTargets,
                Does.Contain(Single<TutorialGuideView>().PanelRect),
                "도착 인사 카드를 밝힙니다."
            );

            TutorialProgress progress = TutorialState.GetOrCreate(BenCode);
            progress.Continue();
            progress.Report(TutorialAction.Walk);
            progress.Report(TutorialAction.Run);
            progress.Continue();
            progress.Report(TutorialAction.Rotate);
            progress.Report(TutorialAction.Zoom);
            progress.Report(TutorialAction.ToggleView);
            progress.Continue();
            progress.ReportWordSaved();
            progress.ReportVocabularyOpened();
            yield return null;
            yield return null;

            Assert.That(progress.Step, Is.EqualTo(TutorialStep.LeaveLodging));
            Assert.That(SpaceTravel.IsLocked, Is.False);
            Assert.That(coachMark.IsSpotlightShowing, Is.False);
            Assert.That(
                coachMark.MarkerTarget.GetComponent<SpaceDoorway>(),
                Is.Not.Null,
                "숙소 문 위에 화살표를 띄웁니다."
            );
        }

        [UnityTest]
        public IEnumerator Town_PointsAtBen_ThenHighlightsOnlyTheTalkOffer()
        {
            yield return SceneManager.LoadSceneAsync("TownScene", LoadSceneMode.Single);
            yield return null;
            CoachMarkView coachMark = Single<CoachMarkView>();
            Passerby ben = FindBen();

            Assert.That(
                SpaceTravel.IsLocked,
                Is.True,
                "Ben과 이야기하기 전에는 다른 장소로 가지 않습니다."
            );
            Assert.That(coachMark.MarkerTarget, Is.EqualTo(ben.transform));

            yield return StandInFrontOf(ben);
            PasserbyOfferView offer = Single<PasserbyOfferView>();
            yield return WaitUntil(() => offer.IsShowing);
            yield return null;

            Assert.That(coachMark.SpotlightTargets, Is.EqualTo(new[] { offer.ShownButtonRect }));
            Assert.That(coachMark.MarkerTarget, Is.Null);
        }

        [UnityTest]
        public IEnumerator BenDialogue_KeepsAWrongSentence_AndHighlightsTheEndButtonAfterTheLines()
        {
            yield return SceneManager.LoadSceneAsync("TownScene", LoadSceneMode.Single);
            yield return null;
            yield return StandInFrontOf(FindBen());
            PasserbyOfferView offer = Single<PasserbyOfferView>();
            yield return WaitUntil(() => offer.IsShowing);
            Click(offer, "AcceptButton");
            yield return null;

            DialoguePanelView panel = Single<DialoguePanelView>();
            DialogueController dialogue = Single<DialogueController>();
            CoachMarkView coachMark = Single<CoachMarkView>();
            TMP_InputField input = panel.GetComponentsInChildren<TMP_InputField>(true).Single();
            Assert.That(dialogue.IsTalking, Is.True);
            Assert.That(coachMark.SpotlightTargets, Does.Contain(input.transform));

            input.text = "Hello there";
            Click(panel, "SendButton");
            yield return null;

            Assert.That(
                input.text,
                Is.EqualTo("Hello there"),
                "고쳐 보낼 수 있게 입력을 되돌립니다."
            );
            Assert.That(
                panel
                    .GetComponentsInChildren<TMP_Text>(true)
                    .Any(text =>
                        text.gameObject.activeInHierarchy
                        && text.text.Contains(DialogueConversation.GuidedLineMissNotice)
                    ),
                Is.True,
                "정해진 대사로 연습한다는 안내가 보입니다."
            );

            while (!dialogue.IsGuidedCompleted)
            {
                Click(panel, "ExampleButton");
                Click(panel, "SendButton");
                yield return null;
            }

            yield return null;
            Assert.That(coachMark.SpotlightTargets, Is.EqualTo(new[] { panel.EndButtonRect }));
        }

        private static Passerby FindBen()
        {
            return Object
                .FindObjectsByType<Passerby>(FindObjectsSortMode.None)
                .Single(passerby => passerby.GetComponent<NpcIdentity>().Code == BenCode);
        }

        private static IEnumerator StandInFrontOf(Passerby passerby)
        {
            Single<PlayerMover>()
                .Teleport(
                    passerby.transform.position + passerby.transform.forward * 4f,
                    Quaternion.identity
                );
            yield return null;
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
