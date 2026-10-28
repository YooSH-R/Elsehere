using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Mission
{
    /// <summary>미션 창의 한 줄. 제목·설명·진행 상태와 [미션 시작] 버튼.</summary>
    public class MissionRowView : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text titleText;

        [SerializeField]
        private TMP_Text descriptionText;

        [SerializeField]
        private TMP_Text statusText;

        [SerializeField]
        private Button startButton;

        private string missionCode;

        /// <summary>[미션 시작]을 누르면 미션 키를 알린다.</summary>
        public event Action<string> StartRequested;

        public void Show(MissionContent mission, bool isActive, bool isCompleted)
        {
            missionCode = mission.Code;
            titleText.text = $"{mission.PlaceName} · <b>{mission.Title}</b>";
            descriptionText.text = mission.Description;
            statusText.text = MissionText.Status(mission, isActive, isCompleted);
            startButton.gameObject.SetActive(!isActive && !isCompleted);
        }

        private void Awake()
        {
            startButton.onClick.AddListener(() => StartRequested?.Invoke(missionCode));
        }
    }
}
