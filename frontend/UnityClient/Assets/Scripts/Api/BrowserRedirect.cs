using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace A305.Onboarding
{
    /// <summary>
    /// 페이지 이동으로 여는 주소(소셜 로그인)를 같은 탭에서 연다. 로그인을 마치면 서버가 클라이언트 주소로 되돌려 보낸다.
    /// </summary>
    public static class BrowserRedirect
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void A305_RedirectTo(string url);
#endif

        public static void GoTo(string url)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            A305_RedirectTo(url);
#else
            // Editor는 브라우저 쿠키를 쓰지 않아 로그인 세션이 이어지지 않는다. Editor에서는 개발용 로그인을 쓴다.
            Application.OpenURL(url);
#endif
        }
    }
}
