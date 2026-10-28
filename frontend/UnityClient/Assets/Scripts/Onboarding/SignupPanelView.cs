using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Onboarding
{
    /// <summary>화면 02 가입 정보 입력. 닉네임·캐릭터·약관 입력을 전달하고, <see cref="SignupForm"/>의 상태를 보여 준다.</summary>
    public class SignupPanelView : MonoBehaviour
    {
        private static readonly Color SelectedColor = new Color(0.24f, 0.42f, 0.85f, 1f);
        private static readonly Color NormalColor = new Color(0.3f, 0.32f, 0.38f, 1f);

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
        private Transform consentsParent;

        [SerializeField]
        private ConsentRowView consentTemplate;

        [SerializeField]
        private Button submitButton;

        private IReadOnlyList<ConsentTerm> shownTerms;

        public event Action<string> NicknameChanged;
        public event Action CheckNicknameRequested;
        public event Action<AvatarType> AvatarSelected;
        public event Action<string, bool> ConsentChanged;
        public event Action<string> ConsentDocumentRequested;
        public event Action SubmitRequested;

        private void Awake()
        {
            consentTemplate.gameObject.SetActive(false);
            nicknameInput.onValueChanged.AddListener(text => NicknameChanged?.Invoke(text));
            checkNicknameButton.onClick.AddListener(() => CheckNicknameRequested?.Invoke());
            femaleButton.onClick.AddListener(() => AvatarSelected?.Invoke(AvatarType.F));
            maleButton.onClick.AddListener(() => AvatarSelected?.Invoke(AvatarType.M));
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
            ((Image)femaleButton.targetGraphic).color =
                form.Avatar == AvatarType.F ? SelectedColor : NormalColor;
            ((Image)maleButton.targetGraphic).color =
                form.Avatar == AvatarType.M ? SelectedColor : NormalColor;

            if (shownTerms != form.Terms)
            {
                ShowConsents(form);
            }

            submitButton.interactable = form.CanSubmit;
        }

        private void ShowConsents(SignupForm form)
        {
            shownTerms = form.Terms;
            TemplateClones.Clear(consentsParent, consentTemplate);
            foreach (ConsentTerm term in form.Terms)
            {
                ConsentRowView row = TemplateClones.Add(consentTemplate, consentsParent);
                row.Show(term);
                row.Toggle.SetIsOnWithoutNotify(form.IsAgreed(term.ConsentType));
                row.Toggle.onValueChanged.AddListener(agreed =>
                    ConsentChanged?.Invoke(term.ConsentType, agreed)
                );
                row.ViewButton.onClick.AddListener(() =>
                    ConsentDocumentRequested?.Invoke(term.Url)
                );
            }
        }

        private static string HintFor(SignupForm form)
        {
            if (form.Nickname.Length == 0)
            {
                return "한글 2~8자 또는 영문·숫자 2~16자로 정해 주세요.";
            }

            switch (form.NicknameProblem)
            {
                case NicknameProblem.TooShort:
                    return "2자 이상 입력해 주세요.";
                case NicknameProblem.TooLong:
                    return "한글은 8자, 영문·숫자는 16자까지 쓸 수 있어요.";
                case NicknameProblem.InvalidCharacter:
                    return "한글, 영문, 숫자만 쓸 수 있어요.";
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
