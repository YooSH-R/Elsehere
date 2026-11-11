using System;

namespace A305.World
{
    /// <summary>
    /// 미션 화면과 폰 메뉴가 함께 보는 코인 잔액. 미션 화면이 잔액을 알리면 폰 메뉴의 코인 위젯이 같은 숫자를 보이고,
    /// 폰의 [마을 지도] 앱은 미션 화면에 큰 마을 지도를 열어 달라고 알린다. 두 화면은 서로의 모듈을 모르므로 이 상태로 만난다.
    /// 퀘스트 진행은 왼쪽 위 퀘스트 알약·카드만 보인다.
    /// </summary>
    public sealed class JourneyStatus
    {
        public static JourneyStatus Shared { get; } = new JourneyStatus();

        public int Balance { get; private set; }

        public event Action Changed;

        /// <summary>폰의 [마을 지도] 앱을 눌렀다.</summary>
        public event Action MapRequested;

        public void ShowBalance(int balance)
        {
            Balance = balance;
            Changed?.Invoke();
        }

        public void Clear()
        {
            Balance = 0;
            Changed?.Invoke();
        }

        public void RequestMap() => MapRequested?.Invoke();
    }
}
