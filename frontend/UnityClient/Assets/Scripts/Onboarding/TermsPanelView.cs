using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Onboarding
{
    /// <summary>
    /// 화면 02 가입의 첫 단계: 약관 동의. [전체 동의], 약관 줄(서버 약관 수만큼 복제), 음성 처리 안내, [다음]을 둔다.
    /// 동의 상태는 <see cref="SignupForm"/>이 갖고, 이 화면은 입력을 전달하고 그 상태를 보여 준다.
    /// </summary>
    public class TermsPanelView : MonoBehaviour
    {
        public const string AllAgreeLabel = "전체 동의";

        [SerializeField]
        [Tooltip("누르면 필수·선택 약관을 모두 켜거나 끈다")]
        private ConsentRowView allAgreeRow;

        [SerializeField]
        private Transform consentsParent;

        [SerializeField]
        private ConsentRowView consentTemplate;

        [SerializeField]
        private Button nextButton;

        private readonly List<KeyValuePair<string, ConsentRowView>> rows =
            new List<KeyValuePair<string, ConsentRowView>>();
        private IReadOnlyList<ConsentTerm> shownTerms;

        public event Action<bool> AllAgreeChanged;
        public event Action<string, bool> ConsentChanged;
        public event Action<string> DocumentRequested;
        public event Action NextRequested;

        private void Awake()
        {
            consentTemplate.gameObject.SetActive(false);
            allAgreeRow.ShowPlain(AllAgreeLabel);
            allAgreeRow.Toggle.onValueChanged.AddListener(agreed =>
                AllAgreeChanged?.Invoke(agreed)
            );
            nextButton.onClick.AddListener(() => NextRequested?.Invoke());
        }

        /// <summary>약관 줄과 [전체 동의]의 체크를 지금 동의 상태로 맞추고, 필수 약관에 모두 동의했을 때만 [다음]을 켠다.</summary>
        public void Show(SignupForm form)
        {
            if (shownTerms != form.Terms)
            {
                ShowConsents(form);
            }

            foreach (KeyValuePair<string, ConsentRowView> row in rows)
            {
                row.Value.Toggle.SetIsOnWithoutNotify(form.IsAgreed(row.Key));
            }

            allAgreeRow.Toggle.SetIsOnWithoutNotify(form.IsAllAgreed);
            nextButton.interactable = form.CanContinueFromTerms;
        }

        private void ShowConsents(SignupForm form)
        {
            shownTerms = form.Terms;
            rows.Clear();
            TemplateClones.Clear(consentsParent, consentTemplate);
            foreach (ConsentTerm term in form.Terms)
            {
                ConsentRowView row = TemplateClones.Add(consentTemplate, consentsParent);
                row.Show(term);
                row.Toggle.onValueChanged.AddListener(agreed =>
                    ConsentChanged?.Invoke(term.ConsentType, agreed)
                );
                row.ViewButton.onClick.AddListener(() => DocumentRequested?.Invoke(term.Url));
                rows.Add(new KeyValuePair<string, ConsentRowView>(term.ConsentType, row));
            }
        }
    }
}
