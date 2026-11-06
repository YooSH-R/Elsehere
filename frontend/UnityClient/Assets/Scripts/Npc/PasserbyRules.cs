namespace A305.Npc
{
    /// <summary>행인이 다가오고 제안하고 쉬는 조건.</summary>
    public readonly struct PasserbyRules
    {
        public PasserbyRules(
            float detectRadiusMeters,
            float offerDistanceMeters,
            float dismissDistanceMeters,
            float restSeconds,
            bool canDecline,
            float followUpCallDistanceMeters = 0f
        )
        {
            DetectRadiusMeters = detectRadiusMeters;
            OfferDistanceMeters = offerDistanceMeters;
            DismissDistanceMeters = dismissDistanceMeters;
            RestSeconds = restSeconds;
            CanDecline = canDecline;
            FollowUpCallDistanceMeters = followUpCallDistanceMeters;
        }

        /// <summary>플레이어가 이 거리 안에 들어오면 다가가기 시작한다.</summary>
        public float DetectRadiusMeters { get; }

        /// <summary>플레이어와 이 거리까지 가까워지면 멈춰서 대화를 제안한다.</summary>
        public float OfferDistanceMeters { get; }

        /// <summary>제안 중 플레이어가 이 거리보다 멀어지면 무시한 것으로 보고 제안을 닫는다.</summary>
        public float DismissDistanceMeters { get; }

        /// <summary>거절·무시·대화가 끝난 뒤 다시 다가오기까지 쉬는 시간.</summary>
        public float RestSeconds { get; }

        /// <summary>제안을 거절할 수 있는지. 튜토리얼 행인은 거절할 수 없다.</summary>
        public bool CanDecline { get; }

        /// <summary>준비해 둔 것이 다 되면 플레이어가 이 거리 안에 있을 때 제자리에서 부른다. 0이면 부르지 않는다.</summary>
        public float FollowUpCallDistanceMeters { get; }
    }
}
