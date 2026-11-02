using System.Collections.Generic;

namespace A305.Dialogue
{
    public static class DialogueHelpText
    {
        /// <param name="isSuggesting">추천 단어를 서버에서 받고 있는지</param>
        public static string Words(IReadOnlyList<string> words, bool isSuggesting = false)
        {
            if (words != null && words.Count > 0)
            {
                return "• " + string.Join("\n• ", words);
            }

            return isSuggesting
                ? "추천 단어를 고르고 있어요..."
                : "추천 단어가 아직 없어요. 대화를 이어가 보세요.";
        }

        public static string Sentence(string hint, string example) =>
            string.IsNullOrWhiteSpace(hint) ? example : hint;
    }
}
