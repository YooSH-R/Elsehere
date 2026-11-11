#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace A305.Player
{
    /// <summary>
    /// 브라우저의 주 입력 장치가 손가락인지(CSS pointer: coarse)와 화면 배율(devicePixelRatio).
    /// 값과 게임 캔버스의 마우스 커서는 Plugins/WebGL/BrowserPointer.jslib에서 다룬다.
    /// </summary>
    internal static class BrowserPointer
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        public static bool IsPrimaryCoarse { get; } = A305Player_IsPrimaryPointerCoarse();

        public static float DevicePixelRatio => A305Player_DevicePixelRatio();

        public static void SetClickCursor(bool clickable) =>
            A305Player_SetClickCursor(clickable ? 1 : 0);

        [DllImport("__Internal")]
        private static extern void A305Player_SetClickCursor(int clickable);

        [DllImport("__Internal")]
        private static extern bool A305Player_IsPrimaryPointerCoarse();

        [DllImport("__Internal")]
        private static extern float A305Player_DevicePixelRatio();
#else
        public static bool IsPrimaryCoarse => false;

        // 에디터와 데스크톱 실행은 마우스 이동을 화면 배율로 키우지 않는다.
        public static float DevicePixelRatio => 1f;

        public static void SetClickCursor(bool clickable) { }
#endif
    }
}
