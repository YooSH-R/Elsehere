using System.Threading.Tasks;
using A305.Api;
using NUnit.Framework;

namespace A305.Onboarding.Tests
{
    public class LogoutFlowTests
    {
        private FakeLogoutApi api;
        private LogoutFlow flow;

        [SetUp]
        public void CreateFlow()
        {
            api = new FakeLogoutApi();
            flow = new LogoutFlow(api);
        }

        [Test]
        public async Task Logout_EndsSessionAndAllowsReturningToEntry()
        {
            bool canLeave = await flow.LogoutAsync();

            Assert.That(canLeave, Is.True);
            Assert.That(api.Calls, Is.EqualTo(1));
            Assert.That(flow.ErrorMessage, Is.Null);
        }

        [Test]
        public async Task Logout_WhenSessionAlreadyEnded_StillReturnsToEntry()
        {
            // 세션이 이미 끝났으면 서버에서 할 일이 없으므로 로그인 화면으로 보낸다.
            api.Error = new ApiException("UNAUTHENTICATED", 401, "로그인이 필요합니다.");

            bool canLeave = await flow.LogoutAsync();

            Assert.That(canLeave, Is.True);
            Assert.That(flow.ErrorMessage, Is.Null);
        }

        [Test]
        public async Task Logout_WhenRequestFails_StaysAndTellsWhy()
        {
            api.Error = new ApiException(
                ApiErrorCodes.NetworkError,
                0,
                "서버에 연결하지 못했어요."
            );

            bool canLeave = await flow.LogoutAsync();

            Assert.That(canLeave, Is.False);
            Assert.That(flow.IsBusy, Is.False);
            Assert.That(flow.ErrorMessage, Is.EqualTo("서버에 연결하지 못했어요."));
        }

        [Test]
        public async Task Logout_RetryAfterFailure_ClearsReason()
        {
            api.Error = new ApiException(
                ApiErrorCodes.NetworkError,
                0,
                "서버에 연결하지 못했어요."
            );
            await flow.LogoutAsync();
            api.Error = null;

            bool canLeave = await flow.LogoutAsync();

            Assert.That(canLeave, Is.True);
            Assert.That(flow.ErrorMessage, Is.Null);
        }

        [Test]
        public async Task Logout_WhileLoggingOut_DoesNotSendAgain()
        {
            api.Gate = new TaskCompletionSource<bool>();

            Task<bool> first = flow.LogoutAsync();
            bool wasBusy = flow.IsBusy;
            bool second = await flow.LogoutAsync();
            api.Gate.SetResult(true);
            await first;

            Assert.That(wasBusy, Is.True);
            Assert.That(second, Is.False);
            Assert.That(api.Calls, Is.EqualTo(1));
        }

        private sealed class FakeLogoutApi : ILogoutApi
        {
            public int Calls { get; private set; }

            public ApiException Error { get; set; }

            /// <summary>정하면 응답이 이 작업이 끝날 때까지 오지 않는다.</summary>
            public TaskCompletionSource<bool> Gate { get; set; }

            public async Task LogoutAsync()
            {
                Calls++;
                if (Gate != null)
                {
                    await Gate.Task;
                }

                if (Error != null)
                {
                    throw Error;
                }
            }
        }
    }
}
