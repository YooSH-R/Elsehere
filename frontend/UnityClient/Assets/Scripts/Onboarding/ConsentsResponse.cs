using System.Collections.Generic;

namespace A305.Onboarding
{
    /// <summary>USER-02 응답. 이 사용자가 바꿀 수 있는 선택 동의 목록이다.</summary>
    public sealed class ConsentsResponse
    {
        public List<ConsentStatus> Consents { get; set; }
    }
}
