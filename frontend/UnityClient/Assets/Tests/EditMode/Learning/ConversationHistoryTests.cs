using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using A305.Api;
using A305.Dialogue;
using NUnit.Framework;

namespace A305.Learning.Tests
{
    public class ConversationHistoryTests
    {
        private FakeHistoryApi api;
        private ConversationHistory history;

        [SetUp]
        public void CreateHistory()
        {
            api = new FakeHistoryApi();
            history = new ConversationHistory(api);
        }

        [Test]
        public async Task Load_ShowsRecentConversationsAndWhetherMoreExist()
        {
            api.Pages[""] = Page("58", Conversation(60), Conversation(59));

            bool loaded = await history.LoadAsync();

            Assert.That(loaded, Is.True);
            Assert.That(api.RequestedCursors, Is.EqualTo(new string[] { null }));
            Assert.That(history.Items.Count, Is.EqualTo(2));
            Assert.That(history.Items[0].ConversationId, Is.EqualTo(60));
            Assert.That(history.HasMore, Is.True);
            Assert.That(history.Message, Is.Null);
        }

        [Test]
        public async Task LoadMore_AppendsNextPageWithCursor()
        {
            api.Pages[""] = Page("59", Conversation(60), Conversation(59));
            api.Pages["59"] = Page(null, Conversation(58));
            await history.LoadAsync();

            bool loaded = await history.LoadMoreAsync();

            Assert.That(loaded, Is.True);
            Assert.That(api.RequestedCursors[1], Is.EqualTo("59"));
            Assert.That(history.Items.Count, Is.EqualTo(3));
            Assert.That(history.Items[2].ConversationId, Is.EqualTo(58));
            Assert.That(history.HasMore, Is.False);
        }

