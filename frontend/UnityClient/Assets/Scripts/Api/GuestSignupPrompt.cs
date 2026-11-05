using System;

namespace A305.Api
{
    /// <summary>
    /// 대화를 다 쓴 게스트에게 가입을 권할 때. 마지막 대화의 피드백을 닫으면 <see cref="Request"/>로 알리고,
    /// 장소마다 있는 가입 안내 창이 받아 연다. 막힌 [말 걸기]는 가입 안내 창을 직접 연다.
    /// </summary>
    public static class GuestSignupPrompt
    {
        /// <summary>가입을 권할 때가 됐다. 가입 안내 창이 받는다.</summary>
        public static event Action Requested;

        public static void Request() => Requested?.Invoke();
    }
}
