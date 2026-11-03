using System;
using UnityEngine;

namespace A305.Wayfinding
{
    /// <summary>
    /// 사용자가 도로 위 길 안내를 켜 두었는지. 처음에는 켜져 있다. 출입문 빛은 이 값과 상관없이 보인다.
    /// 화면 취향이라 서버가 아니라 이 기기(Web 빌드는 브라우저)에 저장한다.
    /// </summary>
    public static class GuideRoutePreference
    {
        public const string PreferenceKey = "A305.Wayfinding.ShowsRoute";

        private static bool? showsRoute;

        public static event Action<bool> Changed;

        public static bool ShowsRoute
        {
            get
            {
                showsRoute ??= PlayerPrefs.GetInt(PreferenceKey, 1) == 1;
                return showsRoute.Value;
            }
            set
            {
                showsRoute = value;
                PlayerPrefs.SetInt(PreferenceKey, value ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke(value);
            }
        }

        /// <summary>저장된 값을 다시 읽는다. 테스트에서 저장을 확인할 때 쓴다.</summary>
        public static void Reload()
        {
            showsRoute = null;
        }
    }
}
