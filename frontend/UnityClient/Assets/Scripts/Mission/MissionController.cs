using System;
using A305.Dialogue;
using UnityEngine;

namespace A305.Mission
{
    /// <summary>
    /// 미션 창에서 미션을 시작하고, 대화가 끝나면 진행 중인 미션을 완료했는지 판정한다.
    /// 미션 창을 언제 보일지는 튜토리얼 같은 진행 흐름이 <see cref="SetPanelVisible"/>로 정한다.
    /// </summary>
    public class MissionController : MonoBehaviour
    {
        [SerializeField]
        private DialogueController dialogue;

        [SerializeField]
        private MissionSource missionSource;

        [SerializeField]
        private MissionPanelView panelView;

        [SerializeField, HideInInspector]
        private FirstDayMissionController firstDayMission;
        private bool storyAvailable;

        public void SetStoryAvailable(bool available)
        {
            storyAvailable = available && firstDayMission != null;
            if (firstDayMission != null)
                firstDayMission.SetAvailable(storyAvailable);
        }

        private MissionBoard board;

        /// <summary>미션을 시작하면 미션 키를 알린다.</summary>
        public event Action<string> MissionStarted;

        public void SetPanelVisible(bool isVisible)
        {
            panelView.SetVisible(isVisible && !storyAvailable);
        }

        private void Awake()
        {
            if (dialogue == null || missionSource == null || panelView == null)
            {
                Debug.LogError(
                    "MissionController의 dialogue·missionSource·panelView가 연결되지 않았습니다.",
                    this
                );
                enabled = false;
                return;
            }

            board = MissionProgress.GetOrCreateBoard(missionSource);
            dialogue.ConversationEnded += CheckCompletion;
            panelView.StartRequested += StartMission;
        }

        private void Start()
        {
            panelView.Refresh(board);
        }

        private void OnDestroy()
        {
            if (dialogue != null)
            {
                dialogue.ConversationEnded -= CheckCompletion;
            }
        }

        private void StartMission(string missionCode)
        {
            MissionContent previous = board.ActiveMission;
            if (!board.Start(missionCode))
            {
                return;
            }

            panelView.Refresh(board);
            panelView.ShowMessage(MissionText.StartMessage(previous, board.ActiveMission));
            MissionStarted?.Invoke(missionCode);
        }

        private void CheckCompletion(DialogueRecord record)
        {
            MissionContent completed = board.CompleteIfMatched(
                record.NpcCode,
                record.UserUtterances
            );
            if (completed == null)
            {
                return;
            }

            panelView.Refresh(board);
            panelView.ShowMessage($"미션 완료! {completed.Title}");
        }
    }
}
