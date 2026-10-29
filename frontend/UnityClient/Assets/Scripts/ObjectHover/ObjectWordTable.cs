using System.Collections.Generic;

namespace A305.ObjectHover
{
    /// <summary>
    /// 사물 키(ERD world_objects.unity_key)로 영어 표현을 찾는다. 키는 서버 값과 같아야 하므로 대소문자를 구분하고,
    /// 같은 키가 두 번 있으면 먼저 온 표현을 쓴다.
    /// </summary>
    public sealed class ObjectWordTable
    {
        private readonly Dictionary<string, ObjectWord> wordsByKey = new Dictionary<
            string,
            ObjectWord
        >(System.StringComparer.Ordinal);

        public ObjectWordTable(IEnumerable<(string UnityKey, ObjectWord Word)> entries)
        {
            foreach ((string unityKey, ObjectWord word) in entries)
            {
                if (!string.IsNullOrEmpty(unityKey) && !wordsByKey.ContainsKey(unityKey))
                {
                    wordsByKey.Add(unityKey, word);
                }
            }
        }

        public bool TryGetWord(string unityKey, out ObjectWord word)
        {
            if (string.IsNullOrEmpty(unityKey))
            {
                word = default;
                return false;
            }

            return wordsByKey.TryGetValue(unityKey, out word);
        }
    }
}
