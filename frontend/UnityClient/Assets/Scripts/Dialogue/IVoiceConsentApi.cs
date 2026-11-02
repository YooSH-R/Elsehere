using System.Threading.Tasks;

namespace A305.Dialogue
{
    /// <summary>
    /// 음성 처리 동의를 켠다. 실패하면 <see cref="A305.Api.ApiException"/>을 던진다. 테스트에서는 가짜 구현으로 바꾼다.
    /// </summary>
    public interface IVoiceConsentApi
    {
        /// <summary>USER-03 선택 동의 중 음성 처리(VOICE)를 켠다.</summary>
        Task AgreeAsync();
    }
}
