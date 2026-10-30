using System;
using A305.Theme;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Onboarding
{
    /// <summary>
    /// 게스트로 시작하기 전 캐릭터 선택과 음성 처리 동의(선택). 캐릭터를 골라야 [시작하기]를 누를 수 있다. 닉네임은 서버가 정한다.
    /// </summary>
    public class GuestPanelView : MonoBehaviour
    {
        // 게스트는 가입 약관 목록(AUTH-03)을 받지 않으므로 음성 처리 동의 문서를 여기 둔다. 서버 ConsentCatalog의 VOICE와 같게 둔다.
        private static readonly ConsentTerm VoiceTerm = new ConsentTerm
        {
            ConsentType = "VOICE",
            Title = "음성 처리 동의",
            Url = "/voice/2026-10",
        };

        [SerializeField]
        private Button femaleButton;

        [SerializeField]
        private Button maleButton;

        [SerializeField]
        private ConsentRowView voiceConsentRow;

        [SerializeField]
        private Button startButton;

        [SerializeField]
        private Button backButton;

        public event Action<AvatarType> AvatarSelected;
        public event Action<bool> VoiceConsentChanged;
        public event Action<string> DocumentRequested;
        public event Action StartRequested;
        public event Action BackRequested;

        private void Awake()
        {
            femaleButton.onClick.AddListener(() => AvatarSelected?.Invoke(AvatarType.F));
            maleButton.onClick.AddListener(() => AvatarSelected?.Invoke(AvatarType.M));
            voiceConsentRow.Show(VoiceTerm);
            voiceConsentRow.Toggle.onValueChanged.AddListener(agreed =>
                VoiceConsentChanged?.Invoke(agreed)
            );
            voiceConsentRow.ViewButton.onClick.AddListener(() =>
                DocumentRequested?.Invoke(VoiceTerm.Url)
            );
            startButton.onClick.AddListener(() => StartRequested?.Invoke());
            backButton.onClick.AddListener(() => BackRequested?.Invoke());
        }

        public void Show(AvatarType? selected, bool voiceAgreed)
        {
            femaleButton.GetComponent<ThemedElement>().SetSelected(selected == AvatarType.F);
            maleButton.GetComponent<ThemedElement>().SetSelected(selected == AvatarType.M);
            voiceConsentRow.Toggle.SetIsOnWithoutNotify(voiceAgreed);
            startButton.interactable = selected != null;
        }
    }
}
