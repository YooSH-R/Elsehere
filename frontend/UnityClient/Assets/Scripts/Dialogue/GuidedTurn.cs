using System;
using System.Collections.Generic;

namespace A305.Dialogue
{
    /// <summary>
    /// 정해진 흐름의 한 턴. 사용자가 읽어 볼 대사와 그 대사에 쓸 만한 추천 단어, 사용자가 말한 뒤 NPC가 이어서 하는 대사.
    /// </summary>
    public readonly struct GuidedTurn
    {
        public GuidedTurn(
            string suggestedEnglish,
            string suggestedKorean,
            DialogueLine reply,
            IReadOnlyList<string> suggestWords = null
        )
        {
            SuggestedEnglish = suggestedEnglish;
            SuggestedKorean = suggestedKorean;
            Reply = reply;
            SuggestWords = suggestWords ?? Array.Empty<string>();
        }

        public string SuggestedEnglish { get; }
        public string SuggestedKorean { get; }
        public DialogueLine Reply { get; }

        /// <summary>이 대사에 쓸 만한 추천 단어. 대화 화면 왼쪽 [단어]에 보인다.</summary>
        public IReadOnlyList<string> SuggestWords { get; }
    }
}
