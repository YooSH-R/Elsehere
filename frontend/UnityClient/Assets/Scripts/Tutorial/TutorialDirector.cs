using System.Collections.Generic;
using System.Threading.Tasks;
using A305.Api;
using A305.Dialogue;
using A305.Learning;
using A305.Mission;
using A305.Npc;
using A305.ObjectHover;
using A305.Player;
using A305.World;
using UnityEngine;

namespace A305.Tutorial
{
    /// <summary>
    /// 공간 씬마다 하나씩 두는 튜토리얼 진행 처리. 걷기·시점·단어 저장·단어장·공간 이동·대화·피드백·지도 신호를 튜토리얼 진행에 전하고,
    /// 단계에 맞게 안내 카드와 코치 마크, 행인이 다가와도 되는지, 장소 이동·택시 잠금, 미션 표시를 바꾼다.
    /// 튜토리얼을 끝내면 서버에 저장하고(TUT-01·02), 저장하지 못했으면 다음 장소에 들어갈 때 다시 보낸다.
    /// </summary>
    public class TutorialDirector : MonoBehaviour
    {
        // 문 위·Ben 머리 위 화살표 높이(발밑·문 바닥 기준).
        private const float DoorMarkerHeightMeters = 2.6f;
        private const float BenMarkerHeightMeters = 2.3f;

        [SerializeField]
        private TutorialGuideDefinition guide;

        [SerializeField]
        private TutorialGuideView guideView;

        [SerializeField]
        private CoachMarkView coachMark;

        [SerializeField]
        private PlayerMover player;

        [SerializeField]
        private WordTooltipView wordTooltip;

        [SerializeField]
        private DialogueController dialogue;

        [SerializeField]
        private DialoguePanelView dialoguePanel;

        [SerializeField]
        private PasserbyOfferView offerView;

        [SerializeField]
        private LearningController learning;

        [SerializeField]
        private LearningHudView hud;

        [SerializeField]
        private HudMenuView hudMenu;

        [SerializeField]
        private FeedbackView feedbackView;

        [SerializeField]
        private MissionController mission;

        [SerializeField]
        private FirstDayMissionView firstDayMission;

        [SerializeField]
        [Tooltip("이 씬에 놓인 행인. 공간 기능 연결 도구가 채운다.")]
        private Passerby[] passersby = new Passerby[0];

        [SerializeField]
        [Tooltip(
            "튜토리얼을 끝낸 기록을 저장할 API 서버 설정. 비우면 저장하지 않는다(서버 없이 씬만 확인할 때)."
        )]
        private ApiSettings apiSettings;

        private TutorialProgress progress;
        private TutorialFinishRecorder finishRecorder;
        private TutorialStep appliedStep;
        private bool isCardShown;
        private TutorialStep cardStep;
        private bool cardCanContinue;
        private Passerby ben;
        private SpaceDoorway exitDoor;
        private TaxiRide[] taxis;
        private TutorialCoachTarget shownCoachTarget;
        private Object shownCoachObject;

