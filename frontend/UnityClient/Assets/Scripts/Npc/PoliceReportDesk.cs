using A305.Dialogue;
using A305.World;
using UnityEngine;

namespace A305.Npc
{
    /// <summary>
    /// 경찰서 창구. 신고인지와 장소는 서버의 의도 판정이 문맥으로 고르고, 신고를 받은 턴 응답에 출동할 장소가 온다
    /// (<see cref="DialogueController.PoliceDispatchRequested"/>). 창구는 화면 위쪽 상태 판에 접수 내용을 보여 주고,
    /// 경찰관이 대화를 마치면 그 경찰관이 창구를 떠나 직접 경찰서 옆 순찰차를 몰고 그 장소로(장소가 없으면 본길 순찰로) 출동한다.
    /// 순찰차가 돌아와 주차하면 창구에 다시 선다(<see cref="PoliceCar"/>).
    /// 무례로 센 말이나 신고가 아닌 말에는 서버가 출동 장소를 보내지 않는다.
    /// </summary>
    public sealed class PoliceReportDesk : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("창구에 선 경찰관")]
        private NpcIdentity officer;

        [SerializeField]
        private PoliceCar patrolCar;

        [SerializeField]
        private DialogueController dialogue;

        [SerializeField]
        private DialoguePanelView panel;

        private bool hasReport;
        private string reportedPlace;
        private bool showingStatus;

        /// <summary>이 창구에서 신고를 받는 경찰관의 NPC 코드</summary>
        public string OfficerCode => officer != null ? officer.Code : null;

        private void Awake()
        {
            if (officer == null || patrolCar == null || dialogue == null || panel == null)
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

        /// <summary>서버가 받은 신고를 접수한다. 대화를 마치면 출동한다.</summary>
        /// <param name="placeCode">출동할 마을 장소 코드. null이면 본길을 순찰한다.</param>
        public void TakeReport(string npcCode, string placeCode)
        {
            if (npcCode != officer.Code)
                return;
            hasReport = true;
            reportedPlace = placeCode;
            if (patrolCar.Phase != PolicePhase.Parked)
            {
                Show("신고 접수 · 순찰차가 이미 출동해 있어요");
                return;
            }
            string placeName = patrolCar.DisplayNameOf(placeCode);
            Show(
                placeName != null
                    ? $"신고 접수 · {placeName} · 대화를 마치면 경찰관이 순찰차로 출동해요"
                    : "신고 접수 · 대화를 마치면 경찰관이 순찰차로 본길을 순찰해요"
            );
        }

        private void OnDialogueEnded(string npcCode)
        {
            if (npcCode != officer.Code)
                return;
            bool dispatching = hasReport;
            string place = reportedPlace;
            hasReport = false;
            reportedPlace = null;
            if (showingStatus)
            {
                showingStatus = false;
                panel.SetVehicleStatus(null);
            }
            // 경찰관이 운전하러 나가면 이 창구(같은 오브젝트)도 꺼지므로 정리를 마친 뒤 출동시킨다.
            if (dispatching)
                patrolCar.Dispatch(place, officer.Code, officer.gameObject);
        }

        private void Show(string message)
        {
            showingStatus = true;
            panel.SetVehicleStatus(message);
        }
    }
}
