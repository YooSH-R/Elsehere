using System;
using A305.Dialogue;
using A305.Player;
using UnityEngine;

namespace A305.Learning
{
    /// <summary>
    /// 대화가 끝나면 피드백을 보이고, 대화·피드백에서 저장한 표현을 단어장에 모은다.
    /// 피드백·단어장 화면이 열려 있는 동안만 이동을 잠그며, 대화 중에는 대화가 정한 잠금을 건드리지 않는다.
    /// </summary>
    public class LearningController : MonoBehaviour
    {
        private const string DialogueSource = "대화";
        private const string FeedbackSource = "피드백";

        [SerializeField]
        private PlayerMover player;

        [SerializeField]
        private DialogueController dialogue;

        [SerializeField]
        private FeedbackExampleSource exampleSource;

        [SerializeField]
        private FeedbackView feedbackView;

        [SerializeField]
        private VocabularyView vocabularyView;

        [SerializeField]
        private LearningHudView hudView;

        private DialogueRecord feedbackRecord;

        /// <summary>피드백을 닫으면 그 대화의 NPC 키와, 정해진 흐름을 끝까지 마쳤는지를 알린다.</summary>
        public event Action<string, bool> FeedbackClosed;

        public bool IsShowingScreen => feedbackView.IsVisible || vocabularyView.IsVisible;

        private void Awake()
        {
            if (
                player == null
                || dialogue == null
                || exampleSource == null
                || feedbackView == null
                || vocabularyView == null
                || hudView == null
            )
            {
                Debug.LogError("LearningController의 참조가 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }

            dialogue.ConversationEnded += ShowFeedback;
            dialogue.ExpressionSaveRequested += SaveFromDialogue;
            feedbackView.CloseRequested += CloseFeedback;
            feedbackView.SaveRequested += SaveFromFeedback;
            feedbackView.VocabularyRequested += OpenVocabulary;
            vocabularyView.CloseRequested += CloseVocabulary;
            vocabularyView.LearnedToggleRequested += ToggleLearned;
            vocabularyView.DeleteRequested += Delete;
            hudView.VocabularyRequested += OpenVocabulary;
        }

        private void Start()
        {
            hudView.SetVocabularyCount(LearningRecord.Vocabulary.Entries.Count);
        }

        private void Update()
        {
            hudView.SetVocabularyAvailable(!dialogue.IsTalking);
        }

        private void OnDestroy()
        {
            if (dialogue != null)
            {
                dialogue.ConversationEnded -= ShowFeedback;
                dialogue.ExpressionSaveRequested -= SaveFromDialogue;
            }
        }

        // 내 발화가 없는 대화는 피드백을 만들지 않고, 이동 잠금도 건드리지 않는다.
        private void ShowFeedback(DialogueRecord record)
        {
            if (record.UserUtterances.Count == 0)
            {
                return;
            }

            feedbackRecord = record;
            // 서버 AI와 나눈 대화는 서버가 만든 피드백을, 대본 대화는 키워드 규칙으로 만든 예시를 보여 준다.
            feedbackView.Show(
                record.ServerFeedback != null
                    ? ConversationFeedback.FromServer(
                        record.NpcName,
                        record.UserUtterances.Count,
                        record.ServerFeedback
                    )
                    : new ConversationFeedback(
                        record.NpcName,
                        record.UserUtterances,
                        exampleSource.GetRules()
                    )
            );
            UpdateMovementLock();
        }

        private void CloseFeedback()
        {
            DialogueRecord closedRecord = feedbackRecord;
            if (closedRecord == null)
            {
                return;
            }

            feedbackRecord = null;
            feedbackView.Hide();
            UpdateMovementLock();
            FeedbackClosed?.Invoke(closedRecord.NpcCode, closedRecord.IsGuidedCompleted);
        }

        private void OpenVocabulary()
        {
            vocabularyView.Show(LearningRecord.Vocabulary);
            UpdateMovementLock();
        }

        private void CloseVocabulary()
        {
            vocabularyView.Hide();
            UpdateMovementLock();
        }

        private void SaveFromDialogue(string english, string korean)
        {
            Save(english, korean, DialogueSource);
        }

        private void SaveFromFeedback(string english, string korean)
        {
            Save(english, korean, FeedbackSource);
        }

        private void Save(string english, string korean, string source)
        {
            VocabularyEntry entry = LearningRecord.Vocabulary.Save(english, korean, source);
            if (entry == null)
            {
                return;
            }

            hudView.SetVocabularyCount(LearningRecord.Vocabulary.Entries.Count);
            hudView.ShowMessage(
                $"단어장에 저장했어요 · {entry.English} (저장 {entry.SaveCount}회)"
            );
        }

        private void ToggleLearned(VocabularyEntry entry)
        {
            LearningRecord.Vocabulary.ToggleLearned(entry);
            vocabularyView.Refresh();
        }

        private void Delete(VocabularyEntry entry)
        {
            LearningRecord.Vocabulary.Delete(entry);
            vocabularyView.Refresh();
            hudView.SetVocabularyCount(LearningRecord.Vocabulary.Entries.Count);
        }

        private void UpdateMovementLock()
        {
            if (dialogue.IsTalking)
            {
                return;
            }

            player.SetMovementLocked(IsShowingScreen);
        }
    }
}
