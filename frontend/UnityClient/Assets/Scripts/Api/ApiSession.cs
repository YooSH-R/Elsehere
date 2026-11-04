using System;

namespace A305.Api
{
    /// <summary>
    /// 서버가 로그인이 풀렸다고(401 UNAUTHENTICATED) 알린 것을 화면에 전한다. 로그인 세션은 서버 메모리에 있어
    /// 서버를 다시 배포하거나 오래 쉬면 사라진다. 장소 화면은 이 알림을 받아 처음 화면으로 돌려보내고,
    /// 로그인 전의 입구 화면은 같은 응답을 받아도 듣는 화면이 없다.
    /// </summary>
    public static class ApiSession
    {
        public static event Action Expired;

        internal static void NotifyExpired()
        {
            Expired?.Invoke();
        }
    }
}
