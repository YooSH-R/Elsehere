using System;

namespace A305.Player
{
    /// <summary>
    /// 여는 길이 여러 개인 창을 지켜보다 보이기 시작하면 ESC 목록(<see cref="UiEscape"/>)에 올리고, 사라지면 내린다.
    /// 창을 가진 쪽이 매 프레임 <see cref="Watch"/>를 부른다.
    /// </summary>
    public sealed class UiEscapeWindow
    {
        private readonly Func<bool> isShowing;
        private readonly Action close;
        private bool wasShowing;

        /// <param name="isShowing">창이 지금 보이는지</param>
        /// <param name="close">ESC를 눌렀을 때 할 일. 보통 닫기 버튼과 같다.</param>
        public UiEscapeWindow(Func<bool> isShowing, Action close)
        {
            this.isShowing = isShowing;
            this.close = close;
        }

        public void Watch()
        {
            bool showing = isShowing();
            // ESC로 한 단계만 뒤로 가 창이 그대로 보이면 다시 올려, 다음 ESC로 창을 닫는다.
            if (showing && !UiEscape.IsListed(this))
            {
                UiEscape.Opened(this, close, isShowing);
            }
            else if (!showing && wasShowing)
            {
                UiEscape.Closed(this);
            }

            wasShowing = showing;
        }
    }
}
