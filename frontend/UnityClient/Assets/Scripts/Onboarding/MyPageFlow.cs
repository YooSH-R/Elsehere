using System;
using System.Threading.Tasks;
using A305.Api;

namespace A305.Onboarding
{
    /// <summary>
    /// 마이페이지의 내 정보와 닉네임 변경. 열 때 BOOT-01로 내 정보를 읽고, 닉네임은 가입과 같은 규칙으로 먼저 확인한 뒤 USER-04로 바꾼다.
    /// 결과와 실패 이유는 <see cref="Message"/>로 알린다.
    /// </summary>
    public sealed class MyPageFlow
    {
        private const string LoadFailedMessage = "내 정보를 불러오지 못했어요.";

        private readonly IMyPageApi api;

        public MyPageFlow(IMyPageApi api)
        {
            this.api = api;
        }

        /// <summary>읽어 온 내 정보. 읽기 전이나 읽지 못했으면 null.</summary>
        public OnboardingUser User { get; private set; }

        /// <summary>요청을 기다리는 중</summary>
        public bool IsBusy { get; private set; }

        /// <summary>마지막 동작의 결과나 실패 이유. 없으면 null.</summary>
        public string Message { get; private set; }

        /// <summary>마이페이지를 열 때마다 다시 읽어 다른 기기에서 바꾼 내용도 보인다.</summary>
        public async Task LoadAsync()
        {
            await RunAsync(async () =>
            {
                BootstrapResponse boot = await api.GetBootstrapAsync();
                User = boot.User;
                if (User == null)
                {
                    Message = LoadFailedMessage;
                }
            });
        }

        /// <returns>닉네임을 바꿨으면 true</returns>
        public async Task<bool> ChangeNicknameAsync(string nickname)
        {
            if (User == null || IsBusy)
            {
                return false;
            }

            NicknameProblem problem = NicknameRule.Check(nickname);
            if (problem != NicknameProblem.None)
            {
                Message = NicknameRule.Describe(problem);
                return false;
            }

            if (nickname == User.Nickname)
            {
                Message = "지금 쓰는 닉네임이에요.";
                return false;
            }

            bool changed = false;
            await RunAsync(async () =>
            {
                NicknameChange saved = await api.ChangeNicknameAsync(nickname);
                User.Nickname = saved.Nickname;
                Message = "닉네임을 바꿨어요.";
                changed = true;
            });
            return changed;
        }

        // 서버가 거절하거나 연결하지 못하면 바꾸지 않고 서버가 준 이유를 남긴다.
        private async Task RunAsync(Func<Task> request)
        {
            IsBusy = true;
            Message = null;
            try
            {
                await request();
            }
            catch (ApiException exception)
            {
                Message = exception.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
