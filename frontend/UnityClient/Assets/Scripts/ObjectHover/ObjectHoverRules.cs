namespace A305.ObjectHover
{
    /// <summary>사물 호버 카드를 띄워도 되는 상황인지 정한다.</summary>
    public static class ObjectHoverRules
    {
        /// <param name="isPointerOverUi">포인터가 HUD 버튼·메뉴·창 같은 UI 위에 있는지. 호버 카드 자신은 세지 않는다.</param>
        /// <param name="isMoving">플레이어가 걷는 중인지</param>
        /// <param name="isMovementLocked">대화·창처럼 이동이 잠긴 상호작용 중인지</param>
        /// <param name="isWithinReach">가리킨 사물이 최대 거리 안에 있는지</param>
        /// <param name="isTouchDevice">휴대폰·태블릿처럼 손가락으로 조작하는 기기인지</param>
        public static bool CanHover(
            bool isPointerOverUi,
            bool isMoving,
            bool isMovementLocked,
            bool isWithinReach,
            bool isTouchDevice
        )
        {
            // 터치 화면에는 호버가 없으므로 사물을 탭해 고정한 카드만 보인다.
            return !isTouchDevice
                && !isPointerOverUi
                && !isMoving
                && !isMovementLocked
                && isWithinReach;
        }
    }
}
