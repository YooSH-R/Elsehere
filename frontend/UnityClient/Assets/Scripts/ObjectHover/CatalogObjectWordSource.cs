using System;
using System.Collections.Generic;
using UnityEngine;

namespace A305.ObjectHover
{
    /// <summary>Unity 공용 에셋으로 씬을 미리 본다. 사용자별 상태는 서버 연동 시 받는다.</summary>
    public sealed class CatalogObjectWordSource : ObjectWordSource
    {
        [SerializeField]
        private ObjectWordDefinition[] words = Array.Empty<ObjectWordDefinition>();
        public IReadOnlyList<ObjectWordDefinition> Words => words;

        public bool TryGetDetails(string wordKey, out ObjectWordDetails details)
        {
            foreach (ObjectWordDefinition definition in words)
                if (definition != null && definition.Key == wordKey)
                {
                    details = definition.Details;
                    return true;
                }
            details = null;
            return false;
        }

        public override bool TryGetWord(string unityKey, out ObjectWord word)
        {
            if (!TryGetDetails(unityKey, out ObjectWordDetails details))
            {
                word = default;
                return false;
            }
            word = Preview(details);
            return true;
        }

        public override bool TryGetWord(ObjectWordDefinition definition, out ObjectWord word)
        {
            if (definition == null)
            {
                word = default;
                return false;
            }
            word = Preview(definition.Details);
            return true;
        }

        private static ObjectWord Preview(ObjectWordDetails details)
        {
            // 기존 검수 조건을 유지한다. 저장 여부는 공용 콘텐츠에 저장하지 않는다.
            return ObjectWord.ForHover(
                details.Text,
                details.MeaningKo,
                details.Example,
                details.Key == "tv"
            );
        }
    }
}
