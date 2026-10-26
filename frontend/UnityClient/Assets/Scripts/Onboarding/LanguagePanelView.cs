using System;
using System.Collections.Generic;
using UnityEngine;

namespace A305.Onboarding
{
    /// <summary>화면 03 배울 언어 선택. 고를 수 없는 언어는 "준비 중" 카드로 보여 준다.</summary>
    public class LanguagePanelView : MonoBehaviour
    {
        [SerializeField]
        private Transform cardsParent;

        [SerializeField]
        private ChoiceCardView cardTemplate;

        public event Action<string> LanguageChosen;

        private void Awake()
        {
            cardTemplate.gameObject.SetActive(false);
        }

        public void Show(IReadOnlyList<LanguageOption> languages)
        {
            TemplateClones.Clear(cardsParent, cardTemplate);
            foreach (LanguageOption language in languages)
            {
                ChoiceCardView card = TemplateClones.Add(cardTemplate, cardsParent);
                card.Show(
                    language.Name,
                    language.Available ? language.Description : "준비 중",
                    language.Available,
                    null
                );
                card.SetSelected(false);
                card.Button.onClick.AddListener(() =>
                    LanguageChosen?.Invoke(language.LanguageCode)
                );
            }
        }
    }
}
