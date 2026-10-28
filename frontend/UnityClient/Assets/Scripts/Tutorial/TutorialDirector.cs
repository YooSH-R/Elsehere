using A305.Dialogue;
using A305.Learning;
using A305.Mission;
using A305.Npc;
using A305.ObjectHover;
using A305.Player;
using UnityEngine;

namespace A305.Tutorial
{
    /// <summary>
    /// 공간 씬마다 하나씩 두는 튜토리얼 진행 처리. 걷기·사물 툴팁·공간 이동·대화·피드백·미션 신호를 튜토리얼 진행에 전하고,
    /// 단계에 맞게 안내 카드, 행인이 다가와도 되는지, 미션 창 표시를 바꾼다.
    /// </summary>
    public class TutorialDirector : MonoBehaviour
    {
        [SerializeField]
        private TutorialGuideDefinition guide;

        [SerializeField]
        private TutorialGuideView guideView;

        [SerializeField]
        private PlayerMover player;

        [SerializeField]
        private WordTooltipView wordTooltip;

        [SerializeField]
        private DialogueController dialogue;

        [SerializeField]
        private PasserbyOfferView offerView;

        [SerializeField]
        private LearningController learning;

        [SerializeField]
        private MissionController mission;

        [SerializeField]
        [Tooltip("이 씬에 놓인 행인. 공간 기능 연결 도구가 채운다.")]
        private Passerby[] passersby = new Passerby[0];

        private TutorialProgress progress;
        private TutorialStep appliedStep;
        private bool isCardShown;
        private TutorialStep cardStep;
        private bool cardCanContinue;

        private void Awake()
        {
            if (
                guide == null
                || guideView == null
                || player == null
                || wordTooltip == null
                || dialogue == null
                || offerView == null
                || learning == null
                || mission == null
            )
            {
                Debug.LogError("TutorialDirector의 참조가 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }

            progress = TutorialState.GetOrCreate(guide.TutorialNpcCode, guide.MissionNpcCode);
            if (gameObject.scene.name != guide.HomeSceneName)
            {
                progress.LeaveHome();
            }

            // 행인은 첫 Update에서 다가오기 시작하므로, 모든 Awake가 끝나기 전에 허용 여부를 정해 둔다.
            ApplyStep();

            dialogue.Started += progress.StartDialogue;
            dialogue.ConversationEnded += EndDialogue;
            learning.FeedbackClosed += CloseFeedback;
            mission.MissionStarted += StartMission;
            guideView.ContinueRequested += progress.Continue;
            guideView.SkipRequested += progress.Skip;
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
            mission.MissionStarted -= StartMission;
        }

        private void Update()
        {
            if (player.IsMoving)
            {
                progress.ReportWalked();
            }

            if (wordTooltip.IsVisible)
            {
                progress.ReportHovered();
            }

            if (progress.Step != appliedStep)
            {
                ApplyStep();
            }

            RefreshCard();
        }

        private void EndDialogue(DialogueRecord record)
        {
            progress.EndDialogue(record.NpcCode, record.IsGuidedCompleted);
        }

        private void CloseFeedback(string npcCode, bool isGuidedCompleted)
        {
            progress.CloseFeedback(npcCode);
        }

        private void StartMission(string missionCode)
        {
            progress.StartMission();
        }

        private void ApplyStep()
        {
            appliedStep = progress.Step;
            foreach (Passerby passerby in passersby)
            {
                string npcCode = passerby.GetComponent<NpcIdentity>().Code;
                passerby.IsApproachAllowed = progress.IsApproachAllowed(npcCode);
            }

            mission.SetStoryAvailable(progress.Step == TutorialStep.Completed);
            mission.SetPanelVisible(progress.IsMissionPanelVisible);
        }

        // 대화 제안 화면과 대화 화면을 가리지 않도록 그동안에는 카드를 숨긴다. 내용이 바뀔 때만 다시 그린다.
        private void RefreshCard()
        {
            bool hasGuide = guide.TryGetStepGuide(progress.Step, out TutorialStepGuide stepGuide);
            bool shouldShow = hasGuide && !dialogue.IsTalking && !offerView.IsShowing;
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
    }
}
