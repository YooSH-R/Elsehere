using System.Collections.Generic;
using A305.Dialogue;
using A305.World;
using UnityEngine;

namespace A305.Npc
{
    /// <summary>
    /// 경찰서 창구. 경찰관과 대화하는 동안 서버가 받아들인 발화로 신고를 접수하고(<see cref="PoliceReportIntake"/>),
    /// 화면 위쪽 상태 판에 접수 내용을 보여 준다. 경찰서는 마을과 다른 장면이라 대화를 마치면 신고를 남겨 두고
    /// (<see cref="PoliceDispatchOrder"/>), 밖으로 나와 마을이 열리면 경찰서 옆 순찰차가 그 신고로 출동한다.
    /// 장소를 하나만 말했으면 그곳으로, 아니면 본길을 순찰한다.
    /// 서버 오류·무례한 발화·NPC의 대화 종료로 끝난 발화는 접수하지 않는다(택시와 같은 기준).
    /// </summary>
    public sealed class PoliceReportDesk : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("창구에 선 경찰관")]
        private NpcIdentity officer;

        [SerializeField]
        [Tooltip("신고를 받을 수 있는 장소와 화면 이름. 순찰차의 출동 장소와 같은 목록이다.")]
        private List<TaxiDestination> destinations = new List<TaxiDestination>();

        [SerializeField]
        private DialogueController dialogue;

        [SerializeField]
        private DialoguePanelView panel;

        private readonly PoliceReportIntake intake = new PoliceReportIntake();
        private bool showingStatus;

        private void Awake()
        {
            if (officer == null || dialogue == null || panel == null)
            {
                Debug.LogError($"{name}의 PoliceReportDesk 연결이 비어 있습니다.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (dialogue == null)
                return;
            dialogue.UserTurnAccepted += OnUserTurnAccepted;
            dialogue.Ended += OnDialogueEnded;
        }

        private void OnDisable()
        {
            if (dialogue == null)
                return;
            dialogue.UserTurnAccepted -= OnUserTurnAccepted;
            dialogue.Ended -= OnDialogueEnded;
        }

        private void OnUserTurnAccepted(string npcCode, string text)
        {
            if (npcCode != officer.Code)
                return;
            intake.Hear(text);
            if (!intake.HasReport)
                return;
            if (PoliceDispatchOrder.IsPending)
            {
                Show("신고 접수 · 순찰차가 이미 출동하기로 했어요");
                return;
            }
            string placeName = destinations
                .Find(destination => destination.Code == intake.Place)
                ?.DisplayName;
            Show(
                placeName != null
                    ? $"신고 접수 · {placeName} · 대화를 마치면 순찰차가 출동해요"
                    : "신고 접수 · 대화를 마치면 순찰차가 본길을 순찰해요"
            );
        }

        private void OnDialogueEnded(string npcCode)
        {
            if (npcCode != officer.Code)
                return;
            if (intake.TryTakeDispatch(out string place))
                PoliceDispatchOrder.TryRequest(place);
            if (showingStatus)
            {
                showingStatus = false;
                panel.SetVehicleStatus(null);
            }
        }

        private void Show(string message)
        {
            showingStatus = true;
            panel.SetVehicleStatus(message);
        }
    }
}
