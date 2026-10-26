using UnityEngine;

namespace A305.ObjectHover
{
    /// <summary>서버 원본에서 생성한 공용 단어 미리보기. 서버 API 연결 전 씬 검수에 사용한다.</summary>
    public sealed class CatalogObjectWordSource : ObjectWordSource
    {
        [SerializeField]
        private TextAsset catalogJson;
        private ObjectWordCatalog catalog;

        public bool TryGetDetails(string wordKey, out ObjectWordDetails details)
        {
            catalog ??= ObjectWordCatalog.Parse(catalogJson.text);
            return catalog.TryGetDetails(wordKey, out details);
        }

        public override bool TryGetWord(string unityKey, out ObjectWord word)
        {
            if (!TryGetDetails(unityKey, out ObjectWordDetails details))
            {
                word = default;
                return false;
            }
            // 기존 Sandbox와 동일하게 TV만 저장된 상태로 미리 본다. 서버 연동 시 사용자 상태를 받는다.
            word = ObjectWord.ForHover(
                details.Text,
                details.MeaningKo,
                details.Example,
                unityKey == "tv"
            );
            return true;
        }

        private void OnValidate()
        {
            catalog = null;
        }
    }
}
