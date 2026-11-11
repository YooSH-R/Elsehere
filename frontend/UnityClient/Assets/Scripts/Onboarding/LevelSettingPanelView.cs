using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Onboarding
{
    /// <summary>
    /// 화면 05 NPC 대화 난이도 설정. 고를 수 없는 단계는 "추후 제공"으로, 레벨 테스트 추천 단계에는 배지를 붙인다.
    /// </summary>
    public class LevelSettingPanelView : MonoBehaviour
    {
        [SerializeField]
        private Transform cardsParent;

        [SerializeField]
        private ChoiceCardView cardTemplate;

        [SerializeField]
        private Button confirmButton;

        private readonly Dictionary<int, ChoiceCardView> cardsByLevel =
            new Dictionary<int, ChoiceCardView>();

        private int selectedLevel;

        public event Action<int> LevelConfirmed;

        private void Awake()
        {
            cardTemplate.gameObject.SetActive(false);
            confirmButton.onClick.AddListener(() => LevelConfirmed?.Invoke(selectedLevel));
        }

        public void Show(LanguageProfileResponse profile)
        {
            TemplateClones.Clear(cardsParent, cardTemplate);
            cardsByLevel.Clear();
            foreach (ConversationLevel level in profile.Levels)
            {
                ChoiceCardView card = TemplateClones.Add(cardTemplate, cardsParent);
                card.Show(
                    level.Name,
                    level.Available ? level.Description : "추후 제공",
                    level.Available,
                    profile.RecommendedLevel == level.Level ? "레벨 테스트 추천" : null
                );
                card.Button.onClick.AddListener(() => Select(level.Level));
                cardsByLevel[level.Level] = card;
            }

            Select(InitialLevel(profile));
        }

        /// <summary>
        /// 화면 05에 들어올 때 미리 골라 둘 난이도. 고른 적이 있으면 그 단계, 없으면 레벨 테스트 추천 단계(고를 수 있을 때),
        /// 그것도 없으면 고를 수 있는 첫 단계(초급)다.
        /// </summary>
        public static int InitialLevel(LanguageProfileResponse profile)
        {
            if (profile.SelectedLevel != null)
            {
                return profile.SelectedLevel.Value;
            }

            bool canChooseRecommended = profile.Levels.Any(level =>
                level.Available && level.Level == profile.RecommendedLevel
            );
            return canChooseRecommended
                ? profile.RecommendedLevel.Value
                : profile.Levels.First(level => level.Available).Level;
        }

        private void Select(int level)
        {
            selectedLevel = level;
            foreach (KeyValuePair<int, ChoiceCardView> entry in cardsByLevel)
            {
                entry.Value.SetSelected(entry.Key == level);
            }
        }
    }
}
