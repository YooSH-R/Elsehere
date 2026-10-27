using System;
using System.Collections.Generic;
using UnityEngine;

namespace A305.ObjectHover
{
    /// <summary>가구 로컬 좌표의 세부 영역과 공용 단어. 여러 프리팹 배치가 함께 사용한다.</summary>
    [CreateAssetMenu(menuName = "A305/Object Hover/Parts", fileName = "ObjectWordParts")]
    public sealed class ObjectWordPartsDefinition : ScriptableObject
    {
        [Serializable]
        public sealed class Part
        {
            [SerializeField]
            private string label;

            [SerializeField]
            private ObjectWordDefinition word;

            [SerializeField]
            private Bounds[] regions = Array.Empty<Bounds>();
            public string Label => label;
            public ObjectWordDefinition Word => word;
            public IReadOnlyList<Bounds> Regions => regions;
        }

        [SerializeField]
        private Part[] parts = Array.Empty<Part>();
        public IReadOnlyList<Part> Parts => parts;

        public int FindPart(Vector3 localPoint, float distanceMeters, float detailDistanceMeters)
        {
            if (distanceMeters > detailDistanceMeters)
                return -1;
            for (int index = 0; index < parts.Length; index++)
            {
                Part part = parts[index];
                if (part.Word == null)
                    continue;
                foreach (Bounds region in part.Regions)
                    if (region.Contains(localPoint))
                        return index;
            }
            return -1;
        }
    }
}
