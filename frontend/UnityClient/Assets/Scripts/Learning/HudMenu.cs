using System;

namespace A305.Learning
{
    /// <summary>
    /// 오른쪽 위 메뉴(≡)의 펼침 상태. 메뉴 버튼은 펼치고 접으며, 항목을 고르거나 바깥을 누르면 접는다.
    /// 상태가 바뀔 때만 알린다.
    /// </summary>
    public sealed class HudMenu
    {
        public bool IsOpen { get; private set; }

        public event Action<bool> Changed;

        public void Toggle() => Set(!IsOpen);

        public void Close() => Set(false);

        private void Set(bool isOpen)
        {
            if (IsOpen == isOpen)
                return;
            IsOpen = isOpen;
            Changed?.Invoke(isOpen);
        }
    }
}
