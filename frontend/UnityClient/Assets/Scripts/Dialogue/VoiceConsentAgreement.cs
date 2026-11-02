using System.Threading.Tasks;
using A305.Api;
using A305.Voice;

namespace A305.Dialogue
{
    /// <summary>
    /// 대화창 동의 창의 [동의하고 켜기]. 서버에 음성 처리 동의를 저장하고, 저장되면 게임 전체의 동의 상태를 켠다.
    /// </summary>
    public sealed class VoiceConsentAgreement
    {
        public const string SaveFailedMessage =
            "동의를 저장하지 못했어요. 잠시 뒤 다시 눌러 주세요.";

        private readonly IVoiceConsentApi api;
        private readonly VoiceConsent consent;

        public VoiceConsentAgreement(IVoiceConsentApi api, VoiceConsent consent)
        {
            this.api = api;
            this.consent = consent;
        }

        public bool IsBusy { get; private set; }

        /// <summary>저장하지 못한 이유. 저장했거나 아직 누르지 않았으면 null이다.</summary>
        public string Message { get; private set; }

        /// <returns>동의를 저장했으면 true. 저장하는 중에 다시 누르면 요청을 보내지 않고 false다.</returns>
        public async Task<bool> AgreeAsync()
        {
            if (IsBusy)
            {
                return false;
            }

            IsBusy = true;
            Message = null;
            try
            {
                await api.AgreeAsync();
                consent.Set(true);
                return true;
            }
            catch (ApiException)
            {
                Message = SaveFailedMessage;
                return false;
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
