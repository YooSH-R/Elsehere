using UnityEngine;

namespace A305.Api
{
    /// <summary>
    /// 지금 접속한 계정이 게스트인지. 입구 화면이 BOOT-01 응답으로 정하고, 장면을 옮겨도 남아 장소 화면이 읽는다
    /// (첫 호텔 퀘스트를 끝낸 게스트에게 가입 권하기, <see cref="GuestSignupPrompt.RequestAfterQuest"/>).
    /// 입구를 거치지 않고 장소 씬을 바로 연 편집기 실행에서는 회원으로 본다.
    /// </summary>
    public static class PlayerAccount
    {
        public static bool IsGuest { get; private set; }

        public static void SetGuest(bool guest) => IsGuest = guest;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => IsGuest = false;
    }
}
