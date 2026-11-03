using System;

namespace A305.World
{
    /// <summary>
    /// 단어장·피드백·피드백 목록처럼 화면 가운데를 차지하는 창이 열려 있는지. 학습 화면이 알리면 미션 화면이 왼쪽 위 퀘스트 카드와
    /// 왼쪽 아래 미니맵을 숨기고 펼친 수첩·큰 지도를 닫아 창끼리 겹치지 않게 한다. 두 화면은 서로의 모듈을 모르므로 이 상태로 만난다.
    /// </summary>
    public sealed class CenterWindow
    {
        public static CenterWindow Shared { get; } = new CenterWindow();

        public bool IsOpen { get; private set; }

        public event Action Changed;

        public void SetOpen(bool open)
        {
            if (IsOpen == open)
            {
                return;
            }

            IsOpen = open;
            Changed?.Invoke();
        }
    }
}
