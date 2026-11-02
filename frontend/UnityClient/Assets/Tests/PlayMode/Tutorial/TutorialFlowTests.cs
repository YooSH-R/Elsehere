using System.Collections;
using System.Linq;
using A305.Dialogue;
using A305.Learning;
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
            progress.Report(TutorialAction.Jump);
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
        public IEnumerator MeetBen_FromArrival_HoldsThePlayerFacingBen_AndBenComesToOffer()
        {
            // 숙소를 나오자마자 다른 곳으로 걸어가지 못하게 이동·시점을 잠그고 Ben을 바라보는 구도로 붙잡아 둔다.
            // Ben은 이동이 잠긴 플레이어에게도, 감지 거리 밖에서도 다가와 말을 건다.
            yield return SceneManager.LoadSceneAsync("TownScene", LoadSceneMode.Single);
            yield return null;
            yield return null;
            PlayerMover player = Single<PlayerMover>();
            PlayerCameraRig cameraRig = Single<PlayerCameraRig>();
            Assert.That(player.IsMovementLocked, Is.True, "나오자마자 이동을 잠급니다.");
            Assert.That(
                cameraRig.IsInMeetingView,
                Is.True,
                "Ben을 기다리는 구도로 시점을 붙잡습니다."
            );

            PasserbyOfferView offer = Single<PasserbyOfferView>();
            yield return WaitUntil(() => offer.IsShowing);
            Vector3 toBen = Vector3.ProjectOnPlane(
                FindBen().transform.position - player.transform.position,
                Vector3.up
            );
            Assert.That(
                Vector3.Angle(player.transform.forward, toBen),
                Is.LessThan(20f),
                "플레이어는 Ben을 바라봅니다."
            );
        }

        [UnityTest]
        public IEnumerator MeetBen_TheDialogueTakesOverTheHold_AndFinishingTheGuideReleasesIt()
        {
            yield return SceneManager.LoadSceneAsync("TownScene", LoadSceneMode.Single);
            yield return null;
            PlayerMover player = Single<PlayerMover>();
            PlayerCameraRig cameraRig = Single<PlayerCameraRig>();
            PasserbyOfferView offer = Single<PasserbyOfferView>();
            yield return WaitUntil(() => offer.IsShowing);
            Click(offer, "AcceptButton");
            yield return null;
            yield return null;
            Assert.That(player.IsMovementLocked, Is.True, "대화가 잠금을 이어받습니다.");
            Assert.That(
                cameraRig.ViewMode,
                Is.EqualTo(CameraViewMode.Dialogue),
                "대화 중에는 대화 시점이 우선합니다."
            );

            DialoguePanelView panel = Single<DialoguePanelView>();
            DialogueController dialogue = Single<DialogueController>();
            while (!dialogue.IsGuidedCompleted)
            {
                Click(panel, "ExampleButton");
                Click(panel, "SendButton");
                yield return null;
            }

            Click(panel, "EndButton");
            yield return null;
            yield return null;
            Assert.That(
                cameraRig.IsInMeetingView,
                Is.False,
                "길 안내를 마치면 기다리는 구도를 풉니다."
            );
            FeedbackView feedback = Single<FeedbackView>();
            Assert.That(feedback.IsVisible, Is.True, "준비: 피드백");
            Assert.That(player.IsMovementLocked, Is.True, "피드백을 보는 동안은 잠겨 있습니다.");

            feedback
                .GetComponentsInChildren<Button>()
                .First(button => button.isActiveAndEnabled && button.name.Contains("Close"))
                .onClick.Invoke();
            yield return null;
            yield return null;
            Assert.That(player.IsMovementLocked, Is.False);
        }

        [UnityTest]
        public IEnumerator MeetBen_EndingTheDialogueEarly_KeepsThePlayerWithBen()
        {
            // 길 안내를 마치지 않고 대화를 끝내면 Ben을 다시 만나야 하므로 계속 붙잡아 둔다.
            yield return SceneManager.LoadSceneAsync("TownScene", LoadSceneMode.Single);
            yield return null;
            PlayerMover player = Single<PlayerMover>();
            PasserbyOfferView offer = Single<PasserbyOfferView>();
            yield return WaitUntil(() => offer.IsShowing);
            Click(offer, "AcceptButton");
            yield return null;

            Click(Single<DialoguePanelView>(), "EndButton");
            yield return null;
            yield return null;

            Assert.That(
                TutorialState.GetOrCreate(BenCode).Step,
                Is.EqualTo(TutorialStep.MeetBen),
                "준비: 다시 Ben 만나기"
            );
            Assert.That(player.IsMovementLocked, Is.True);
            Assert.That(Single<PlayerCameraRig>().IsInMeetingView, Is.True);
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
            progress.Report(TutorialAction.Jump);
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
        public IEnumerator OpeningTheVocabulary_ExplainsTheBook_WhileTheGuideCardIsHidden()
        {
            yield return SceneManager.LoadSceneAsync("HomeScene", LoadSceneMode.Single);
            yield return null;
            TutorialProgress progress = TutorialState.GetOrCreate(BenCode);
            progress.Continue();
            progress.Report(TutorialAction.Walk);
            progress.Report(TutorialAction.Run);
            progress.Report(TutorialAction.Jump);
            progress.Continue();
            progress.Report(TutorialAction.Rotate);
            progress.Report(TutorialAction.Zoom);
            progress.Report(TutorialAction.ToggleView);
            progress.Continue();
            progress.ReportWordSaved();
            Assert.That(
                progress.Step,
                Is.EqualTo(TutorialStep.OpenVocabulary),
                "준비: 단어장 열기"
            );

            // 피드백 화면에도 [단어장 보기]가 있어 오른쪽 위 메뉴의 [단어장]을 고른다.
            Object
                .FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(button =>
                    button.name == "VocabularyButton"
                    && !button
                        .GetComponentsInParent<Transform>(true)
                        .Any(parent => parent.name.Contains("Feedback"))
                )
                .onClick.Invoke();
            yield return null;
            yield return null;

            CoachMarkView coachMark = Single<CoachMarkView>();
            Assert.That(progress.Step, Is.EqualTo(TutorialStep.LeaveLodging));
            Assert.That(
                Single<TutorialGuideView>().IsVisible,
                Is.False,
                "단어장 위로 안내 카드는 숨깁니다."
            );
            Assert.That(coachMark.IsSpotlightShowing, Is.True, "단어장 창을 밝힙니다.");
            Assert.That(coachMark.CaptionText, Does.Contain("단어장"));
            Assert.That(coachMark.CaptionText, Does.Contain("밖으로"));
        }

        [UnityTest]
        public IEnumerator LockedDoor_ShowsTheNoticeOnTheGuideCard_ThenClearsIt()
        {
            // 화면 구석의 HUD 알림은 잘 보이지 않으므로, 할 일이 적힌 안내 카드에 바로 알린다.
            yield return SceneManager.LoadSceneAsync("HomeScene", LoadSceneMode.Single);
            yield return null;
            TutorialState.GetOrCreate(BenCode).Continue();
            yield return null;
            TutorialGuideView guideView = Single<TutorialGuideView>();
            Assert.That(guideView.IsVisible, Is.True, "준비: 걷기 안내 카드");

            Assert.That(SpaceTravel.TryTravel("TownScene", "HomeDoor"), Is.False);
            yield return null;

            Assert.That(guideView.IsNoticeShowing, Is.True);
            Assert.That(guideView.NoticeText, Does.Contain("갈 수 없어요"));

            yield return new WaitForSeconds(TutorialGuideView.NoticeSeconds + 0.2f);
            Assert.That(
                guideView.IsNoticeShowing,
                Is.False,
                "잠시 보인 뒤 할 일 한 줄만 남깁니다."
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
            yield return FinishTour(coachMark);
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

        [UnityTest]
        public IEnumerator BenDialogue_TeachesStepByStepWithNext_ThenKeepsTheLinesVisible()
        {
            // 대화를 시작하면 무엇부터 볼지 하나씩 어둡게 밝히고 옆 말풍선으로 설명하며 [다음]으로 넘긴다.
            // 다 보면 대사를 읽으며 할 수 있게 덮지 않고, 입력칸 옆에서 할 대사를 알린다.
            yield return SceneManager.LoadSceneAsync("TownScene", LoadSceneMode.Single);
            yield return null;
            PasserbyOfferView offer = Single<PasserbyOfferView>();
            yield return WaitUntil(() => offer.IsShowing);
            Click(offer, "AcceptButton");
            yield return null;
            yield return null;

            DialoguePanelView panel = Single<DialoguePanelView>();
            CoachMarkView coachMark = Single<CoachMarkView>();
            RectTransform[][] tour =
            {
                new[] { panel.LinesRect },
                panel.ToolParts.ToArray(),
                new[] { panel.SuggestedWordsRect },
                new[] { panel.SuggestedSentenceRect },
            };
            foreach (RectTransform[] targets in tour)
            {
                Assert.That(coachMark.SpotlightTargets, Is.EqualTo(targets));
                Assert.That(coachMark.IsDimmingOutside, Is.True, "하나씩 밝혀 볼 곳을 알립니다.");
                Assert.That(coachMark.IsNextShowing, Is.True);
                Assert.That(
                    coachMark.CaptionRect.Overlaps(coachMark.Hole),
                    Is.False,
                    "설명은 밝힌 곳을 가리지 않고 옆에 둡니다."
                );
                ClickNext(coachMark);
                yield return null;
                yield return null;
            }

            TMP_InputField input = panel.GetComponentsInChildren<TMP_InputField>(true).Single();
            Assert.That(coachMark.SpotlightTargets, Does.Contain(input.transform));
            Assert.That(coachMark.IsDimmingOutside, Is.False, "대사가 보이게 덮지 않습니다.");
            Assert.That(coachMark.IsNextShowing, Is.False);
            Assert.That(coachMark.CaptionText, Does.Contain("정해진 흐름"));
            Assert.That(coachMark.CaptionRect.Overlaps(coachMark.Hole), Is.False);
        }

        // 대화 시작 티칭을 [다음]으로 끝까지 넘긴다.
        private static IEnumerator FinishTour(CoachMarkView coachMark)
        {
            for (int step = 0; step < TutorialCoachPlan.DialogueTourLength; step++)
            {
                ClickNext(coachMark);
                yield return null;
                yield return null;
            }
        }

        private static void ClickNext(CoachMarkView coachMark)
        {
            coachMark
                .GetComponentsInChildren<Button>()
                .Single(button => button.name == "NextButton")
                .onClick.Invoke();
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
