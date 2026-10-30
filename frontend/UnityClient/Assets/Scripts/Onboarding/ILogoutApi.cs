using System.Threading.Tasks;

namespace A305.Onboarding
{
    /// <summary>
    /// 세션을 끝내는 API. 마이페이지의 [로그아웃](AUTH-05)과 [회원 탈퇴](USER-05)가 쓴다.
    /// 실패하면 <see cref="A305.Api.ApiException"/>을 던진다.
    /// </summary>
    public interface ILogoutApi
    {
        Task LogoutAsync();

        /// <summary>USER-05. 내용과 개인정보를 지우고 세션을 끝낸다.</summary>
        Task WithdrawAsync();
    }
}
