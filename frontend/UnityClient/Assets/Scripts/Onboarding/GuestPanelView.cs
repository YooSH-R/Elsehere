using System;
using A305.Theme;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Onboarding
{
    /// <summary>게스트로 시작하기 전 캐릭터 선택. 캐릭터를 골라야 [시작하기]를 누를 수 있다. 닉네임은 서버가 정한다.</summary>
    public class GuestPanelView : MonoBehaviour
    {
        [SerializeField]
        private Button femaleButton;

        [SerializeField]
        private Button maleButton;

        [SerializeField]
        private Button startButton;

        [SerializeField]
        private Button backButton;

        public event Action<AvatarType> AvatarSelected;
        public event Action StartRequested;
        public event Action BackRequested;

        private void Awake()
        {
            femaleButton.onClick.AddListener(() => AvatarSelected?.Invoke(AvatarType.F));
            maleButton.onClick.AddListener(() => AvatarSelected?.Invoke(AvatarType.M));
            startButton.onClick.AddListener(() => StartRequested?.Invoke());
            backButton.onClick.AddListener(() => BackRequested?.Invoke());
        }

        public void Show(AvatarType? selected)
        {
            femaleButton.GetComponent<ThemedElement>().SetSelected(selected == AvatarType.F);
            maleButton.GetComponent<ThemedElement>().SetSelected(selected == AvatarType.M);
            startButton.interactable = selected != null;
        }
    }
}
