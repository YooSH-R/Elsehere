#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace A305.Ambience
{
    /// <summary>
    /// 로딩 페이지(WebGLTemplates/A305Responsive/index.html)가 게임 파일을 받는 동안 틀기 시작한 접속 음악.
    /// Web 빌드에서만 있고, 브라우저 연결은 Plugins/WebGL/LoadingPageMusic.jslib이 맡는다.
    /// </summary>
    public static class LoadingPageMusic
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern float A305_LoadingMusicVolume();

        [DllImport("__Internal")]
        private static extern void A305_SetLoadingMusic(
            float volume,
            bool playing,
            bool soundEnabled,
            float level
        );
#endif

        /// <summary>
        /// 로딩 페이지 음악의 지금 음량. 페이지에 음악이 없으면(Editor, 음원을 받지 못함) null이다.
        /// </summary>
        public static float? CurrentVolume()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            float volume = A305_LoadingMusicVolume();
            return volume < 0 ? null : volume;
#else
            return null;
#endif
        }

        /// <summary>
        /// 음악 음량과 재생 여부를 페이지 음악에 반영하고, 다음 로딩 화면도 스피커 선택(켬·끔, 고른 크기)을 따르도록 페이지에 남긴다.
        /// 페이지는 음악 음량에 고른 크기(<paramref name="level"/>)를 곱해 낸다.
        /// </summary>
        public static void Apply(float volume, bool playing, bool soundEnabled, float level)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            A305_SetLoadingMusic(volume, playing, soundEnabled, level);
#endif
        }
    }
}
