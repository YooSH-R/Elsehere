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
    /// 공간 씬마다 하나씩 두는 튜토리얼 진행 처리. 걷기·시점·단어 저장·단어장·공간 이동·대화·피드백·수첩 신호를 튜토리얼 진행에 전하고,
    /// 단계에 맞게 안내 카드와 코치 마크, 행인이 다가와도 되는지, 장소 이동·택시 잠금, 미션 표시를 바꾼다.
    /// 숙소를 나오면 Ben과 길 안내를 마칠 때까지 Ben을 바라보는 구도로 플레이어를 붙잡아 둔다.
    /// Ben과 대화를 시작하면 대사·[뜻]·추천 단어·추천 문장을 차례로 밝히는 티칭을 [다음]으로 넘기며 보인다.
    /// 튜토리얼을 끝내면 서버에 저장하고(TUT-01·02), 저장하지 못했으면 다음 장소에 들어갈 때 다시 보낸다.
    /// </summary>
    public class TutorialDirector : MonoBehaviour
    {
        // Ben 머리 위 화살표 높이(발밑 기준).
        private const float BenMarkerHeightMeters = 2.3f;
        private const float FinishRetrySeconds = 5f;

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
        [Tooltip(
            "첫날 미션의 퀘스트 카드와 수첩. 비우면(첫날 미션 화면이 없는 Sandbox) 오늘의 목표 안내를 바로 마친다."
        )]
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
        private bool savingFinish;
        private float nextFinishAttempt;
        private TutorialStep appliedStep;
        private bool isCardShown;
        private bool isSkipCardShown;
        private TutorialStep skipCardStep;
        private TutorialStep cardStep;
        private bool cardCanContinue;
        private int cardDoneCount;
        private bool cardUsesTouch;
        private bool isHoldingForBen;
        private Passerby ben;
        private Transform benFace;
        private SpaceDoorway exitDoor;
        private TaxiRide[] taxis;
        private readonly TutorialAutoAdvance autoAdvance = new TutorialAutoAdvance();
        private TutorialCoachTarget shownCoachTarget;

        // 대화 시작·대화 노트 티칭에서 [다음]으로 넘긴 수. 대화를 새로 시작하면 처음부터 다시 보인다.
        private int dialogueTourStep;
        private bool relationshipExplained;
        private int feedbackTourStep;
        private Object shownCoachObject;
        private string shownCaption;

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
            benFace = ben != null ? ben.GetComponent<NpcIdentity>().LookTarget : null;
            exitDoor = FindExitDoor();
            taxis = FindInThisScene<TaxiRide>();
            if (gameObject.scene.name != guide.HomeSceneName)
            {
                progress.LeaveHome();
            }

            // 행인은 첫 Update에서 다가오기 시작하므로, 모든 Awake가 끝나기 전에 허용 여부를 정해 둔다.
            ApplyStep();

            dialogue.Started += progress.StartDialogue;
            dialogue.Started += RestartDialogueTour;
            dialogue.ConversationEnded += EndDialogue;
            learning.FeedbackClosed += CloseFeedback;
            wordTooltip.SaveRequested += progress.ReportWordSaved;
            hud.VocabularyRequested += progress.ReportVocabularyOpened;
            if (firstDayMission != null)
            {
                firstDayMission.JournalOpened += progress.ReportJournalOpened;
            }

            guideView.ContinueRequested += progress.Continue;
            guideView.SkipRequested += SkipTutorial;
            SpaceTravel.Refused += ShowTravelLocked;
        }

        private void OnDestroy()
        {
            if (progress == null)
            {
                return;
            }

            dialogue.Started -= progress.StartDialogue;
            dialogue.Started -= RestartDialogueTour;
            dialogue.ConversationEnded -= EndDialogue;
            learning.FeedbackClosed -= CloseFeedback;
            wordTooltip.SaveRequested -= progress.ReportWordSaved;
            hud.VocabularyRequested -= progress.ReportVocabularyOpened;
            if (firstDayMission != null)
            {
                firstDayMission.JournalOpened -= progress.ReportJournalOpened;
            }

            SpaceTravel.Refused -= ShowTravelLocked;
            CenterWindow.Shared.TopObstruction = null;
        }

        private void Update()
        {
            progress.UsesTouch = PlayerTouchControls.ShouldShowControls();
            ReportActions();
            HoldPlayerForBen();

            // 첫날 미션 화면이 없으면 보여 줄 지도가 없으므로 오늘의 목표 안내를 바로 마친다.
            if (firstDayMission == null)
            {
                progress.ReportJournalOpened();
            }

            // 걷기·둘러보기는 안내한 조작을 모두 해내면 완료 표시를 잠깐 보인 뒤 저절로 넘어간다.
            if (autoAdvance.Tick(progress.Step, progress.CanContinue, Time.deltaTime))
            {
                progress.Continue();
            }

            if (progress.Step != appliedStep)
            {
                ApplyStep();
            }

            if (Time.unscaledTime >= nextFinishAttempt)
            {
                SaveFinish();
            }

            RefreshCard();
            RefreshCoachMark();
        }

        private void ReportActions()
        {
            if (player.IsMoving)
            {
                progress.Report(TutorialAction.Walk);
            }

            if (player.IsRunning)
            {
                progress.Report(TutorialAction.Run);
            }

            if (player.JumpedThisFrame)
            {
                progress.Report(TutorialAction.Jump);
            }

            PlayerCameraRig cameraRig = player.CameraRig;
            if (cameraRig == null)
            {
                return;
            }

            if (cameraRig.TurnedByUserThisFrame)
            {
                progress.Report(TutorialAction.Rotate);
            }

            if (cameraRig.ZoomedByUserThisFrame)
            {
                progress.Report(TutorialAction.Zoom);
            }

            if (cameraRig.ToggledViewThisFrame)
            {
                progress.Report(TutorialAction.ToggleView);
            }
        }

        // 숙소를 나오면 다른 곳으로 걸어가지 않게 이동·시점을 잠그고, Ben을 바라보며 어깨 너머로 Ben을 비추는 구도로 붙잡아 둔다.
        // Ben은 자기를 기다리느라 잠긴 플레이어에게 다가와 말을 건다. 대화 중에는 대화가 잠금과 시점을 맡고,
        // 길 안내를 마치지 않고 대화를 끝내면 다시 붙잡는다. 단어장 같은 화면이 열려 있으면 Ben은 다가오지 않는다.
        // 다른 화면이 잠금을 풀어도 다음 프레임에 다시 잠근다.
        private void HoldPlayerForBen()
        {
            bool shouldHold =
                progress.Step == TutorialStep.MeetBen
                && ben != null
                && benFace != null
                && !SpaceTravel.IsTraveling;
            PlayerCameraRig cameraRig = player.CameraRig;
            if (ben != null)
            {
                ben.IsPlayerWaiting = shouldHold && !learning.IsShowingScreen;
            }

            if (shouldHold)
            {
                if (!player.IsMovementLocked)
                {
                    player.SetMovementLocked(true);
                }

                if (!dialogue.IsTalking)
                {
                    player.FaceToward(ben.transform.position);
                }

                if (cameraRig != null && !cameraRig.IsInMeetingView)
                {
                    cameraRig.EnterMeetingView(benFace);
                }

                isHoldingForBen = true;
            }
            else if (isHoldingForBen)
            {
                isHoldingForBen = false;
                if (cameraRig != null)
                {
                    cameraRig.ExitMeetingView();
                }

                if (!dialogue.IsTalking && !SpaceTravel.IsTraveling && !learning.IsShowingScreen)
                {
                    player.SetMovementLocked(false);
                }
            }
        }

        private void EndDialogue(DialogueRecord record)
        {
            progress.EndDialogue(record.NpcCode, record.IsGuidedCompleted);
            SaveFinish();
        }

        // 길 안내 대화 중에 건너뛰면 마치지 못한 대화를 피드백 없이 닫는다. 정해진 대사를 마치기 전에는 [대화 종료]로 끝낼 수 없기 때문이다.
        private void SkipTutorial()
        {
            progress.Skip();
            dialogue.Abandon();
        }

        private void CloseFeedback(string npcCode, bool isGuidedCompleted)
        {
            progress.CloseFeedback(npcCode);
        }

        // 할 일이 적힌 안내 카드에 바로 알린다. 카드가 숨은 동안(대화·피드백 화면)에만 HUD 알림을 쓴다.
        private void ShowTravelLocked()
        {
            if (guideView.IsVisible)
            {
                guideView.ShowNotice(guide.TravelLockedMessage);
                return;
            }

            hud.ShowMessage(guide.TravelLockedMessage);
        }

        private void ApplyStep()
        {
            appliedStep = progress.Step;
            // Ben 대화·전체 완료 직후와, 저장하지 못한 채 다음 단계·장소에 들어온 순간에 보낸다.
            if (appliedStep == TutorialStep.Completed || progress.NeedsBenDialogueSave)
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
            // 튜토리얼은 정해진 대본으로 연습하는 시간이라 마치거나 건너뛸 때까지 게임 시각을 멈춰 둔다.
            GameClock.Shared.IsHeld = progress.IsGuiding;
            foreach (TaxiRide taxi in taxis)
            {
                taxi.IsHailAllowed = !progress.IsGuiding;
            }

            mission.SetStoryAvailable(progress.IsMissionAvailable);
            mission.SetPanelVisible(progress.IsMissionAvailable);
        }

        private void SaveFinish()
        {
            if (
                finishRecorder != null
                && !savingFinish
                && (progress.NeedsFinishSave || progress.NeedsBenDialogueSave)
            )
            {
                _ = SaveFinishAsync();
            }
        }

        private async Task SaveFinishAsync()
        {
            savingFinish = true;
            TutorialFinishSaveStatus status;
            try
            {
                status = await finishRecorder.SaveAsync(progress);
            }
            finally
            {
                savingFinish = false;
                nextFinishAttempt = Time.unscaledTime + FinishRetrySeconds;
            }
            if (status == TutorialFinishSaveStatus.Saved)
            {
                GameClock.Shared.RequestSynchronization();
                // 서버가 튜토리얼 완료·건너뛰기와 함께 첫 퀘스트를 시작하므로, 건너뛰어 퀘스트 카드 안내를 못 본 사용자도
                // 장소를 옮기거나 대화하기 전에 작은 퀘스트 알약 대신 할 일이 담긴 카드를 보게 다시 읽는다.
                if (progress.Step == TutorialStep.Completed)
                    mission.RefreshStory();
            }
            if (
                status == TutorialFinishSaveStatus.Failed
                || status == TutorialFinishSaveStatus.Rejected
            )
            {
                Debug.LogWarning(
                    $"튜토리얼 대화·완료 기록을 저장하지 못했습니다({status}, {finishRecorder.LastError.Code}): "
                        + finishRecorder.LastError.Message,
                    this
                );
            }
        }

        // 대화 제안·대화·피드백·단어장 화면을 가리지 않도록 그동안에는 카드를 숨기고 코치 마크 설명으로 할 일을 알린다.
        // 내용이 바뀔 때만 다시 그린다.
        private void RefreshCard()
        {
            // 대화·제안·대화 피드백·단어장·휴대폰이 할 일 카드를 가리는 동안에는 [건너뛰기]만 남긴 작은 카드를 맨 위에 두고,
            // 할 일은 코치 마크 설명으로 알린다. 대화처럼 안내 카드가 없는 단계는 가장 가까운 앞 단계의 이름을 쓴다.
            bool isCovered =
                dialogue.IsTalking
                || offerView.IsShowing
                || learning.IsShowingScreen
                || hudMenu.IsOpen;
            if (
                isCovered
                && progress.IsGuiding
                && guide.TryGetNearestStepGuide(progress.Step, out TutorialStepGuide nearest)
            )
            {
                if (!isSkipCardShown || skipCardStep != nearest.Step)
                {
                    guideView.ShowSkipOnly(
                        nearest,
                        guide.StepGuideNumber(nearest.Step),
                        guide.StepGuideCount
                    );
                    isSkipCardShown = true;
                    skipCardStep = nearest.Step;
                    isCardShown = false;
                }

                return;
            }

            bool hasGuide = guide.TryGetStepGuide(progress.Step, out TutorialStepGuide stepGuide);
            if (isCovered || !hasGuide)
            {
                if (isCardShown || isSkipCardShown)
                {
                    guideView.Hide();
                    isCardShown = false;
                    isSkipCardShown = false;
                }

                return;
            }

            isSkipCardShown = false;

            IReadOnlyList<TutorialAction> actions = TutorialProgress.RequiredActions(
                progress.Step,
                progress.UsesTouch
            );
            var partDone = new bool[actions.Count];
            int doneCount = 0;
            for (int i = 0; i < actions.Count; i++)
            {
                partDone[i] = progress.IsDone(actions[i]);
                doneCount += partDone[i] ? 1 : 0;
            }

            bool isChanged =
                !isCardShown
                || cardStep != progress.Step
                || cardCanContinue != progress.CanContinue
                || cardDoneCount != doneCount
                || cardUsesTouch != progress.UsesTouch;
            if (!isChanged)
            {
                return;
            }

            guideView.Show(
                stepGuide,
                progress.CanContinue,
                progress.UsesTouch,
                partDone,
                guide.StepGuideNumber(progress.Step),
                guide.StepGuideCount
            );
            isCardShown = true;
            cardStep = progress.Step;
            cardCanContinue = progress.CanContinue;
            cardDoneCount = doneCount;
            cardUsesTouch = progress.UsesTouch;
        }

        private void RefreshCoachMark()
        {
            // 설명 말풍선과 단어장·대화 피드백·대화 노트 창이 위에 남겨 둔 [건너뛰기] 카드를 피한다.
            RectTransform skipCard = guideView.IsSkipOnly ? guideView.PanelRect : null;
            coachMark.KeepClear = skipCard;
            CenterWindow.Shared.TopObstruction = skipCard;
            var screen = new TutorialScreen
            {
                IsTalking = dialogue.IsTalking,
                IsTalkOfferShowing =
                    ben != null && (offerView.IsShownBy(ben) || offerView.IsTalkPromptShownBy(ben)),
                IsGuidedCompleted = dialogue.IsGuidedCompleted,
                GuidedTurnIndex = dialogue.GuidedTurnIndex,
                DialogueTourStep = dialogueTourStep,
                RelationshipExplained = relationshipExplained,
                FeedbackTourStep = feedbackTourStep,
                IsWordCardPinned = wordTooltip.IsPinned,
                CanSaveWord = wordTooltip.CanSaveWord,
                IsMenuOpen = hudMenu.IsOpen,
                IsLearningScreenShowing = learning.IsShowingScreen,
                IsVocabularyShowing = learning.IsShowingVocabulary,
                IsFeedbackShowing = feedbackView.IsVisible,
                IsJournalOpen = firstDayMission != null && firstDayMission.IsJournalOpen,
                // 장면을 내릴 때 대화창이 이 처리보다 먼저 지워질 수 있다.
                IsVoiceLightOn = dialoguePanel != null && dialoguePanel.IsVoiceLightOn,
            };
            RefreshVoiceLightNote(screen);
            TutorialCoachTarget target = TutorialCoachPlan.Choose(progress.Step, screen);
            // 화면 구성에 따라 숨은 부분(한 쪽뿐인 피드백의 쪽 넘김 등)은 [다음]을 누를 수 없으므로 건너뛴다.
            if (TutorialCoachPlan.IsTour(target) && !HasVisiblePart(SpotlightParts(target)))
            {
                ContinueTour(target);
                return;
            }

            Object targetObject = CoachObject(target);
            string caption = TutorialGuideText.KeyChips(Caption(target));
            if (
                target == shownCoachTarget
                && targetObject == shownCoachObject
                && caption == shownCaption
            )
            {
                return;
            }

            shownCoachTarget = target;
            shownCoachObject = targetObject;
            shownCaption = caption;
            switch (target)
            {
                case TutorialCoachTarget.Door when exitDoor != null:
                    // 문 위 화살표는 문 가까이에서 캐릭터를 가리므로 발밑 나침반으로 문 쪽을 가리킨다.
                    coachMark.ShowCompass(player.transform, exitDoor.transform);
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
                    // 대화 시작·대화 노트 티칭은 하나씩 어둡게 밝혀 볼 곳을 알리고 [다음]으로 넘긴다.
                    coachMark.ShowSpotlight(
                        SpotlightParts(target),
                        caption,
                        next: TutorialCoachPlan.IsTour(target)
                            ? () => ContinueTour(target)
                            : (System.Action)null,
                        // 대사를 말하는 동안에는 오른쪽 [힌트]도 눌러야 하므로 둘레를 덮지 않고 테두리만 둔다.
                        dimOutside: target != TutorialCoachTarget.GuidedLine,
                        captionSide: CaptionSide(target),
                        // 호감도와 할 대사 설명은 대사 칸 왼쪽 위에 두어 가운데 서 있는 Ben 얼굴을 가리지 않는다.
                        captionAlignLeft: target == TutorialCoachTarget.DialogueRelationship
                            || target == TutorialCoachTarget.GuidedLine
                    );
                    break;
            }
        }

        // 녹음하는 동안에는 할 대사 말풍선을 그대로 두고, [말하기]의 불 옆에 불이 무슨 뜻인지 말풍선을 하나 더 띄운다.
        private void RefreshVoiceLightNote(TutorialScreen screen)
        {
            if (TutorialCoachPlan.ExplainsVoiceLight(progress.Step, screen))
            {
                coachMark.ShowNote(
                    dialoguePanel.VoiceLightRect,
                    TutorialGuideText.KeyChips(guide.VoiceLightCaption)
                );
            }
            else
            {
                coachMark.HideNote();
            }
        }

        private void RestartDialogueTour(string npcCode)
        {
            relationshipExplained = false;
            dialogueTourStep = 0;
            feedbackTourStep = 0;
        }

        private void ContinueTour(TutorialCoachTarget target)
        {
            if (target == TutorialCoachTarget.DialogueRelationship)
            {
                relationshipExplained = true;
            }
            else if (TutorialCoachPlan.IsDialogueTour(target))
            {
                dialogueTourStep++;
            }
            else
            {
                feedbackTourStep++;
            }
        }

        private static bool HasVisiblePart(IReadOnlyList<RectTransform> parts)
        {
            foreach (RectTransform part in parts)
            {
                if (part != null && part.gameObject.activeInHierarchy)
                {
                    return true;
                }
            }

            return false;
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
                case TutorialCoachTarget.DialogueLines:
                    return dialoguePanel.DialogueLinesParts;
                case TutorialCoachTarget.DialogueTools:
                    return dialoguePanel.ToolParts;
                case TutorialCoachTarget.DialogueRelationship:
                    return dialoguePanel.RelationshipParts;
                case TutorialCoachTarget.GuidedLine:
                    // 대사를 읽으며 말해야 하므로 대사 칸과 입력 줄을 한 테두리로 묶는다. 오른쪽 [힌트]는 담지 않는다.
                    var parts = new List<RectTransform>(dialoguePanel.DialogueLinesParts);
                    parts.AddRange(dialoguePanel.GuidedLineParts);
                    return parts;
                case TutorialCoachTarget.SuggestedWords:
                    return new[] { dialoguePanel.SuggestedWordsRect };
                case TutorialCoachTarget.SuggestedSentence:
                    return new[] { dialoguePanel.SuggestedSentenceRect };
                case TutorialCoachTarget.DialogueEnd:
                    return new[] { dialoguePanel.EndButtonRect };
                // 탭을 누르면 바뀌는 결과 칸도 함께 밝혀 무엇이 달라지는지 보이게 한다.
                // 스크롤 안내에서는 고정된 본문 영역을 밝힌다.
                case TutorialCoachTarget.FeedbackSections:
                    return new[] { feedbackView.SectionRowRect, feedbackView.PageCardRect };
                case TutorialCoachTarget.FeedbackPages:
                    return new[] { feedbackView.ScrollAreaRect };
                case TutorialCoachTarget.FeedbackClose:
                    return new[] { feedbackView.CloseButtonRect };
                case TutorialCoachTarget.Vocabulary:
                    return new[] { learning.VocabularyPanelRect };
                case TutorialCoachTarget.QuestCard:
                    return firstDayMission != null
                        ? new[] { firstDayMission.QuestButtonRect }
                        : new RectTransform[0];
                default:
                    return new RectTransform[0];
            }
        }

        // 안내 카드가 숨는 대화 제안·대화·단어장·피드백 화면에서는 밝힌 곳 옆 말풍선으로 할 일을 알린다.
        // 길 안내 대화는 처음에 볼 곳을 하나씩 설명하고, 그 뒤에는 지금 할 대사·뜻을 크게 보이며
        // 다른 문장을 보냈으면 다시 입력하라고 알린다.
        private string Caption(TutorialCoachTarget target)
        {
            switch (target)
            {
                case TutorialCoachTarget.TalkOffer:
                    return guide.TalkOfferCaption;
                case TutorialCoachTarget.DialogueLines:
                    return guide.DialogueLinesCaption;
                case TutorialCoachTarget.DialogueRelationship:
                    return guide.RelationshipCaption;
                case TutorialCoachTarget.DialogueTools:
                    return guide.DialogueToolsCaption;
                case TutorialCoachTarget.SuggestedWords:
                    return guide.SuggestedWordsCaption;
                case TutorialCoachTarget.SuggestedSentence:
                    return guide.SuggestedSentenceCaption;
                case TutorialCoachTarget.VocabularyButton:
                    return guide.VocabularyButtonCaption;
                case TutorialCoachTarget.GuidedLine:
                    string notice = dialogue.IsGuidedLineMissed
                        ? guide.GuidedLineMissCaption
                        : guide.GuidedLineCaption;
                    return $"{notice}\n<size=130%><b>{dialogue.GuidedLine}</b></size>\n{dialogue.GuidedLineKorean}";
                case TutorialCoachTarget.DialogueEnd:
                    return guide.DialogueEndCaption;
                case TutorialCoachTarget.Vocabulary:
                    return guide.VocabularyCaption;
                case TutorialCoachTarget.FeedbackSections:
                    return guide.FeedbackSectionsCaption;
                case TutorialCoachTarget.FeedbackPages:
                    return guide.FeedbackPagesCaption;
                case TutorialCoachTarget.FeedbackClose:
                    return guide.FeedbackCloseCaption;
                default:
                    return null;
            }
        }

        // 대화창의 이름 줄·도구 줄·대사 칸처럼 옆에 다른 버튼이 늘어선 곳은 설명을 위에 두고,
        // 대화 피드백 창을 밝히는 티칭과 닫기 버튼은 설명을 창 아래쪽 넓은 자리에 두어 탭과 제목을 가리지 않는다.
        private static CoachMarkLayout.BubbleSide? CaptionSide(TutorialCoachTarget target)
        {
            switch (target)
            {
                case TutorialCoachTarget.DialogueTools:
                case TutorialCoachTarget.DialogueRelationship:
                case TutorialCoachTarget.GuidedLine:
                    return CoachMarkLayout.BubbleSide.Above;
                case TutorialCoachTarget.FeedbackSections:
                case TutorialCoachTarget.FeedbackPages:
                case TutorialCoachTarget.FeedbackClose:
                    return CoachMarkLayout.BubbleSide.Below;
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
