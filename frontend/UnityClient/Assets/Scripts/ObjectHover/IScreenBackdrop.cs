namespace A305.ObjectHover
{
    /// <summary>
    /// 다른 화면 위를 어둡게 덮고 한 곳만 밝히는 막(튜토리얼 코치 마크 등). 캔버스 루트의 컴포넌트가 구현하면
    /// 단어 카드는 이 캔버스를 피할 화면으로 보지 않는다. 덮개를 밝힐 곳 둘레의 여러 조각으로 나눠 그려도 화면 전체를 덮는 막처럼 다룬다.
    /// </summary>
    public interface IScreenBackdrop { }
}
