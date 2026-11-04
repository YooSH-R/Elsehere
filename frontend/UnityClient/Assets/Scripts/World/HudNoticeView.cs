using TMPro;
using UnityEngine;

namespace A305.World
{
    /// <summary>
    /// 화면 위 가운데의 짧은 안내판. 닫힌 장소의 문 앞 거절 안내처럼 잠깐 띄우는 안내와,
    /// 영업이 끝난 장소의 남은 시간처럼 끌 때까지 띄우는 안내를 보인다.
    /// </summary>
    public sealed class HudNoticeView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("안내를 띄울 때 켜는 판")]
        private GameObject plate;

        [SerializeField]
        private TMP_Text label;

        [SerializeField]
        [Min(0f)]
        [Tooltip("잠깐 띄우는 안내가 보이는 실제 시간(초)")]
        private float briefSeconds = 3f;

        private float hideAtRealtime = float.PositiveInfinity;

        public bool IsShowing => plate.activeSelf;

        public string Message => label.text;

        private void Awake()
        {
            if (plate == null || label == null)
            {
                Debug.LogError("HudNoticeView의 plate나 label이 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }

            plate.SetActive(false);
        }

        /// <summary>끌 때까지 안내를 띄운다.</summary>
        public void Show(string message)
        {
            label.text = message;
            plate.SetActive(true);
            hideAtRealtime = float.PositiveInfinity;
        }

        /// <summary>안내를 잠깐 띄웠다가 스스로 끈다.</summary>
        public void ShowBriefly(string message)
        {
            Show(message);
            hideAtRealtime = Time.unscaledTime + briefSeconds;
        }

        public void Hide()
        {
            plate.SetActive(false);
            hideAtRealtime = float.PositiveInfinity;
        }

        private void Update()
        {
            if (Time.unscaledTime >= hideAtRealtime)
            {
                Hide();
            }
        }
    }
}
