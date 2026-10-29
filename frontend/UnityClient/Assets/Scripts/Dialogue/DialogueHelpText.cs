using System.Collections.Generic;

namespace A305.Dialogue
{
    public static class DialogueHelpText
    {
        public static string Words(IReadOnlyList<string> words) =>
            words == null || words.Count == 0
                ? "추천 단어가 아직 없어요. 대화를 이어가 보세요."
                : "• " + string.Join("\n• ", words);

        public static string Sentence(string hint, string example) =>
            string.IsNullOrWhiteSpace(hint) ? example : hint;
    }
}
