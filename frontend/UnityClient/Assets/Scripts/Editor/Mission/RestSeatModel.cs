using UnityEngine;

namespace A305.EditorTools.Mission
{
    /// <summary>
    /// 앉을 가구 모델 하나에 두는 앉기 자리. 값은 가구 원점 아래 바닥에서 잰 크기 1 기준이고, 앞(+z)은 가구의 앞이다.
    /// </summary>
    public sealed class RestSeatModel
    {
        public RestSeatModel(
            string meshPath,
            float cushionTopMeters,
            float frontEdgeMeters,
            float[] seatOffsetsMeters,
            float yawDegrees = 0f,
            bool backless = false
        )
        {
            MeshPath = meshPath;
            CushionTopMeters = cushionTopMeters;
            FrontEdgeMeters = frontEdgeMeters;
            SeatOffsetsMeters = seatOffsetsMeters;
            YawDegrees = yawDegrees;
            Backless = backless;
        }

        /// <summary>가구를 알아보는 메시. 프리팹 중첩이나 풀기와 상관없이 같은 모델이면 같은 메시를 그린다.</summary>
        public string MeshPath { get; }

        public float CushionTopMeters { get; }
        public float FrontEdgeMeters { get; }

        /// <summary>
        /// 가구 폭 방향(x)으로 잰 자리 위치. 벤치·소파는 여러 자리를 둔다.
        /// 자리 표시를 가구 자식의 로컬 위치로 두므로 크기를 바꿔 놓은 가구에서는 함께 늘고 준다.
        /// </summary>
        public float[] SeatOffsetsMeters { get; }

        /// <summary>앉아서 보는 방향. 가구 앞(+z)에서 잰 각도</summary>
        public float YawDegrees { get; }

        /// <summary>등받이가 없어 놓인 곳의 탁자·카운터 쪽을 보는 자리인지</summary>
        public bool Backless { get; }

        /// <summary>
        /// 크기를 바꿔 놓은 가구에 맞춘 앉는 면. <see cref="A305.Mission.RestSeat"/>는 쿠션 높이와 앞 모서리를 미터로 받으므로 가구 배율을 곱한다.
        /// </summary>
        public RestSeatModel ScaledBy(Vector3 furnitureScale)
        {
            // 앞 모서리는 앉아서 보는 방향으로 잰 거리라, 옆을 보는 자리는 가구 폭(x) 배율을 따른다.
            Vector3 front = Quaternion.Euler(0f, YawDegrees, 0f) * Vector3.forward;
            float frontScale =
                Mathf.Abs(front.x) * furnitureScale.x + Mathf.Abs(front.z) * furnitureScale.z;
            return new RestSeatModel(
                MeshPath,
                CushionTopMeters * furnitureScale.y,
                FrontEdgeMeters * frontScale,
                SeatOffsetsMeters,
                YawDegrees,
                Backless
            );
        }
    }
}
