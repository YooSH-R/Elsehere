using System.Threading.Tasks;
using A305.Api;

namespace A305.Onboarding
{
    /// <summary>
    /// 공간 화면의 [로그아웃]. 서버 세션을 끝내고(AUTH-05) 입구 화면으로 돌아가도 되는지 정한다.
    /// 이미 로그인이 풀렸으면(401) 로그아웃한 것과 같이 본다.
    /// </summary>
    public sealed class LogoutFlow
    {
        private const string UnauthenticatedCode = "UNAUTHENTICATED";

        private readonly ILogoutApi api;

        public LogoutFlow(ILogoutApi api)
        {
            this.api = api;
        }

        /// <summary>로그아웃 요청을 기다리는 중</summary>
        public bool IsBusy { get; private set; }

        /// <summary>마지막 로그아웃이 실패한 이유. 실패하지 않았으면 null.</summary>
        public string ErrorMessage { get; private set; }

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
