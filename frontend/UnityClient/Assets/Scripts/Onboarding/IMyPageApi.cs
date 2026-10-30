using System.Threading.Tasks;

namespace A305.Onboarding
{
    /// <summary>마이페이지가 쓰는 API. 내 정보(BOOT-01)와 닉네임 변경(USER-04). 실패하면 <see cref="A305.Api.ApiException"/>을 던진다.</summary>
    public interface IMyPageApi
    {
        Task<BootstrapResponse> GetBootstrapAsync();

        Task<NicknameChange> ChangeNicknameAsync(string nickname);
    }
}