        [Test]
        public async Task LoadMore_OnLastPage_DoesNotRequest()
        {
            api.Pages[""] = Page(null, Conversation(60));
            await history.LoadAsync();

            bool loaded = await history.LoadMoreAsync();

            Assert.That(loaded, Is.False);
            Assert.That(api.RequestedCursors.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task Load_AgainAfterNewConversation_StartsFromFirstPage()
        {
            // 창을 다시 열면 방금 끝낸 대화도 보이도록 처음부터 다시 읽는다.
            api.Pages[""] = Page(null, Conversation(60));
            await history.LoadAsync();
            api.Pages[""] = Page(null, Conversation(61), Conversation(60));

            await history.LoadAsync();

            Assert.That(history.Items.Count, Is.EqualTo(2));
            Assert.That(history.Items[0].ConversationId, Is.EqualTo(61));
        }

        [Test]
        public async Task Load_WhenRequestFails_KeepsListAndTellsWhy()
        {
            api.Pages[""] = Page(null, Conversation(60));
            await history.LoadAsync();
            api.PageError = Failure("서버에 연결하지 못했어요.");

            bool loaded = await history.LoadAsync();

            Assert.That(loaded, Is.False);
            Assert.That(history.Items.Count, Is.EqualTo(1));
            Assert.That(history.IsBusy, Is.False);
            Assert.That(history.Message, Is.EqualTo("서버에 연결하지 못했어요."));
        }

        [Test]
        public async Task Load_WhileLoading_DoesNotRequestAgain()
        {
            api.Pages[""] = Page(null, Conversation(60));
            api.PageGate = new TaskCompletionSource<bool>();

            Task<bool> first = history.LoadAsync();
            bool wasBusy = history.IsBusy;
            bool second = await history.LoadAsync();
            api.PageGate.SetResult(true);
            await first;

            Assert.That(wasBusy, Is.True);
            Assert.That(second, Is.False);
            Assert.That(api.RequestedCursors.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task Open_ShowsSavedFeedbackWithTranscript()
        {
            ConversationSummary conversation = Conversation(58);
            api.Feedback = new PastFeedback
            {
                Feedback = new FeedbackData
                {
                    Status = "DONE",
                    Summary = "주문을 끝까지 해냈어요.",
                },
                DetailedFeedback = new List<DetailedFeedbackItem>
                {
                    new DetailedFeedbackItem { BetterText = "I'd like an iced latte, please." },
                },
            };
            api.Detail = new ConversationDetail
            {
                Turns = new List<TurnDetail>
                {
                    new TurnDetail
                    {
                        Seq = 1,
                        Speaker = "NPC",
                        Text = "What can I get you?",
                        MeaningKo = "뭐 드릴까요?",
                    },
                    new TurnDetail
                    {
                        Seq = 2,
                        Speaker = "USER",
                        Text = "An iced latte, please.",
                    },
                },
            };

            PastConversation past = await history.OpenAsync(conversation);

            Assert.That(api.OpenedConversationIds, Is.EqualTo(new long[] { 58, 58 }));
            Assert.That(past.NpcName, Is.EqualTo("Mia"));
            Assert.That(past.UtteranceCount, Is.EqualTo(4));
            Assert.That(past.Feedback.ConversationId, Is.EqualTo(58));
            Assert.That(past.Feedback.Summary.Summary, Is.EqualTo("주문을 끝까지 해냈어요."));
            Assert.That(
                past.Feedback.Details[0].BetterText,
                Is.EqualTo("I'd like an iced latte, please.")
            );
            Assert.That(past.Lines[0].IsNpc, Is.True);
            Assert.That(past.Lines[0].English, Is.EqualTo("What can I get you?"));
            Assert.That(past.Lines[0].Korean, Is.EqualTo("뭐 드릴까요?"));
            Assert.That(past.Lines[1].IsNpc, Is.False);
            Assert.That(past.Lines[1].English, Is.EqualTo("An iced latte, please."));
        }

        [Test]
        public async Task Open_ConversationWithoutFeedback_SaysWhy()
        {
            // 피드백을 만들기 전에 끝난 대화(진행 중 만료 등)는 평가를 지어내지 않고 이유만 보인다.
            api.Feedback = new PastFeedback { Feedback = null };

            PastConversation past = await history.OpenAsync(Conversation(58));

            Assert.That(past.Feedback.Summary, Is.Null);
            Assert.That(past.Feedback.FailureMessage, Does.Contain("피드백을 만들기 전에"));
        }

        [Test]
        public async Task Open_WhenRequestFails_ReturnsNothingAndTellsWhy()
        {
            api.OpenError = Failure("서버에 연결하지 못했어요.");

            PastConversation past = await history.OpenAsync(Conversation(58));

            Assert.That(past, Is.Null);
            Assert.That(history.IsBusy, Is.False);
            Assert.That(history.Message, Is.EqualTo("서버에 연결하지 못했어요."));
        }

        private static ConversationPage Page(string nextCursor, params ConversationSummary[] items)
        {
            return new ConversationPage
            {
                Items = new List<ConversationSummary>(items),
                NextCursor = nextCursor,
            };
        }

        private static ConversationSummary Conversation(long id)
        {
            return new ConversationSummary
            {
                ConversationId = id,
                Npc = new NpcSummary
                {
                    NpcCode = "CAFE_STAFF",
                    Name = "Mia",
                    RoleLabel = "카페 직원",
                },
                SceneCode = "CAFE",
                Status = "ENDED_BY_USER",
                StartedAt = new DateTimeOffset(2026, 10, 21, 14, 0, 0, TimeSpan.FromHours(9)),
                UserTurnCount = 4,
                FeedbackStatus = "DONE",
            };
        }

        private static ApiException Failure(string message)
        {
            return new ApiException(ApiErrorCodes.NetworkError, 0, message);
        }

        private sealed class FakeHistoryApi : IConversationHistoryApi
        {
            // 커서(첫 페이지는 빈 문자열)별 응답
            public Dictionary<string, ConversationPage> Pages { get; } =
                new Dictionary<string, ConversationPage>();

            public ApiException PageError { get; set; }

            /// <summary>정하면 목록 응답이 이 작업이 끝날 때까지 오지 않는다.</summary>
            public TaskCompletionSource<bool> PageGate { get; set; }

            public List<string> RequestedCursors { get; } = new List<string>();

            public PastFeedback Feedback { get; set; } = new PastFeedback();

            public ConversationDetail Detail { get; set; } = new ConversationDetail();

            public ApiException OpenError { get; set; }

            public List<long> OpenedConversationIds { get; } = new List<long>();

            public async Task<ConversationPage> GetPageAsync(string cursor)
            {
                RequestedCursors.Add(cursor);
                if (PageGate != null)
                {
                    await PageGate.Task;
                }

                if (PageError != null)
                {
                    throw PageError;
                }

                return Pages[cursor ?? ""];
            }

            public Task<PastFeedback> GetFeedbackAsync(long conversationId)
            {
                OpenedConversationIds.Add(conversationId);
                if (OpenError != null)
                {
                    throw OpenError;
                }

                return Task.FromResult(Feedback);
            }

            public Task<ConversationDetail> GetDetailAsync(long conversationId)
            {
                OpenedConversationIds.Add(conversationId);
                return Task.FromResult(Detail);
            }
        }
    }
}
