using System.Threading.Tasks;
using A305.Api;
using NUnit.Framework;

namespace A305.Dialogue.Tests
{
    public class ConversationAllowanceTests
    {
        [Test]
        public void BeforeServerStatus_DoesNotBlockStarting()
        {
            // 상태를 받기 전에는 막지 않는다. 최종 확인은 대화 시작에서 서버가 한다.
            var allowance = new ConversationAllowance();

            Assert.That(allowance.CanStartConversation, Is.True);
            Assert.That(allowance.RemainingConversations, Is.Null);
        }

        [Test]
        public void Apply_GuestWithConversationsLeft_KeepsRemainingCount()
        {
            var allowance = new ConversationAllowance();

            allowance.Apply(Status(canStart: true, remaining: 2));

            Assert.That(allowance.CanStartConversation, Is.True);
            Assert.That(allowance.RemainingConversations, Is.EqualTo(2));
        }

        [Test]
        public void Apply_GuestUsedAllConversations_BlocksStarting()
        {
            var allowance = new ConversationAllowance();

            allowance.Apply(Status(canStart: false, remaining: 0));

            Assert.That(allowance.CanStartConversation, Is.False);
            Assert.That(allowance.RemainingConversations, Is.EqualTo(0));
        }

        [Test]
        public void Apply_Member_HasNoRemainingCount()
        {
            var allowance = new ConversationAllowance();
            allowance.Apply(Status(canStart: true, remaining: 2));

            allowance.Apply(Status(canStart: true, remaining: null));

            Assert.That(allowance.RemainingConversations, Is.Null);
        }

        [Test]
        public async Task RefreshAsync_AppliesServerStatus()
        {
            var allowance = new ConversationAllowance();

            await allowance.RefreshAsync(
                new FakeAiStatusApi(Status(canStart: false, remaining: 0))
            );

            Assert.That(allowance.CanStartConversation, Is.False);
            Assert.That(allowance.RemainingConversations, Is.EqualTo(0));
        }

        [Test]
        public async Task RefreshAsync_WhenServerFails_KeepsLastStatus()
        {
            var allowance = new ConversationAllowance();
            allowance.Apply(Status(canStart: false, remaining: 0));

            await allowance.RefreshAsync(new FakeAiStatusApi(null));

            Assert.That(allowance.CanStartConversation, Is.False);
            Assert.That(allowance.RemainingConversations, Is.EqualTo(0));
        }

        private static AiStatusData Status(bool canStart, int? remaining)
        {
            return new AiStatusData
            {
                Status = canStart ? "AI_AVAILABLE" : "AI_EXHAUSTED",
                Capabilities = new AiCapabilities { CanStartConversation = canStart },
                RemainingConversations = remaining,
            };
        }

        // reply가 null이면 서버에 연결하지 못한 것처럼 실패한다.
        private sealed class FakeAiStatusApi : IAiStatusApi
        {
            private readonly AiStatusData reply;

            public FakeAiStatusApi(AiStatusData reply)
            {
                this.reply = reply;
            }

            public Task<AiStatusData> GetStatusAsync()
            {
                if (reply == null)
                {
                    throw new ApiException(
                        ApiErrorCodes.NetworkError,
                        0,
                        "서버에 연결하지 못했습니다."
                    );
                }

                return Task.FromResult(reply);
            }
        }
    }
}
