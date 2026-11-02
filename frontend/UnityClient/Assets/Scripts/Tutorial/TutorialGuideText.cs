using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace A305.Tutorial
{
    /// <summary>
    /// 튜토리얼 안내 문구 꾸미기. 문구의 {키}·{버튼}을 강조색 [키] 표시로 바꾸고, " · "로 나뉜 할 일 중 해낸 것을 초록색으로 바꾸며,
    /// 진행 점을 그린다.
    /// 테마의 제목 글꼴에서는 TMP 강조 배경(mark)이 그려지지 않아 괄호와 글자색으로만 꾸민다.
    /// 기호는 서비스 글꼴(NanumSquareRound)에 있는 ●·○만 쓴다.
    /// </summary>
    public static class TutorialGuideText
    {
        private const string ChipOpen = "<color=#C0612B>[";
        private const string ChipClose = "]</color>";
        private const string PartSeparator = " · ";
        private const string DoneOpen = "<color=#3E9B5A>";
        private const string DoneClose = "</color>";
        private static readonly Regex Key = new Regex(@"\{([^{}]+)\}");
        private static readonly Regex Tag = new Regex(@"<[^>]+>");

        public static string KeyChips(string text)
        {
            return Key.Replace(text ?? "", match => ChipOpen + match.Groups[1].Value + ChipClose);
        }

        /// <summary>읽는 사람에게 보이는 글자 수. [키] 괄호는 세고 서식 태그는 세지 않는다.</summary>
        public static int VisibleLength(string text)
        {
            return Tag.Replace(Key.Replace(text ?? "", "[$1]"), "").Length;
        }

        /// <summary>할 일 한 줄. " · "로 나뉜 할 일마다 해낸 것은 초록색, 남은 것은 강조색 [키]로 보인다.</summary>
        /// <param name="partDone">할 일 순서대로 해냈는지. 모자라면 남은 할 일로 본다.</param>
        public static string TaskLine(string task, IReadOnlyList<bool> partDone)
        {
            string[] parts = SplitParts(task);
            for (int i = 0; i < parts.Length; i++)
            {
                bool isDone = partDone != null && i < partDone.Count && partDone[i];
                parts[i] = isDone
                    ? DoneOpen + Key.Replace(parts[i], "[$1]") + DoneClose
                    : KeyChips(parts[i]);
            }

            return string.Join(PartSeparator, parts);
        }

        /// <summary>" · "로 나뉜 할 일 수</summary>
        public static int PartCount(string task) => SplitParts(task).Length;

        private static string[] SplitParts(string task)
        {
            return (task ?? "").Split(new[] { PartSeparator }, StringSplitOptions.None);
        }

        /// <param name="done">지금 단계까지 채울 점 수</param>
        public static string ProgressDots(int done, int total)
        {
            var dots = new StringBuilder();
            for (int i = 0; i < total; i++)
            {
                if (i > 0)
                {
                    dots.Append(' ');
                }

                dots.Append(i < done ? '●' : '○');
            }

            return dots.ToString();
        }
    }
}
