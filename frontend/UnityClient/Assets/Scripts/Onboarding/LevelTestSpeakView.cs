using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Onboarding
{
    /// <summary>
    /// 화면 04의 말하기 문항(L3): 상황 카드와 말할 정보, 왼쪽 답 칸(말하면 받아 적고, 고치거나 직접 쓸 수 있음), 오른쪽 마이크 버튼.
    /// 녹음 상태는 답 칸 위 안내 문구로 알린다.
    /// </summary>
    public class LevelTestSpeakView : MonoBehaviour
    {
        /// <summary>답 칸 위 안내 문구가 알리는 상태</summary>
        public enum AnswerNote
        {
            Ready,
            NoVoiceConsent,
            MicrophoneUnavailable,
            Preparing,
            Recording,
            TooQuiet,
        }

        [SerializeField]
        private TMP_Text situationText;

        [SerializeField]
        private Transform cuesParent;

        [SerializeField]
        [Tooltip("말할 정보 칩. 정보 수만큼 복제한다")]
        private RectTransform cueTemplate;

        [SerializeField]
        private TMP_InputField answerInput;

        [SerializeField]
        private TMP_Text answerNoteText;

        [SerializeField]
        private Button microphoneButton;

        private LevelTestItem shownItem;

        public event Action<string> AnswerChanged;
        public event Action MicrophoneRequested;

        private void Awake()
        {
            cueTemplate.gameObject.SetActive(false);
            answerInput.onValueChanged.AddListener(answer => AnswerChanged?.Invoke(answer));
            microphoneButton.onClick.AddListener(() => MicrophoneRequested?.Invoke());
        }

        public void Show(LevelTestItem item, string answer, bool canSpeak)
        {
            if (item != shownItem)
            {
                shownItem = item;
                // 시안처럼 상황 문장과 할 일 문장을 줄을 나눠 보인다.
                situationText.text = item.Question.Replace(". ", ".\n");
                ShowCues(item);
            }

            if (answerInput.text != answer)
            {
                answerInput.SetTextWithoutNotify(answer);
            }

            microphoneButton.interactable = canSpeak;
        }

        public void ShowNote(AnswerNote note)
        {
            answerNoteText.text = note switch
            {
                AnswerNote.NoVoiceConsent => "내 답 · 음성 처리 동의 전이라 직접 써 주세요",
                AnswerNote.MicrophoneUnavailable => "내 답 · 마이크를 쓸 수 없어요. 직접 써 주세요",
                AnswerNote.Preparing => "내 답 · 마이크를 여는 중이에요",
                AnswerNote.Recording => "내 답 · 듣고 있어요. 다 말하면 마이크를 누르세요",
                AnswerNote.TooQuiet => "내 답 · 잘 들리지 않았어요. 다시 말해 주세요",
                _ => "내 답 · 말하면 여기에 적혀요. 고치거나 직접 써도 돼요",
            };
        }

        private void ShowCues(LevelTestItem item)
        {
            TemplateClones.Clear(cuesParent, cueTemplate);
            foreach (string cue in item.Cues)
            {
                RectTransform chip = TemplateClones.Add(cueTemplate, cuesParent);
                chip.GetComponentInChildren<TMP_Text>().text = cue;
            }
        }
    }
}
