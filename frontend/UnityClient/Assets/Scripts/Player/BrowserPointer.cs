#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace A305.Player
{
    /// <summary>브라우저의 주 입력 장치가 손가락인지(CSS pointer: coarse). 값은 Plugins/WebGL/BrowserPointer.jslib이 읽는다.</summary>
    internal static class BrowserPointer
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        public static bool IsPrimaryCoarse { get; } = A305Player_IsPrimaryPointerCoarse();

        [DllImport("__Internal")]
        private static extern bool A305Player_IsPrimaryPointerCoarse();
#else
        public static bool IsPrimaryCoarse => false;
#endif
    }
}
