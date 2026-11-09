namespace A305.Learning
{
    /// <summary>
    /// 조작법 카드의 문구. PC는 키보드·마우스, 휴대폰·태블릿은 조이스틱·끌기·탭을 보인다.
    /// [E 대화하기]처럼 그 자리에서 버튼으로 뜨는 조작은 넣지 않는다.
    /// </summary>
    public static class ControlsGuideText
    {
        /// <summary>카드 제목이자 메뉴(≡) 항목 이름</summary>
        public const string MenuLabel = "조작법";

        private const string Keyboard =
            "<b>WASD</b> 이동 · <b>Shift</b> 달리기 · <b>Space</b> 점프\n"
            + "<b>마우스 왼쪽 끌기</b> 시점 돌리기\n"
            + "<b>휠</b> 가까이·멀리 · <b>V</b> 1인칭·3인칭\n"
            + "<b>사물 클릭</b> 단어 카드 · <b>NPC 클릭</b> 말 걸기";

        private const string Touch =
            "<b>왼쪽 조이스틱</b> 이동, 끝까지 밀면 달리기 · <b>점프 버튼</b> 점프\n"
            + "<b>오른쪽 끌기</b> 시점 돌리기 · <b>1인칭 버튼</b> 1인칭·3인칭\n"
            + "<b>사물 탭</b> 단어 카드 · <b>NPC 탭</b> 말 걸기";

        public static string ReopenHint => $"닫아도 메뉴의 [{MenuLabel}]으로 다시 볼 수 있어요.";

        public static string Body(bool isTouchDevice) => isTouchDevice ? Touch : Keyboard;
    }
}
