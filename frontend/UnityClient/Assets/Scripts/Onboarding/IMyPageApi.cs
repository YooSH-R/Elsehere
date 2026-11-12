using System.Collections.Generic;
using System.Threading.Tasks;

namespace A305.Onboarding
{
    /// <summary>
    /// 마이페이지가 쓰는 API. 내 정보(BOOT-01), 닉네임 변경(USER-04), 선택 동의 조회·변경(USER-02·03),
    /// 대화 난이도 조회·변경(ONB-08·09).
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

        /// <summary>ONB-08. 배우는 언어의 지금 난이도와 고를 수 있는 단계.</summary>
        Task<LanguageProfileResponse> GetLanguageProfileAsync(string languageCode);

        /// <summary>ONB-09. 초기 설정을 마친 뒤에 바꿔도 초기 설정 단계는 그대로다.</summary>
        Task<LevelSelectionResponse> SelectLevelAsync(string languageCode, int level);
    }
}
