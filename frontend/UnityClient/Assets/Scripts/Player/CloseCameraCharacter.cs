namespace A305.Player
{
    /// <summary>
    /// 벽에 밀려 캐릭터에 너무 가까워진 3인칭 카메라에서 캐릭터를 숨길지 정한다. 머리 속이 화면을 덮지 않게 하려는 것이며,
    /// 기준 근처에서 깜빡이지 않도록 숨기는 거리보다 조금 더 멀어져야 다시 보인다.
    /// </summary>
    public sealed class CloseCameraCharacter
    {
        public const float HideBelowMeters = 1.2f;
        public const float ShowAboveMeters = 1.5f;

        private bool hidden;

        /// <param name="cameraDistanceMeters">카메라 기준점(머리 높이)에서 카메라까지의 거리</param>
        public bool IsHidden(float cameraDistanceMeters)
        {
            if (cameraDistanceMeters < HideBelowMeters)
                hidden = true;
            else if (cameraDistanceMeters > ShowAboveMeters)
                hidden = false;
            return hidden;
        }
    }
}
