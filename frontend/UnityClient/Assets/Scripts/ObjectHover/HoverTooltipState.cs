namespace A305.ObjectHover
{
    /// <summary>
    /// 포인터가 가리키는 사물과 표시 가능 여부로 툴팁에 보여 줄 사물을 정한다. 툴팁은 하나만 보인다.
    /// 마우스가 스쳐 지나갈 때 깜박이지 않도록 같은 사물을 잠시 가리킨 뒤에 보이고,
    /// 포인터가 벗어나거나 보일 수 없는 상태(이동·거리·상호작용)가 되면 바로 숨긴다.
    /// 사물을 클릭해 고정한 카드는 고정을 풀 때까지 그대로 둔다.
    /// </summary>
    public sealed class HoverTooltipState
    {
        private readonly float showDelaySeconds;
        private string hoveredKey;
        private float hoveredSeconds;
        private string pinnedKey;

        public HoverTooltipState(float showDelaySeconds)
        {
            this.showDelaySeconds = showDelaySeconds;
        }

        /// <summary>지금 툴팁에 보여 줄 사물 키. 없으면 null.</summary>
        public string VisibleKey { get; private set; }

        /// <summary>클릭으로 고정한 카드가 있는지. 고정한 카드는 커서가 벗어나도 닫지 않는다.</summary>
        public bool IsPinned => pinnedKey != null;

        /// <summary>클릭한 사물의 카드를 지연 없이 고정한다. 이미 고정한 카드가 있으면 바꾼다.</summary>
        public void Pin(string key)
        {
            Hide();
            pinnedKey = key;
            VisibleKey = key;
        }

        /// <summary>고정을 풀고 카드를 닫는다. 그 뒤의 호버는 다시 지연 시간이 지나야 보인다.</summary>
        public void Unpin()
        {
            pinnedKey = null;
            Hide();
        }

        /// <param name="hoveredKey">이번 프레임에 포인터가 가리킨 사물 키. 없으면 null.</param>
        /// <param name="canShow">이동 중이 아니고, 가까우며, 상호작용 중이 아니어서 툴팁을 보여도 되는지</param>
        public void Update(string hoveredKey, bool canShow, float deltaTimeSeconds)
        {
            // 고정한 카드는 호버로 닫거나 바꾸지 않는다.
            if (IsPinned)
            {
                return;
            }

            if (!canShow || string.IsNullOrEmpty(hoveredKey))
            {
                Hide();
                return;
            }

            if (hoveredKey != this.hoveredKey)
            {
                Hide();
                this.hoveredKey = hoveredKey;
            }

            hoveredSeconds += deltaTimeSeconds;
            if (hoveredSeconds >= showDelaySeconds)
            {
                VisibleKey = hoveredKey;
            }
        }

        private void Hide()
        {
            VisibleKey = null;
            hoveredKey = null;
            hoveredSeconds = 0f;
        }
    }
}
