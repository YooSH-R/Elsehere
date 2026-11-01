using System;
using System.Text;

namespace A305.Dialogue
{
    /// <summary>
    /// 튜토리얼처럼 정해진 대사를 따라 말하는 대화에서 입력이 그 대사와 같은지 본다.
    /// 대소문자·띄어쓰기·문장부호(아포스트로피 포함)는 보지 않고, 글자 8개마다 한 글자 정도의 오타는 같은 대사로 본다.
    /// </summary>
    public static class GuidedLineMatcher
    {
        private const int LettersPerAllowedTypo = 8;

        public static bool IsSameLine(string said, string line)
        {
            string saidText = Normalize(said);
            string lineText = Normalize(line);
            if (saidText.Length == 0 || lineText.Length == 0)
            {
                return false;
            }

            int allowedTypos = Math.Max(1, lineText.Length / LettersPerAllowedTypo);
            return EditDistance(saidText, lineText) <= allowedTypos;
        }

        // 글자·숫자만 남기고 소문자로 바꾸며, 띄어쓰기는 한 칸으로 줄인다.
        private static string Normalize(string text)
        {
            var normalized = new StringBuilder();
            foreach (char letter in text ?? "")
            {
                if (char.IsLetterOrDigit(letter))
                {
                    normalized.Append(char.ToLowerInvariant(letter));
                }
                else if (
                    char.IsWhiteSpace(letter)
                    && normalized.Length > 0
                    && normalized[^1] != ' '
                )
                {
                    normalized.Append(' ');
                }
            }

            return normalized.ToString().TrimEnd();
        }

        // 한 글자를 넣거나 빼거나 바꾸는 횟수(레벤슈타인 거리).
        private static int EditDistance(string from, string to)
        {
            int[] previous = new int[to.Length + 1];
            int[] current = new int[to.Length + 1];
            for (int j = 0; j <= to.Length; j++)
            {
                previous[j] = j;
            }

            for (int i = 1; i <= from.Length; i++)
            {
                current[0] = i;
                for (int j = 1; j <= to.Length; j++)
                {
                    int replace = previous[j - 1] + (from[i - 1] == to[j - 1] ? 0 : 1);
                    current[j] = Math.Min(replace, Math.Min(previous[j], current[j - 1]) + 1);
                }

                (previous, current) = (current, previous);
            }

            return previous[to.Length];
        }
    }
}
