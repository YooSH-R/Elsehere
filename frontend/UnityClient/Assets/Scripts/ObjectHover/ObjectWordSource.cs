using UnityEngine;

namespace A305.ObjectHover
{
    /// <summary>
    /// 사물 키로 영어 표현을 찾는 곳. 실제 단어는 서버 마스터 데이터(ERD world_objects·vocabularies)에서 받으며,
    /// 서버 연동 전에는 <see cref="MockObjectWordSource"/>를 쓴다.
    /// </summary>
    public abstract class ObjectWordSource : ScriptableObject
    {
        public abstract bool TryGetWord(string unityKey, out ObjectWord word);
    }
}
