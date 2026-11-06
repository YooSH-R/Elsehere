using System.Collections.Generic;
using A305.Dialogue;
using A305.World;
using UnityEngine;

namespace A305.Npc
{
    /// <summary>
    /// 경찰서 창구. 서버가 문맥으로 판정한 출동 응답으로 신고를 접수하고,
    /// 화면 위쪽 상태 판에 접수 내용을 보여 준다. 경찰서는 마을과 다른 장면이라 대화를 마치면 신고를 남겨 두고
    /// (<see cref="PoliceDispatchOrder"/>), 밖으로 나와 마을이 열리면 경찰서 옆 순찰차가 그 신고로 출동한다.
    /// 출동할 장소가 없으면 본길을 순찰한다. 무례한 말과 신고가 아닌 말은 서버가 출동 응답을 보내지 않는다.
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

        private bool hasReport;
        private long reportedTurnId;
        private string reportedPlace;
        private bool showingStatus;

        public string OfficerCode => officer != null ? officer.Code : null;

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
            dialogue.PoliceDispatchRequested += TakeReport;
            dialogue.Ended += OnDialogueEnded;
        }

        private void OnDisable()
        {
            if (dialogue == null)
                return;
            dialogue.PoliceDispatchRequested -= TakeReport;
            dialogue.Ended -= OnDialogueEnded;
        }

        public void TakeReport(string npcCode, long turnId, string placeCode)
        {
            if (npcCode != officer.Code)
                return;
            hasReport = true;
            reportedTurnId = turnId;
            reportedPlace = placeCode;
            if (PoliceDispatchOrder.IsPending || PoliceDispatchOrder.Current != null)
            {
                Show("신고 접수 · 앞선 신고를 마친 뒤 차례로 출동해요");
                return;
            }
            string placeName = destinations
                .Find(destination => destination.Code == placeCode)
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
            if (hasReport)
                PoliceDispatchOrder.TryRequest(reportedTurnId, officer.Code, reportedPlace);
            hasReport = false;
            reportedPlace = null;
            if (showingStatus)
            {
                showingStatus = false;
                panel.SetVehicleStatus(null);
            }
            if (PoliceDispatchOrder.IsAway(officer.Code))
                officer.gameObject.SetActive(false);
        }

        private void Show(string message)
        {
            showingStatus = true;
            panel.SetVehicleStatus(message);
        }
    }
}
