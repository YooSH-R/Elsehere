using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Onboarding
{
    /// <summary>
    /// 화면 02 가입의 두 번째 단계: 가입 정보 입력. 닉네임·캐릭터 입력을 전달하고, <see cref="SignupForm"/>의 상태를 보여 준다.
    /// 약관은 앞 단계(<see cref="TermsPanelView"/>)에서 받고, [뒤로]로 그 단계에 돌아간다.
    /// 게스트가 회원이 되는 가입이면 캐릭터는 게스트 때 것으로 골라 둔 채 바꾸지 못하게 하고, 기록이 이어진다고 안내한다.
    /// </summary>
    public class SignupPanelView : MonoBehaviour
    {
        private const string MemberSubtitle = "NPC가 부를 이름과 캐릭터를 정해 주세요.";
        private const string GuestSubtitle =
            "닉네임만 정하면 캐릭터와 게스트 기록이 그대로 이어져요.";

        [SerializeField]
        [Tooltip("제목 아래 안내. 게스트가 가입할 때는 기록이 이어진다고 알린다.")]
        private TMP_Text subtitleText;

        [SerializeField]
        private TMP_InputField nicknameInput;

        [SerializeField]
        private Button checkNicknameButton;

        [SerializeField]
        private TMP_Text nicknameHintText;

        [SerializeField]
        private Button femaleButton;

        [SerializeField]
        private Button maleButton;

        [SerializeField]
        [Tooltip("약관 동의 단계로 돌아간다")]
        private Button backButton;

        [SerializeField]
        private Button submitButton;

        public event Action<string> NicknameChanged;
        public event Action CheckNicknameRequested;
        public event Action<AvatarType> AvatarSelected;
        public event Action BackRequested;
        public event Action SubmitRequested;

        private void Awake()
        {
            nicknameInput.onValueChanged.AddListener(text => NicknameChanged?.Invoke(text));
            checkNicknameButton.onClick.AddListener(() => CheckNicknameRequested?.Invoke());
            femaleButton.onClick.AddListener(() => AvatarSelected?.Invoke(AvatarType.F));
            maleButton.onClick.AddListener(() => AvatarSelected?.Invoke(AvatarType.M));
            backButton.onClick.AddListener(() => BackRequested?.Invoke());
            submitButton.onClick.AddListener(() => SubmitRequested?.Invoke());
        }

        public void Show(SignupForm form)
        {
            if (nicknameInput.text != form.Nickname)
            {
                nicknameInput.SetTextWithoutNotify(form.Nickname);
            }

            nicknameHintText.text = HintFor(form);
            checkNicknameButton.interactable =
                form.NicknameProblem == NicknameProblem.None
                && form.NicknameStatus != NicknameStatus.Available;
            AvatarButtons.ShowSelected(femaleButton, maleButton, form.Avatar);
            femaleButton.interactable = !form.IsGuestConversion;
            maleButton.interactable = !form.IsGuestConversion;
            subtitleText.text = form.IsGuestConversion ? GuestSubtitle : MemberSubtitle;
            submitButton.interactable = form.CanSubmit;
        }

        private static string HintFor(SignupForm form)
        {
            if (form.Nickname.Length == 0)
            {
                return "한글 2~8자 또는 영문·숫자 2~16자로 정해 주세요.";
            }

            if (form.NicknameProblem != NicknameProblem.None)
            {
                return NicknameRule.Describe(form.NicknameProblem);
            }

            switch (form.NicknameStatus)
            {
                case NicknameStatus.Available:
                    return "사용할 수 있는 닉네임이에요.";
                case NicknameStatus.Taken:
                    return "이미 사용 중인 닉네임이에요.";
                default:
                    return "[중복 확인]을 눌러 주세요.";
            }
        }
    }
}