        private void Awake()
        {
            if (
                guide == null
                || guideView == null
                || coachMark == null
                || player == null
                || wordTooltip == null
                || dialogue == null
                || dialoguePanel == null
                || offerView == null
                || learning == null
                || hud == null
                || hudMenu == null
                || feedbackView == null
                || mission == null
                || firstDayMission == null
            )
            {
                Debug.LogError("TutorialDirector의 참조가 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }

            progress = TutorialState.GetOrCreate(guide.TutorialNpcCode);
            if (apiSettings != null)
            {
                finishRecorder = new TutorialFinishRecorder(
                    new TutorialApi(apiSettings.CreateClient())
                );
            }

            ben = FindBen();
            exitDoor = FindExitDoor();
            taxis = FindInThisScene<TaxiRide>();
            if (gameObject.scene.name != guide.HomeSceneName)
            {
                progress.LeaveHome();
            }

            // 행인은 첫 Update에서 다가오기 시작하므로, 모든 Awake가 끝나기 전에 허용 여부를 정해 둔다.
            ApplyStep();

            dialogue.Started += progress.StartDialogue;
            dialogue.ConversationEnded += EndDialogue;
            learning.FeedbackClosed += CloseFeedback;
            wordTooltip.SaveRequested += progress.ReportWordSaved;
            hud.VocabularyRequested += progress.ReportVocabularyOpened;
            firstDayMission.JournalOpened += progress.ReportJournalOpened;
            guideView.ContinueRequested += progress.Continue;
            guideView.SkipRequested += progress.Skip;
            SpaceTravel.Refused += ShowTravelLocked;
        }

        private void OnDestroy()
        {
            if (progress == null)
            {
                return;
            }

            dialogue.Started -= progress.StartDialogue;
            dialogue.ConversationEnded -= EndDialogue;
            learning.FeedbackClosed -= CloseFeedback;
            wordTooltip.SaveRequested -= progress.ReportWordSaved;
            hud.VocabularyRequested -= progress.ReportVocabularyOpened;
            firstDayMission.JournalOpened -= progress.ReportJournalOpened;
            SpaceTravel.Refused -= ShowTravelLocked;
        }

        private void Update()
        {
            if (player.IsMoving)
            {
                progress.ReportWalked();
            }

            if (player.CameraRig != null && player.CameraRig.TurnedByUserThisFrame)
            {
                progress.ReportLooked();
            }

            // 서버 단어 카드가 없거나 이미 저장한 단어라 [저장]을 누를 수 없으면 카드를 고정한 것으로 넘어간다.
            if (wordTooltip.IsPinned && !wordTooltip.CanSaveWord)
            {
                progress.ReportWordSaved();
            }

            if (progress.Step != appliedStep)
            {
                ApplyStep();
            }

            RefreshCard();
            RefreshCoachMark();
        }

        private void EndDialogue(DialogueRecord record)
        {
            progress.EndDialogue(record.NpcCode, record.IsGuidedCompleted);
        }

        private void CloseFeedback(string npcCode, bool isGuidedCompleted)
        {
            progress.CloseFeedback(npcCode);
        }

        private void ShowTravelLocked()
        {
            hud.ShowMessage(guide.TravelLockedMessage);
        }

        private void ApplyStep()
        {
            appliedStep = progress.Step;
            // 튜토리얼을 끝낸 순간과, 저장하지 못한 채 다음 장소에 들어온 순간(Awake에서 적용) 모두 여기서 보낸다.
            if (appliedStep == TutorialStep.Completed)
            {
                SaveFinish();
            }

            foreach (Passerby passerby in passersby)
            {
                string npcCode = passerby.GetComponent<NpcIdentity>().Code;
                passerby.IsApproachAllowed = progress.IsApproachAllowed(npcCode);
                if (npcCode == guide.TutorialNpcCode)
                {
                    // 튜토리얼 중에는 길 안내 대본으로 말하고, 마치면 평소 행인으로 돌아간다.
                    passerby.SetTutorialScript(progress.IsGuiding ? guide.TutorialScriptKey : null);
                }
            }

            SpaceTravel.SetLocked(!progress.IsTravelAllowed);
            foreach (TaxiRide taxi in taxis)
            {
                taxi.IsHailAllowed = !progress.IsGuiding;
            }

            mission.SetStoryAvailable(progress.IsMissionAvailable);
            mission.SetPanelVisible(progress.IsMissionAvailable);
        }

        private void SaveFinish()
        {
            if (finishRecorder != null && progress.NeedsFinishSave)
            {
                _ = SaveFinishAsync();
            }
        }

        private async Task SaveFinishAsync()
        {
            TutorialFinishSaveStatus status = await finishRecorder.SaveAsync(progress);
            if (
                status == TutorialFinishSaveStatus.Failed
                || status == TutorialFinishSaveStatus.Rejected
            )
            {
                Debug.LogWarning(
                    $"튜토리얼을 끝낸 기록을 저장하지 못했습니다({status}, {finishRecorder.LastError.Code}): "
                        + finishRecorder.LastError.Message,
                    this
                );
            }
        }

        // 대화 제안·대화·피드백·단어장 화면을 가리지 않도록 그동안에는 카드를 숨기고 코치 마크 설명으로 할 일을 알린다.
        // 내용이 바뀔 때만 다시 그린다.
        private void RefreshCard()
        {
            bool hasGuide = guide.TryGetStepGuide(progress.Step, out TutorialStepGuide stepGuide);
            bool shouldShow =
                hasGuide
                && !dialogue.IsTalking
                && !offerView.IsShowing
                && !learning.IsShowingScreen;
            if (!shouldShow)
            {
                if (isCardShown)
                {
                    guideView.Hide();
                    isCardShown = false;
                }

                return;
            }

            bool isChanged =
                !isCardShown
                || cardStep != progress.Step
                || cardCanContinue != progress.CanContinue;
            if (!isChanged)
            {
                return;
            }

            guideView.Show(stepGuide, progress.CanContinue);
            isCardShown = true;
            cardStep = progress.Step;
            cardCanContinue = progress.CanContinue;
        }

        private void RefreshCoachMark()
        {
            var screen = new TutorialScreen
            {
                IsTalking = dialogue.IsTalking,
                IsTalkOfferShowing =
                    ben != null && (offerView.IsShownBy(ben) || offerView.IsTalkPromptShownBy(ben)),
                IsGuidedCompleted = dialogue.IsGuidedCompleted,
                IsWordCardPinned = wordTooltip.IsPinned,
                CanSaveWord = wordTooltip.CanSaveWord,
                IsMenuOpen = hudMenu.IsOpen,
                IsLearningScreenShowing = learning.IsShowingScreen,
                IsFeedbackShowing = feedbackView.IsVisible,
                IsJournalOpen = firstDayMission.IsJournalOpen,
            };
            TutorialCoachTarget target = TutorialCoachPlan.Choose(
                progress.Step,
                progress.CanContinue,
                screen
            );
            Object targetObject = CoachObject(target);
            if (target == shownCoachTarget && targetObject == shownCoachObject)
            {
                return;
            }

            shownCoachTarget = target;
            shownCoachObject = targetObject;
            switch (target)
            {
                case TutorialCoachTarget.Door when exitDoor != null:
                    coachMark.ShowMarker(exitDoor.transform, DoorMarkerHeightMeters);
                    break;
                case TutorialCoachTarget.Ben when ben != null:
                    coachMark.ShowMarker(ben.transform, BenMarkerHeightMeters);
                    break;
                case TutorialCoachTarget.None:
                case TutorialCoachTarget.Door:
                case TutorialCoachTarget.Ben:
                    coachMark.Hide();
                    break;
                default:
                    coachMark.ShowSpotlight(SpotlightParts(target), Caption(target));
                    break;
            }
        }

        // 같은 단계라도 밝힐 버튼이 바뀌면([말 걸기] → [대화하기]) 다시 그리도록 대상 객체를 함께 본다.
        private Object CoachObject(TutorialCoachTarget target)
        {
            return target == TutorialCoachTarget.TalkOffer ? offerView.ShownButtonRect : null;
        }

        private IReadOnlyList<RectTransform> SpotlightParts(TutorialCoachTarget target)
        {
            switch (target)
            {
                case TutorialCoachTarget.GuideCard:
                    return new[] { guideView.PanelRect };
                case TutorialCoachTarget.WordSaveButton:
                    return new[] { wordTooltip.SaveButtonRect };
                case TutorialCoachTarget.MenuButton:
                    return new[] { hudMenu.MenuButtonRect };
                case TutorialCoachTarget.VocabularyButton:
                    return new[] { hud.VocabularyButtonRect };
                case TutorialCoachTarget.TalkOffer:
                    return new[] { offerView.ShownButtonRect };
                case TutorialCoachTarget.GuidedLine:
                    return dialoguePanel.GuidedLineParts;
                case TutorialCoachTarget.DialogueEnd:
                    return new[] { dialoguePanel.EndButtonRect };
                case TutorialCoachTarget.Feedback:
                    return new[] { feedbackView.PanelRect };
                case TutorialCoachTarget.MapButton:
                    return new[] { firstDayMission.JournalButtonRect };
                default:
                    return new RectTransform[0];
            }
        }

        // 안내 카드가 숨는 대화 제안·대화·피드백 화면에서는 코치 마크 설명으로 할 일을 알린다.
        private string Caption(TutorialCoachTarget target)
        {
            switch (target)
            {
                case TutorialCoachTarget.TalkOffer:
                    return guide.TalkOfferCaption;
                case TutorialCoachTarget.GuidedLine:
                    return guide.GuidedLineCaption;
                case TutorialCoachTarget.DialogueEnd:
                    return guide.DialogueEndCaption;
                case TutorialCoachTarget.Feedback:
                    return guide.TryGetStepGuide(
                        TutorialStep.BenFeedback,
                        out TutorialStepGuide feedback
                    )
                        ? feedback.Task
                        : null;
                default:
                    return null;
            }
        }

        private Passerby FindBen()
        {
            foreach (Passerby passerby in passersby)
            {
                if (passerby.GetComponent<NpcIdentity>().Code == guide.TutorialNpcCode)
                {
                    return passerby;
                }
            }

            return null;
        }

        // 숙소에서 밖으로 나가는 문. 여러 개면 플레이어와 가장 가까운 문을 가리킨다.
        private SpaceDoorway FindExitDoor()
        {
            SpaceDoorway nearest = null;
            float nearestDistance = float.MaxValue;
            foreach (SpaceDoorway doorway in FindInThisScene<SpaceDoorway>())
            {
                float distance = Vector3.Distance(
                    doorway.transform.position,
                    player.transform.position
                );
                if (distance < nearestDistance)
                {
                    nearest = doorway;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        // 다음 장소를 미리 불러오는 동안 다른 씬의 문·택시를 잡지 않도록 이 씬의 것만 고른다.
        private T[] FindInThisScene<T>()
            where T : Component
        {
            var found = new List<T>();
            foreach (T candidate in FindObjectsByType<T>(FindObjectsSortMode.None))
            {
                if (candidate.gameObject.scene == gameObject.scene)
                {
                    found.Add(candidate);
                }
            }

            return found.ToArray();
        }
    }
}
