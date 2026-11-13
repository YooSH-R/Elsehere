using System.Collections.Generic;
using UnityEngine;

namespace A305.Player
{
    /// <summary>손잡이부터 꼭지까지 곧은 우산. 전체 기울기는 들고 있는 자세에서 정한다.</summary>
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

        /// <summary>접은 우산의 손잡이부터 끝까지 가장 긴 길이(m)</summary>
        public const float FoldedLength = .68f;
        public Transform Canopy => canopy;
        public IReadOnlyList<Vector3> ShaftPoints => points;
        private readonly Vector3[] points = new Vector3[2];

        // 짧게 들 때는 접은 천도 같은 비율로 줄인다.
        public void FitFolded(float length = FoldedLength)
        {
            FitShaft(length);
            float share = length / FoldedLength;
            canopy.localPosition = Vector3.up * (.12f * share);
            canopy.localRotation = Quaternion.identity;
            canopy.localScale = new Vector3(.07f, 1.7f * share, .07f);
        }

        /// <summary>곧은 우산대 끝에 캐노피를 붙여 우산 전체가 같은 방향으로 기울어진다.</summary>
        public void Fit(float length, float radius)
        {
            FitShaft(length + canopyHeight);
            canopy.localPosition = Vector3.up * length;
            canopy.localRotation = Quaternion.identity;
            canopy.localScale = new Vector3(radius / .84f, 1, radius / .84f);
        }

        private void FitShaft(float length)
        {
            shaft.localPosition = Vector3.up * (length * .5f);
            shaft.localRotation = Quaternion.identity;
            shaft.localScale = new Vector3(.028f, length * .5f, .028f);
            points[0] = transform.position;
            points[1] = transform.TransformPoint(Vector3.up * length);
        }
    }
}
