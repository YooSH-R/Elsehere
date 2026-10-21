namespace A305.Npc
{
    /// <summary>먼저 말을 거는 행인의 상태.</summary>
    public enum PasserbyPhase
    {
        /// <summary>제자리에서 플레이어가 가까이 오기를 기다린다.</summary>
        Waiting,

        /// <summary>플레이어에게 걸어간다.</summary>
        Approaching,

        /// <summary>플레이어 앞에서 대화를 제안하고 답을 기다린다.</summary>
        Offering,

        /// <summary>플레이어와 대화 중이다.</summary>
        Talking,

        /// <summary>거절·무시·대화 뒤 다시 다가오기 전까지 쉰다.</summary>
        Resting,
    }
}
