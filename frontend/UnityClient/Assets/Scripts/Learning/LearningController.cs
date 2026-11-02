using System;
using System.Threading.Tasks;
using A305.Api;
using A305.Dialogue;
using A305.Player;
using A305.World;
using UnityEngine;

namespace A305.Learning
{
    /// <summary>
    /// 대화가 끝나면 피드백을 보이고, 단어장 화면을 서버 단어장(VOCAB-01·03·04)과 잇는다. Editor에서 서버 단어장을 읽지 못하면
    /// 확인용 단어장(<see cref="LocalVocabularyApi"/>)으로 바꿔 같은 Play 동안 사물 카드에서 저장한 단어를 보인다.
    /// [피드백 목록] 창은 지난 대화 목록(CONV-09)에서 고른 대화의 저장된 피드백을 같은 피드백 화면으로 다시 열고, 닫으면 목록으로 돌아온다.
    /// 피드백·단어장·피드백 목록 화면이 열려 있는 동안만 이동을 잠그며, 대화 중에는 대화가 정한 잠금을 건드리지 않는다.
    /// 대화·피드백의 [표현 저장]은 서버가 그 출처의 저장을 열기 전까지 숨긴다.
    /// </summary>
    public class LearningController : MonoBehaviour
    {
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

        [SerializeField]
        [Tooltip(
            "단어장을 읽고 저장할 API 서버 설정. 비우면 단어장을 열 수 없다(서버 없이 씬만 확인할 때)."
        )]
        private ApiSettings apiSettings;

        private VocabularyBook vocabulary;
        private bool isLocalVocabulary;

        private ConversationHistory history;

        private DialogueRecord feedbackRecord;

        // [피드백 목록]에서 다시 연 대화. 대화 직후의 피드백(feedbackRecord)과 함께 열리지 않는다.
        private PastConversation pastConversation;

        // 지금 피드백 화면에 보이는 서버 피드백. [다시 만들기]로 새로 받으면 바뀐다.
        private ServerFeedback shownServerFeedback;

        /// <summary>피드백을 닫으면 그 대화의 NPC 키와, 정해진 흐름을 끝까지 마쳤는지를 알린다.</summary>
        public event Action<string, bool> FeedbackClosed;

        public bool IsShowingScreen =>
            feedbackView.IsVisible || vocabularyView.IsVisible || historyView.IsVisible;

        /// <summary>단어장 화면이 열려 있는지. 튜토리얼이 단어장을 처음 열었을 때 설명을 붙인다.</summary>
        public bool IsShowingVocabulary => vocabularyView.IsVisible;

        /// <summary>단어장 창. 튜토리얼 코치 마크가 밝힌다.</summary>
        public RectTransform VocabularyPanelRect => vocabularyView.PanelRect;

        private ConversationHistoryView historyView => hudView.HistoryView;

