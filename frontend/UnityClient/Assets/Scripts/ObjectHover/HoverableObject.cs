using UnityEngine;

namespace A305.ObjectHover
{
    /// <summary>
    /// 마우스를 올리면 영어 표현을 보여 줄 사물. 사물 루트에 붙이고, 자식을 포함해 콜라이더가 있어야 가리킬 수 있다.
    /// 모델을 바꿀 때도 이 컴포넌트와 공용 단어 참조는 유지한다.
    /// </summary>
    public class HoverableObject : MonoBehaviour
    {
        [SerializeField]
        private ObjectWordDefinition word;
        public ObjectWordDefinition Word => word;

        [SerializeField, Tooltip("Data 폴더에서 관리하는 세부 영역·단어 연결")]
        private ObjectWordPartsDefinition parts;
        public ObjectWordPartsDefinition Parts => parts;

        [SerializeField, HideInInspector]
        private string unityKey;

        public string UnityKey => word != null ? word.Key : unityKey;

        /// <summary>툴팁을 붙일 지점. 보이는 모양 전체를 감싸는 상자의 위쪽 가운데.</summary>
        public Vector3 GetTopCenter()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return transform.position;
            }

            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        }

        private void Awake()
        {
            if (string.IsNullOrEmpty(UnityKey))
            {
                Debug.LogError($"{name}의 HoverableObject에 사물 키가 없습니다.", this);
            }
        }
    }
}
