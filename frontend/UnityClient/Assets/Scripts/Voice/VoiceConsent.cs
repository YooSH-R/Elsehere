using UnityEngine;

namespace A305.Voice
{
    /// <summary>
    /// 지금 사용자가 음성 처리에 동의했는지. 동의하지 않았으면 대화에서 음성 입력을 막는다(API 명세 CONV-02).
    /// 입구 화면이 BOOT-01로 정하고 마이페이지와 대화창 동의 창에서 바꾸며, 씬을 옮겨도 유지되도록 게임 전체가
    /// <see cref="Shared"/>를 함께 쓴다.
    /// </summary>
    public sealed class VoiceConsent
    {
        /// <summary>음성 처리 동의 문서 주소. 서버 ConsentCatalog의 VOICE 버전과 같게 둔다.</summary>
        public const string DocumentUrl = "/voice/2026-10";

        public static VoiceConsent Shared { get; private set; } = new VoiceConsent();

        public bool IsAgreed { get; private set; }

        public void Set(bool agreed)
        {
            IsAgreed = agreed;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetShared()
        {
            Shared = new VoiceConsent();
        }
    }
}
