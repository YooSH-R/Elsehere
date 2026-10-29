namespace A305.Player
{
    public enum CameraViewMode
    {
        ThirdPerson,
        FirstPerson,

        /// <summary>NPC와 1:1 대화 중. 1인칭으로 상대를 바라보며 회전·줌·시점 전환을 받지 않는다.</summary>
        Dialogue,
    }
}
