namespace A305.Player
{
    /// <summary>조이스틱과 회전 드래그 영역을 보일지 정한다. 휴대폰·태블릿 브라우저에서만 보이고 PC 웹에서는 숨긴다.</summary>
    public static class TouchControlsAvailability
    {
        public static bool ShouldShow(
            bool isWebBrowser,
            bool hasTouchscreen,
            bool isMobileBrowser,
            bool isPrimaryPointerCoarse
        )
        {
            // Unity Web은 PC 브라우저에서도 Touchscreen 장치를 등록할 수 있어 장치 유무로는 PC를 가려낼 수 없다.
            // iPadOS Safari는 Mac UA를 보내므로 주 입력 장치가 손가락인지도 본다.
            // 마우스·터치패드가 주 입력인 터치 노트북은 숨긴다.
            if (isWebBrowser)
            {
                return isMobileBrowser || isPrimaryPointerCoarse;
            }

            return hasTouchscreen;
        }

        public static bool CountsAsTouchscreen(bool isEditor, bool isNative) => false;
    }
}
