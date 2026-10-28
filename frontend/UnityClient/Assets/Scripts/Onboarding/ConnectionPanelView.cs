using System;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Onboarding
{
    /// <summary>
    /// 접속 상태를 확인하지 못했을 때의 화면. 실패한 이유는 화면 아래 안내 문구에 보이고, 이 화면은 [다시 시도]를 둔다.
    /// </summary>
    public class ConnectionPanelView : MonoBehaviour
    {
        [SerializeField]
        private Button retryButton;

        public event Action RetryRequested;

        private void Awake()
        {
            retryButton.onClick.AddListener(() => RetryRequested?.Invoke());
        }
    }
}
