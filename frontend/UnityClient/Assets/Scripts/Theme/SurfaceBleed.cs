using UnityEngine;
using UnityEngine.UI;

namespace A305.Theme
{
    /// <summary>
    /// 유리·입체 버튼 그림은 그림자를 그릴 바깥 여백을 가진다. 그 여백만큼 그리는 면을 바깥으로 넓혀 보이는 모양이 칸 경계에 맞고
    /// 그림자는 칸 밖으로 번지게 한다. 9칸 나눔의 테두리 폭은 그대로 두고 가운데 기준 바깥쪽으로만 민다. 누르는 영역은 그대로다.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public sealed class SurfaceBleed : BaseMeshEffect
    {
        [SerializeField]
        private Vector2 margin;

        public Vector2 Margin
        {
            get => margin;
            set
            {
                if (margin == value)
                    return;
                margin = value;
                if (graphic != null)
                    graphic.SetVerticesDirty();
            }
        }

        public override void ModifyMesh(VertexHelper mesh)
        {
            if (margin == Vector2.zero)
                return;
            Vector2 center = ((RectTransform)transform).rect.center;
            UIVertex vertex = default;
            for (int i = 0; i < mesh.currentVertCount; i++)
            {
                mesh.PopulateUIVertex(ref vertex, i);
                Vector3 position = vertex.position;
                position.x += position.x < center.x ? -margin.x : margin.x;
                position.y += position.y < center.y ? -margin.y : margin.y;
                vertex.position = position;
                mesh.SetUIVertex(vertex, i);
            }
        }
    }
}
