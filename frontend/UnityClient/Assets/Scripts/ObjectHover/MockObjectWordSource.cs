using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace A305.ObjectHover
{
    /// <summary>
    /// 서버 연동 전에 쓰는 확인용 단어 목록. 서버 API가 준비되면 서버에서 받는 단어로 바꾸고 이 에셋은 개발 확인에만 쓴다.
    /// </summary>
    [CreateAssetMenu(menuName = "A305/ObjectHover/Mock Object Words", fileName = "MockObjectWords")]
    public class MockObjectWordSource : ObjectWordSource
    {
        [SerializeField]
        private List<Entry> entries = new List<Entry>();

        private ObjectWordTable table;

        public override bool TryGetWord(string unityKey, out ObjectWord word)
        {
            table ??= new ObjectWordTable(
                entries.Select(entry =>
                    (
                        entry.unityKey,
                        ObjectWord.ForHover(
                            entry.text,
                            entry.meaningKo,
                            entry.example,
                            entry.isSaved
                        )
                    )
                )
            );
            return table.TryGetWord(unityKey, out word);
        }

        // Inspector에서 목록을 고치면 다음 조회 때 다시 만든다.
        private void OnValidate()
        {
            table = null;
        }

        [Serializable]
        private class Entry
        {
            [Tooltip("ERD world_objects.unity_key")]
            public string unityKey;

            [Tooltip("ERD vocabularies.text")]
            public string text;

            [Tooltip("ERD vocabularies.meaning_ko")]
            public string meaningKo;

            [Tooltip("ERD vocabularies.example. 단어장에 저장한 단어만 호버에 보인다.")]
            public string example;

            [Tooltip("사용자가 단어장에 저장했는지(ERD user_vocab). 서버 연동 전 확인용 값이다.")]
            public bool isSaved;
        }
    }
}
