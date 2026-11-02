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

        /// <summary>캐노피 아랫면(살 끝)의 높이(손잡이 기준, m).</summary>
        public float ShaftLength { get; private set; }

        public Transform Canopy => canopy;

        public void Fit(float shaftLength)
        {
            ShaftLength = Mathf.Max(.1f, shaftLength);
            float toTip = ShaftLength + canopyHeight;
            shaft.localPosition = Vector3.up * (toTip / 2f);
            Vector3 scale = shaft.localScale;
            shaft.localScale = new Vector3(scale.x, toTip / 2f, scale.z);
            canopy.localPosition = Vector3.up * ShaftLength;
        }
    }
}
