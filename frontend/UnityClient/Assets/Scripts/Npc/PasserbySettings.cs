using UnityEngine;

namespace A305.Npc
{
    /// <summary>행인이 다가오고 제안하고 쉬는 조건과 걷는 속도.</summary>
    [CreateAssetMenu(menuName = "A305/Npc/Passerby Settings", fileName = "PasserbySettings")]
    public class PasserbySettings : ScriptableObject
    {
        [SerializeField]
        [Min(0f)]
        [Tooltip("플레이어가 이 거리 안에 들어오면 다가가기 시작한다.")]
        private float detectRadiusMeters = 6f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("플레이어와 이 거리까지 가까워지면 멈춰서 대화를 제안한다.")]
        private float offerDistanceMeters = 2f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("제안 중 플레이어가 이 거리보다 멀어지면 무시한 것으로 보고 제안을 닫는다.")]
        private float dismissDistanceMeters = 6f;

        [SerializeField]
        [Min(0f)]
        [Tooltip(
            "거절·무시·대화가 끝난 뒤 다시 다가오기까지 쉬는 시간(실제 시간). 다른 공간에 있어도 흐른다."
        )]
        private float restSeconds = 60f;

        [SerializeField]
        [Tooltip("제안을 거절할 수 있는지. 튜토리얼 행인은 거절할 수 없다.")]
        private bool canDecline = true;

        [SerializeField]
        [Tooltip(
            "하루(기기 날짜)에 한 번만 먼저 다가온다. 그날 이미 말을 걸었으면 플레이어가 먼저 말을 거는 것만 받는다."
        )]
        private bool approachesOncePerDay;

        [SerializeField]
        [Min(0f)]
        [Tooltip(
            "대화에서 준비해 주기로 한 것(음료·빨래 등)이 다 되면 플레이어가 이 거리 안에 있을 때 자리를 떠나지 않고 부른다."
        )]
        private float followUpCallDistanceMeters = 6f;

        [SerializeField]
        [Min(0f)]
        private float walkSpeedMetersPerSecond = 1.6f;

        [SerializeField]
        [Min(0f)]
        private float turnSpeedDegreesPerSecond = 540f;

        public bool ApproachesOncePerDay => approachesOncePerDay;

        public float WalkSpeedMetersPerSecond => walkSpeedMetersPerSecond;

        public float TurnSpeedDegreesPerSecond => turnSpeedDegreesPerSecond;

        public PasserbyRules ToRules()
        {
            return new PasserbyRules(
                detectRadiusMeters,
                offerDistanceMeters,
                dismissDistanceMeters,
                restSeconds,
                canDecline,
                followUpCallDistanceMeters
            );
        }
    }
}
