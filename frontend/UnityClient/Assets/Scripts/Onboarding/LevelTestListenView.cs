using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Onboarding
{
    /// <summary>
    /// 화면 04의 듣기 문항(L2): 말한 사람, [다시 듣기] 칸, 물음, 보기 4줄. 음성 재생과 듣기 횟수 판단은 <see cref="LevelTestPanelView"/>와
    /// <see cref="LevelTestSession"/>이 맡는다.
    /// </summary>
    public class LevelTestListenView : MonoBehaviour
    {
        [SerializeField]
        private GameObject speakerPill;

        [SerializeField]
        private TMP_Text speakerText;

        [SerializeField]
        private Button playButton;

        [SerializeField]
        private TMP_Text questionText;

        [SerializeField]
        private LevelTestChoiceRow[] choiceRows;

        public event Action PlayRequested;
        public event Action<int> ChoiceSelected;

        private void Awake()
        {
            playButton.onClick.AddListener(() => PlayRequested?.Invoke());
            for (int index = 0; index < choiceRows.Length; index++)
            {
                int choice = index;
                choiceRows[index].Button.onClick.AddListener(() => ChoiceSelected?.Invoke(choice));
            }
        }

        public void Show(LevelTestItem item, int? chosenChoice, bool canPlay)
        {
            bool hasSpeaker = !string.IsNullOrEmpty(item.Speaker);
            speakerPill.SetActive(hasSpeaker);
            speakerText.text = hasSpeaker ? item.Speaker : "";
            questionText.text = item.Question;
            playButton.interactable = canPlay;
            for (int index = 0; index < choiceRows.Length; index++)
            {
                bool exists = index < item.Choices.Count;
                choiceRows[index].gameObject.SetActive(exists);
                if (exists)
                {
                    choiceRows[index].Show(item.Choices[index], chosenChoice == index);
                }
            }
        }
    }
}
