using System.Collections.Generic;
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
        public IReadOnlyList<Vector3> ShaftPoints => points;
        private readonly Vector3[] points = new Vector3[34];
        private Mesh curvedMesh;
        private const int Sides = 12;
        private readonly Vector3[] vertices = new Vector3[34 * Sides];
        private readonly Vector3[] normals = new Vector3[34 * Sides];

        /// <summary>접은 우산의 손잡이부터 끝까지 가장 긴 길이(m)</summary>
        public const float FoldedLength = .68f;

        /// <summary>
        /// 접은 천과 곧은 우산대를 원점(열린 우산과 같은 손잡이)에서 위(+Y) 방향으로 length만큼 잇는다.
        /// 짧게 들 때는 접은 천도 같은 비율로 줄인다.
        /// </summary>
        public void FitFolded(float length = FoldedLength)
        {
            for (int i = 0; i < points.Length; i++)
                points[i] = transform.position + transform.up * (length * i / (points.Length - 1));
            UpdateTube();
            float share = length / FoldedLength;
            canopy.SetPositionAndRotation(
                transform.position + transform.up * (.12f * share),
                transform.rotation
            );
            canopy.localScale = new Vector3(.07f, 1.7f * share, .07f);
        }

        /// <summary>손잡이에서 귀 옆까지 매끄러운 곡선으로 잇고 캐노피는 수평으로 둔다.</summary>
        public void Fit(Vector3 side, Vector3 roof, Quaternion roofRotation, float radius)
        {
            Vector3 up = roofRotation * Vector3.up;
            Vector3 start = transform.position;
            float length = Vector3.Distance(start, side);
            Vector3 first = start + transform.up * length * .45f;
            Vector3 second = side - up * length * .35f;
            for (int i = 0; i <= 24; i++)
            {
                float t = i / 24f,
                    u = 1 - t;
                points[i] =
                    u * u * u * start
                    + 3 * u * u * t * first
                    + 3 * u * t * t * second
                    + t * t * t * side;
            }
            Vector3 tip = roof + up * canopyHeight;
            for (int i = 25; i < points.Length; i++)
                points[i] = Vector3.Lerp(side, tip, (i - 24) / 9f);
            UpdateTube();
            canopy.SetPositionAndRotation(roof, roofRotation);
            canopy.localScale = new Vector3(radius / .84f, 1, radius / .84f);
        }

        private void UpdateTube()
        {
            if (curvedMesh == null)
            {
                curvedMesh = new Mesh { name = "HeldUmbrellaCurvedShaft" };
                curvedMesh.MarkDynamic();
                var tube = new GameObject("CurvedShaft", typeof(MeshFilter), typeof(MeshRenderer));
                tube.transform.SetParent(transform, false);
                tube.GetComponent<MeshFilter>().sharedMesh = curvedMesh;
                tube.GetComponent<MeshRenderer>().sharedMaterials = shaft
                    .GetComponent<Renderer>()
                    .sharedMaterials;
                shaft.gameObject.SetActive(false);
                var triangles = new int[(points.Length - 1) * Sides * 6];
                int cursor = 0;
                for (int ring = 0; ring < points.Length - 1; ring++)
                for (int side = 0; side < Sides; side++)
                {
                    int a = ring * Sides + side,
                        b = ring * Sides + (side + 1) % Sides,
                        c = a + Sides,
                        d = b + Sides;
                    triangles[cursor++] = a;
                    triangles[cursor++] = b;
                    triangles[cursor++] = c;
                    triangles[cursor++] = b;
                    triangles[cursor++] = d;
                    triangles[cursor++] = c;
                }
                curvedMesh.vertices = vertices;
                curvedMesh.triangles = triangles;
            }
            for (int ring = 0; ring < points.Length; ring++)
            {
                Vector3 tangent = (
                    points[Mathf.Min(ring + 1, points.Length - 1)] - points[Mathf.Max(0, ring - 1)]
                ).normalized;
                Vector3 across = Vector3.Cross(tangent, transform.forward).normalized;
                Vector3 around = Vector3.Cross(tangent, across).normalized;
                for (int side = 0; side < Sides; side++)
                {
                    float angle = side * Mathf.PI * 2 / Sides;
                    Vector3 normal = across * Mathf.Cos(angle) + around * Mathf.Sin(angle);
                    int index = ring * Sides + side;
                    vertices[index] = transform.InverseTransformPoint(
                        points[ring] + normal * .014f
                    );
                    normals[index] = transform.InverseTransformDirection(normal);
                }
            }
            curvedMesh.vertices = vertices;
            curvedMesh.normals = normals;
            curvedMesh.RecalculateBounds();
        }

        private void OnDestroy()
        {
            if (curvedMesh == null)
                return;
            if (Application.isPlaying)
                Destroy(curvedMesh);
            else
                DestroyImmediate(curvedMesh);
        }
    }
}
