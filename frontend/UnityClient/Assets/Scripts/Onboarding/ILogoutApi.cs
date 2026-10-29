using System.Threading.Tasks;

namespace A305.Onboarding
{
    /// <summary>AUTH-05 로그아웃. 공간 화면의 [로그아웃]이 쓴다. 실패하면 <see cref="A305.Api.ApiException"/>을 던진다.</summary>
    public interface ILogoutApi
    {
        Task LogoutAsync();
    }
}
