using UnityEngine;

namespace A305.ObjectHover
{
    /// <summary>
    /// 공용 에셋 또는 이전 사물 키에서 표시할 단어를 찾는다.
    /// 씬 검수는 CatalogObjectWordSource를 사용하며 사용자별 상태는 서버 연동 시 결합한다.
    /// </summary>
    public abstract class ObjectWordSource : ScriptableObject
    {
        public abstract bool TryGetWord(string unityKey, out ObjectWord word);

        public virtual bool TryGetWord(ObjectWordDefinition definition, out ObjectWord word)
        {
            return TryGetWord(definition.Key, out word);
        }
    }
}
