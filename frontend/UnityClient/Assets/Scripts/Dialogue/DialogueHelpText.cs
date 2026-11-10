using System.Collections.Generic;
using System.Linq;

namespace A305.Dialogue
{
    public static class DialogueHelpText
    {
        /// <param name="isSuggesting">추천 단어를 서버에서 받고 있는지</param>
        public static string Words(IReadOnlyList<string> words, bool isSuggesting = false)
        {
            string[] entries = words
                ?.Where(word => !string.IsNullOrWhiteSpace(word))
                .Select(FormatWord)
                .ToArray();
            if (entries != null && entries.Length > 0)
                return string.Join("\n\n", entries);

            return isSuggesting
                ? "추천 단어를 고르고 있어요..."
                : "추천 단어가 아직 없어요. 대화를 이어가 보세요.";
        }

        private static string FormatWord(string value)
        {
            int separator = value.IndexOf(" — ", System.StringComparison.Ordinal);
            string word = separator < 0 ? value.Trim() : value.Substring(0, separator).Trim();
            string meaning = separator < 0 ? "" : value.Substring(separator + 3).Trim();
            return Sentence(word, meaning);
        }

        public static string Sentence(string english, string meaningKo) =>
            "<b>"
            + Escape(english ?? "")
            + "</b>"
            + (
                string.IsNullOrWhiteSpace(meaningKo)
                    ? ""
                    : "\n<size=80%>" + Escape(meaningKo) + "</size>"
            );

        private static string Escape(string value) => value.Replace("<", "‹").Replace(">", "›");
    }
}
