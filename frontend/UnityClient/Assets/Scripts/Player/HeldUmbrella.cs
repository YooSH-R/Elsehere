using UnityEngine;

namespace A305.Player
{
    /// <summary>
    /// 플레이어가 펴 드는 우산 모델. 원점이 손잡이를 쥐는 자리이고 우산대는 위(+Y)로 선다.
    /// 캐릭터 머리 높이에 맞춰 우산대 길이를 정하고 캐노피를 그 끝에 둔다.
    /// </summary>
    public sealed class HeldUmbrella : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("높이 2인 원기둥. 길이에 맞춰 늘린다")]
        private Transform shaft;

        [SerializeField]
        [Tooltip("우산대 끝에 두는 캐노피 묶음. 아랫면(살 끝)이 원점에 오게 만든다")]
        private Transform canopy;

        [SerializeField]
        [Tooltip("캐노피 아랫면에서 꼭지까지 높이(m). 우산대를 꼭지까지 잇는다")]
        private float canopyHeight = .3f;

        public Transform Canopy => canopy;
        private Transform handleStem;

        /// <summary>손잡이 위의 꺾인 연결부가 머리를 피하고, 주 우산대와 캐노피는 수직·수평을 유지한다.</summary>
        public void Fit(Vector3 bend, Vector3 roof, Quaternion roofRotation, float radius)
        {
            if (handleStem == null)
            {
                handleStem = Instantiate(shaft, transform);
                handleStem.name = "HandleStem";
            }
            PlaceShaft(handleStem, transform.position, bend);
            PlaceShaft(shaft, bend, roof + roofRotation * Vector3.up * canopyHeight);
            canopy.SetPositionAndRotation(roof, roofRotation);
            canopy.localScale = new Vector3(radius / .84f, 1, radius / .84f);
        }

        private static void PlaceShaft(Transform segment, Vector3 start, Vector3 end)
        {
            Vector3 direction = end - start;
            segment.SetPositionAndRotation(
                (start + end) * .5f,
                Quaternion.FromToRotation(Vector3.up, direction)
            );
            Vector3 scale = segment.localScale;
            segment.localScale = new Vector3(scale.x, direction.magnitude * .5f, scale.z);
        }
    }
}
