using System;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Onboarding
{
    /// <summary>
    /// 화면 04 레벨 테스트 첫 화면. MVP는 레벨 판정을 하지 않아 [레벨 테스트 진행하기]를 막아 두고 [건너뛰기]만 쓴다.
    /// </summary>
    public class LevelTestPanelView : MonoBehaviour
    {
        [SerializeField]
        private Button startButton;

        [SerializeField]
        private Button skipButton;

        public event Action SkipRequested;

        private void Awake()
        {
            startButton.interactable = false;
            skipButton.onClick.AddListener(() => SkipRequested?.Invoke());
        }
    }
}
