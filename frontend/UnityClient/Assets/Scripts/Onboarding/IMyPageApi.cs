using System.Collections.Generic;
using System.Threading.Tasks;

namespace A305.Onboarding
{
    /// <summary>
    /// 마이페이지가 쓰는 API. 내 정보(BOOT-01), 닉네임 변경(USER-04), 선택 동의 조회·변경(USER-02·03).
    /// 실패하면 <see cref="A305.Api.ApiException"/>을 던진다.
    /// </summary>
    public interface IMyPageApi
    {
        Task<BootstrapResponse> GetBootstrapAsync();

        Task<NicknameChange> ChangeNicknameAsync(string nickname);

        /// <summary>USER-02. 이 사용자가 바꿀 수 있는 선택 동의. 게스트는 음성 처리만 받는다.</summary>
        Task<List<ConsentStatus>> GetConsentsAsync();

        /// <summary>USER-03. 바꾼 뒤의 동의 상태를 돌려준다.</summary>
        Task<ConsentStatus> ChangeConsentAsync(string consentType, bool agreed);
    }
}