        private void Awake()
        {
            if (
                player == null
                || dialogue == null
                || exampleSource == null
                || feedbackView == null
                || vocabularyView == null
                || hudView == null
                || hudView.HistoryView == null
            )
            {
                Debug.LogError("LearningController의 참조가 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }

            if (apiSettings != null)
            {
                vocabulary = new VocabularyBook(new VocabularyApi(apiSettings.CreateClient()));
                history = new ConversationHistory(
                    new ConversationHistoryApi(apiSettings.CreateClient())
                );
            }

            // 서버가 대화·피드백 출처의 저장(NPC_LINE·FEEDBACK)을 아직 받지 않으므로 저장 버튼을 숨긴다.
            dialogue.SetExpressionSaveAvailable(false);
            feedbackView.SetSaveAvailable(false);

            dialogue.ConversationEnded += ShowFeedback;
            feedbackView.CloseRequested += CloseFeedback;
            feedbackView.VocabularyRequested += OpenVocabulary;
            feedbackView.RegenerateRequested += RegenerateFeedback;
            vocabularyView.CloseRequested += CloseVocabulary;
            vocabularyView.LearnedToggleRequested += ToggleLearned;
            vocabularyView.DeleteRequested += Delete;
            vocabularyView.MemoSaveRequested += SaveMemo;
            vocabularyView.RetryRequested += RetryVocabulary;
            hudView.VocabularyRequested += OpenVocabulary;
            hudView.HistoryRequested += OpenHistory;
            hudView.HomeRequested += ReturnHome;
            historyView.CloseRequested += CloseHistory;
            historyView.OpenRequested += OpenPastFeedback;
            historyView.MoreRequested += LoadMoreHistory;
        }

        private void Start()
        {
            if (vocabulary != null)
            {
                _ = LoadVocabularyAsync();
            }
        }

        private void Update()
        {
            hudView.SetVocabularyAvailable(!dialogue.IsTalking);
            hudView.SetHomeAvailable(gameObject.scene.name != "HomeScene" && !IsHomeBlocked);
        }

        private bool IsHomeBlocked =>
            dialogue.IsTalking
            || player.IsMovementLocked
            || IsShowingScreen
            || SpaceTravel.IsTraveling;

        private void ReturnHome()
        {
            if (
                HomeNavigation.TryReturn(
                    gameObject.scene.name,
                    IsHomeBlocked,
                    SpaceTravel.TryTravel
                )
            )
                player.SetMovementLocked(true);
        }

        private void OnDestroy()
        {
            if (hudView != null)
            {
                hudView.HomeRequested -= ReturnHome;
                hudView.HistoryRequested -= OpenHistory;
            }
            if (dialogue != null)
            {
                dialogue.ConversationEnded -= ShowFeedback;
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
            shownServerFeedback = record.ServerFeedback;
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
                    ),
                record.Lines
            );
            UpdateMovementLock();
        }

        private void RegenerateFeedback()
        {
            if (shownServerFeedback == null)
            {
                return;
            }

            if (feedbackRecord != null)
            {
                _ = RegenerateFeedbackAsync(feedbackRecord, shownServerFeedback);
            }
            else if (pastConversation != null)
            {
                _ = RegeneratePastFeedbackAsync(pastConversation, shownServerFeedback);
            }
        }

        private async Task RegenerateFeedbackAsync(DialogueRecord record, ServerFeedback failed)
        {
            feedbackView.ShowRegenerating();
            ServerFeedback regenerated = await dialogue.RegenerateFeedbackAsync(failed);
            // 기다리는 사이 씬을 떠났거나 피드백을 닫았으면 보여 주지 않는다.
            if (this == null || feedbackRecord != record)
            {
                return;
            }

            shownServerFeedback = regenerated;
            feedbackView.Show(
                ConversationFeedback.FromServer(
                    record.NpcName,
                    record.UserUtterances.Count,
                    regenerated
                ),
                record.Lines
            );
        }

        private void CloseFeedback()
        {
            // [피드백 목록]에서 연 피드백은 대화 결과(미션·튜토리얼)를 다시 알리지 않고 목록으로 돌아간다.
            if (pastConversation != null)
            {
                pastConversation = null;
                shownServerFeedback = null;
                feedbackView.Hide();
                UpdateMovementLock();
                return;
            }

            DialogueRecord closedRecord = feedbackRecord;
            if (closedRecord == null)
            {
                return;
            }

            feedbackRecord = null;
            shownServerFeedback = null;
            feedbackView.Hide();
            UpdateMovementLock();
            FeedbackClosed?.Invoke(closedRecord.NpcCode, closedRecord.IsGuidedCompleted);
        }

        private void OpenHistory()
        {
            if (history == null)
            {
                hudView.ShowMessage("피드백 목록 서버가 연결되지 않았어요.");
                return;
            }

            historyView.Show(history);
            UpdateMovementLock();
            _ = LoadHistoryAsync(history.LoadAsync(), "피드백 목록을 불러오는 중이에요…");
        }

        private void LoadMoreHistory()
        {
            _ = LoadHistoryAsync(history.LoadMoreAsync(), "피드백 목록을 더 불러오는 중이에요…");
        }

        // 창을 열 때마다 처음부터 다시 읽어 방금 끝낸 대화도 보인다. 실패하면 이전 목록과 이유를 보인다.
        private async Task LoadHistoryAsync(Task<bool> loading, string waitingMessage)
        {
            historyView.Refresh();
            historyView.ShowStatus(waitingMessage);
            bool loaded = await loading;
            if (this == null || !historyView.IsVisible)
            {
                return;
            }

            historyView.Refresh();
            historyView.ShowStatus(loaded ? "" : history.Message);
        }

        private void CloseHistory()
        {
            historyView.Hide();
            UpdateMovementLock();
        }

        private void OpenPastFeedback(ConversationSummary conversation)
        {
            _ = OpenPastFeedbackAsync(conversation);
        }

        // 저장된 피드백과 대화 기록을 받아 대화 직후와 같은 피드백 화면으로 연다. 피드백 목록 창은 그 아래에 그대로 둔다.
        private async Task OpenPastFeedbackAsync(ConversationSummary conversation)
        {
            historyView.ShowStatus("피드백을 불러오는 중이에요…");
            PastConversation past = await history.OpenAsync(conversation);
            if (this == null || !historyView.IsVisible)
            {
                return;
            }

            if (past == null)
            {
                historyView.ShowStatus(history.Message);
                return;
            }

            historyView.ShowStatus("");
            pastConversation = past;
            shownServerFeedback = past.Feedback;
            feedbackView.Show(
                ConversationFeedback.FromServer(past.NpcName, past.UtteranceCount, past.Feedback),
                past.Lines
            );
            UpdateMovementLock();
        }

        private async Task RegeneratePastFeedbackAsync(PastConversation past, ServerFeedback failed)
        {
            feedbackView.ShowRegenerating();
            ServerFeedback regenerated = await dialogue.RegenerateFeedbackAsync(failed);
            // 기다리는 사이 씬을 떠났거나 피드백을 닫았으면 보여 주지 않는다.
            if (this == null || pastConversation != past)
            {
                return;
            }

            shownServerFeedback = regenerated;
            feedbackView.Show(
                ConversationFeedback.FromServer(past.NpcName, past.UtteranceCount, regenerated),
                past.Lines
            );
        }

        private void OpenVocabulary()
        {
            if (vocabulary == null)
            {
                hudView.ShowMessage("단어장 서버가 연결되지 않았어요.");
                return;
            }

            // 읽기를 먼저 시작해 열자마자 '불러오는 중'이나 지난번 목록이 보이게 한다. 받기 전의 빈 목록을 '단어 없음'으로 보이지 않는다.
            Task loading = LoadVocabularyAsync();
            vocabularyView.Show(vocabulary);
            // 지난번 목록을 보이는 동안에만 아래에 새로 읽는 중임을 알린다. 목록이 없으면 가운데 안내가 같은 말을 한다.
            if (!loading.IsCompleted && vocabulary.ListState == VocabularyListState.Entries)
            {
                vocabularyView.ShowStatus("단어장을 불러오는 중이에요…");
            }

            UpdateMovementLock();
        }

        // 다른 기기나 이전 접속에서 바뀐 내용도 보이도록 단어장을 열 때마다 서버에서 다시 읽는다.
        private async Task LoadVocabularyAsync()
        {
            VocabularyBook book = vocabulary;
            bool loaded = await book.LoadAsync();
            // 먼저 끝난 다른 불러오기가 확인용 단어장으로 바꿨으면 그쪽이 화면을 그린다.
            if (this == null || book != vocabulary)
            {
                return;
            }

            // Editor에서는 서버 없이도 사물 카드에서 저장한 단어를 볼 수 있게 확인용 단어장으로 바꾼다. 빌드는 실패 안내를 그대로 보인다.
            if (!loaded && Application.isEditor && !isLocalVocabulary)
            {
                Debug.LogWarning(
                    "서버 단어장을 읽지 못해 Editor 확인용 단어장으로 이 Play 동안 저장한 단어를 보입니다.",
                    this
                );
                vocabulary = new VocabularyBook(new LocalVocabularyApi());
                isLocalVocabulary = true;
                loaded = await vocabulary.LoadAsync();
                if (vocabularyView.IsVisible)
                {
                    vocabularyView.Show(vocabulary);
                }
            }

            hudView.SetVocabularyCount(vocabulary.Entries.Count);
            if (vocabularyView.IsVisible)
            {
                vocabularyView.Refresh();
                // 목록이 없을 때의 실패는 가운데 [다시 시도]와 함께 보이므로 아래 안내에는 쓰지 않는다.
                bool showsFailureInCenter = vocabulary.ListState == VocabularyListState.Failed;
                vocabularyView.ShowStatus(
                    loaded || showsFailureInCenter ? "" : vocabulary.LastFailureMessage
                );
            }
        }

        // 단어장을 불러오지 못한 화면의 [다시 시도]. 다시 읽는 동안 가운데에 '불러오는 중'을 보인다.
        private void RetryVocabulary()
        {
            _ = LoadVocabularyAsync();
            vocabularyView.Refresh();
        }

        private void CloseVocabulary()
        {
            vocabularyView.Hide();
            UpdateMovementLock();
        }

        private void ToggleLearned(VocabularyEntry entry)
        {
            _ = ApplyVocabularyChangeAsync(vocabulary.ToggleLearnedAsync(entry), "");
        }

        private void Delete(VocabularyEntry entry)
        {
            _ = ApplyVocabularyChangeAsync(vocabulary.DeleteAsync(entry), "단어장에서 지웠어요.");
        }

        private void SaveMemo(VocabularyEntry entry, string memo)
        {
            _ = ApplyVocabularyChangeAsync(
                vocabulary.SaveMemoAsync(entry, memo),
                "메모를 저장했어요."
            );
        }

        // 서버 저장이 끝난 뒤에 화면을 다시 그린다. 실패하면 값은 그대로이고 이유를 보여 준다.
        private async Task ApplyVocabularyChangeAsync(Task<bool> change, string doneMessage)
        {
            vocabularyView.ShowStatus("저장하는 중이에요…");
            bool saved = await change;
            if (this == null || !vocabularyView.IsVisible)
            {
                return;
            }

            vocabularyView.Refresh();
            vocabularyView.ShowStatus(saved ? doneMessage : vocabulary.LastFailureMessage);
            hudView.SetVocabularyCount(vocabulary.Entries.Count);
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
