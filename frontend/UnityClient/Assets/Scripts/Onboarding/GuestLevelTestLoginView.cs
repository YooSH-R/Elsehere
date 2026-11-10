using System;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Onboarding
{
    /// <summary>게스트에게 레벨테스트 로그인 필요를 알리는 모달. API 호출과 로그인 이동은 화면이 맡는다.</summary>
    public sealed class GuestLevelTestLoginView : MonoBehaviour
    {
        [SerializeField]
        private Button loginButton;

        [SerializeField]
        private Button continueButton;

        [SerializeField]
        private Button closeButton;

        public event Action LoginRequested;
        public event Action ContinueRequested;
        public event Action CloseRequested;

        private void Awake()
        {
            loginButton.onClick.AddListener(() => LoginRequested?.Invoke());
            continueButton.onClick.AddListener(() => ContinueRequested?.Invoke());
            closeButton.onClick.AddListener(() => CloseRequested?.Invoke());
        }

        public void Show(bool visible) => gameObject.SetActive(visible);
    }
}
