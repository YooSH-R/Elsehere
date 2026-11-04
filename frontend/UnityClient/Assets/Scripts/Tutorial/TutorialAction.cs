namespace A305.Tutorial
{
    /// <summary>튜토리얼 조작 단계(걷기·둘러보기)에서 해야 할 조작.</summary>
    public enum TutorialAction
    {
        /// <summary>WASD·방향키나 조이스틱으로 걷기</summary>
        Walk,

        /// <summary>Shift를 누른 채 달리기</summary>
        Run,

        /// <summary>Space로 뛰어오르기</summary>
        Jump,

        /// <summary>마우스 왼쪽 버튼 끌기나 화면 끌기로 시점 돌리기</summary>
        Rotate,

        /// <summary>마우스 휠로 가까이·멀리 보기</summary>
        Zoom,

        /// <summary>V로 1인칭·3인칭 바꾸기</summary>
        ToggleView,
    }
}
