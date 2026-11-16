using System;
using System.Collections.Generic;
using System.Text;

namespace A305.Dialogue
{
    public readonly struct ReviewReferenceMarker
    {
        public ReviewReferenceMarker(int sourceIndex, bool used)
        {
            SourceIndex = sourceIndex;
            Used = used;
        }

        public int SourceIndex { get; }
        public bool Used { get; }
    }

    public sealed class ReviewReferenceText
    {
        private const string CorrectionHeading = "다시 말해 볼 문장";
        private const string ExpressionHeading = "이 상황에서 자주 써요";

        public string Text { get; private set; } = "";
        public IReadOnlyList<ReviewReferenceMarker> Markers { get; private set; } =
            new List<ReviewReferenceMarker>();

        /// <param name="listFirst">복습처럼 문장 목록이 먼저 보여야 하면 상황 설명을 목록 아래에 둔다.</param>
        public static ReviewReferenceText Format(
            IReadOnlyList<ReviewReferenceItem> items,
            int verifiedCount,
            string scenario = null,
            string countLabel = "말해봤어요",
            bool listFirst = false
        )
        {
            items = items ?? Array.Empty<ReviewReferenceItem>();
            var text = new StringBuilder();
            var markers = new List<ReviewReferenceMarker>();
            bool hasScenario = !string.IsNullOrEmpty(scenario);
            if (hasScenario && !listFirst)
                text.Append(scenario).AppendLine();
            text.Append("<color=#C9794A><b>")
                .Append(Math.Min(items.Count, Math.Max(0, verifiedCount)))
                .Append(" / ")
                .Append(items.Count)
                .Append("</b>  ")
                .Append(countLabel)
                .Append("</color>");
            // 고친 문장이 있을 때만 구역을 나눈다. 서버가 고친 문장을 앞에 두므로 순서는 바꾸지 않는다.
            bool hasCorrections = false;
            foreach (ReviewReferenceItem item in items)
                hasCorrections |= item.IsCorrection;
            bool? headed = null;
            foreach (ReviewReferenceItem item in items)
            {
                if (hasCorrections && headed != item.IsCorrection)
                {
                    headed = item.IsCorrection;
                    text.Append("\n\n<size=85%><b>")
                        .Append(item.IsCorrection ? CorrectionHeading : ExpressionHeading)
                        .Append("</b></size>");
                }
                // 말해 볼 문장은 그 아래 설명(85%)보다 크게 보여 한눈에 찾는다.
                text.Append("\n\n<indent=38><size=117%><b>");
                markers.Add(new ReviewReferenceMarker(text.Length, item.Used));
                text.Append(Escape(item.Expression)).Append("</b></size>");
                if (item.IsCorrection)
                    text.Append("\n<size=85%>내가 한 말 · ")
                        .Append(Said(item.OriginalText))
                        .Append("</size>");
                else if (!string.IsNullOrWhiteSpace(item.MeaningKo))
                    text.Append("\n<size=85%>").Append(Escape(item.MeaningKo)).Append("</size>");
                text.Append("</indent>");
            }
            if (hasScenario && listFirst)
                text.Append("\n\n").Append(scenario.TrimEnd());
            return new ReviewReferenceText { Text = text.ToString(), Markers = markers.ToArray() };
        }

        // 한국어로 한 말은 틀린 말이 아니므로 그대로 두고, 고친 영어 문장에만 줄을 긋는다.
        private static string Said(string original)
        {
            foreach (char letter in original)
                if (letter >= '가' && letter <= '힣')
                    return Escape(original);
            return "<s>" + Escape(original) + "</s>";
        }

        private static string Escape(string value) =>
            (value ?? "").Replace("<", "‹").Replace(">", "›");
    }

    public sealed class ReviewReferenceItem
    {
        /// <param name="originalText">고친 문장이면 학습자가 원래 한 말. 상황 표현·연습 목표는 null</param>
        public ReviewReferenceItem(
            string expression,
            string meaningKo,
            bool used,
            string originalText = null
        )
        {
            Expression = expression;
            MeaningKo = meaningKo;
            Used = used;
            OriginalText = originalText;
        }

        public string Expression { get; }
        public string MeaningKo { get; }
        public bool Used { get; }
        public string OriginalText { get; }
        public bool IsCorrection => !string.IsNullOrWhiteSpace(OriginalText);
    }
}
