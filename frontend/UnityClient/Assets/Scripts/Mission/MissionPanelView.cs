using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace A305.Mission
{
    /// <summary>
    /// 화면 왼쪽 위의 미션 창. 미션마다 줄 템플릿을 복제해 보이고, 진행 상황과 시작·완료 알림을 보인다.
    /// </summary>
    public class MissionPanelView : MonoBehaviour
    {
        [SerializeField]
        private GameObject panel;

        [SerializeField]
        private TMP_Text headerText;

        [SerializeField]
        private TMP_Text messageText;

        [SerializeField]
        private Transform rowsParent;

        [SerializeField]
        [Tooltip("미션마다 복제하는 줄. 비활성 상태로 둔다.")]
        private MissionRowView rowTemplate;

        private readonly List<MissionRowView> rows = new List<MissionRowView>();

        /// <summary>어느 줄에서든 [미션 시작]을 누르면 미션 키를 알린다.</summary>
        public event Action<string> StartRequested;

        public bool IsVisible => panel.activeSelf;

        public void SetVisible(bool isVisible)
        {
            panel.SetActive(isVisible);
        }

        public void Refresh(MissionBoard board)
        {
            headerText.text = $"오늘의 작은 도전 {board.CompletedCount}/{board.Missions.Count}";
            CreateMissingRows(board.Missions.Count);

            for (int i = 0; i < board.Missions.Count; i++)
            {
                MissionContent mission = board.Missions[i];
                rows[i].Show(mission, board.ActiveMission == mission, board.IsCompleted(mission.Code));
            }
        }

        public void ShowMessage(string message)
        {
            messageText.text = message;
        }

        private void Awake()
        {
            rowTemplate.gameObject.SetActive(false);
            messageText.text = "미션은 한 번에 하나씩 진행해요.";
        }

        private void CreateMissingRows(int count)
        {
            while (rows.Count < count)
            {
                MissionRowView row = Instantiate(rowTemplate, rowsParent);
                row.gameObject.SetActive(true);
                row.StartRequested += code => StartRequested?.Invoke(code);
                rows.Add(row);
            }
        }
    }
}
