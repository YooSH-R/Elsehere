using System.Threading.Tasks;
using A305.Api;

namespace A305.Onboarding
{
    /// <summary>
    /// 마이페이지의 [로그아웃]과 [회원 탈퇴]. 서버 세션을 끝내고(AUTH-05, USER-05) 입구 화면으로 돌아가도 되는지 정한다.
    /// 로그아웃은 이미 로그인이 풀렸으면(401) 로그아웃한 것과 같이 보지만, 탈퇴는 그때 지워지지 않았으므로 다시 로그인하라고 알린다.
    /// </summary>
    public sealed class LogoutFlow
    {
        private const string UnauthenticatedCode = "UNAUTHENTICATED";
        private const string SignInAgainMessage =
            "로그인이 풀려 탈퇴하지 못했어요. 다시 로그인한 뒤 탈퇴해 주세요.";

        private readonly ILogoutApi api;

        public LogoutFlow(ILogoutApi api)
        {
            this.api = api;
        }

        /// <summary>로그아웃·탈퇴 요청을 기다리는 중</summary>
        public bool IsBusy { get; private set; }

        /// <summary>마지막 로그아웃·탈퇴가 실패한 이유. 실패하지 않았으면 null.</summary>
        public string ErrorMessage { get; private set; }

        /// <returns>탈퇴해 입구 화면으로 돌아가도 되면 true</returns>
        public async Task<bool> WithdrawAsync()
        {
            if (IsBusy)
            {
                return false;
            }

            IsBusy = true;
            ErrorMessage = null;
            try
            {
                await api.WithdrawAsync();
                return true;
            }
            catch (ApiException exception) when (exception.Code == UnauthenticatedCode)
            {
                ErrorMessage = SignInAgainMessage;
                return false;
            }
            catch (ApiException exception)
            {
                ErrorMessage = exception.Message;
                return false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <returns>입구 화면으로 돌아가도 되면 true</returns>
        public async Task<bool> LogoutAsync()
        {
            if (IsBusy)
            {
                return false;
            }

            IsBusy = true;
            ErrorMessage = null;
            try
            {
                await api.LogoutAsync();
                return true;
            }
            catch (ApiException exception) when (exception.Code == UnauthenticatedCode)
            {
                return true;
            }
            catch (ApiException exception)
            {
                ErrorMessage = exception.Message;
                return false;
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
