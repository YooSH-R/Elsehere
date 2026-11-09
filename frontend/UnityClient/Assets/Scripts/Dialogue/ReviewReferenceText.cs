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
        public string Text { get; private set; } = "";
        public IReadOnlyList<ReviewReferenceMarker> Markers { get; private set; } =
            new List<ReviewReferenceMarker>();

        public static ReviewReferenceText Format(
            IReadOnlyList<ReviewReferenceItem> items,
            int verifiedCount,
            string scenario = null
        )
        {
            items = items ?? Array.Empty<ReviewReferenceItem>();
            var text = new StringBuilder();
            var markers = new List<ReviewReferenceMarker>();
            if (!string.IsNullOrEmpty(scenario))
                text.Append(scenario).AppendLine();
            text.Append("<color=#C9794A><b>")
                .Append(Math.Min(items.Count, Math.Max(0, verifiedCount)))
                .Append(" / ")
                .Append(items.Count)
                .Append("</b>  말해봤어요</color>");
            foreach (ReviewReferenceItem item in items)
            {
                text.Append("\n\n<indent=38><b>");
                markers.Add(new ReviewReferenceMarker(text.Length, item.Used));
                text.Append(Escape(item.Expression)).Append("</b>");
                if (!string.IsNullOrWhiteSpace(item.MeaningKo))
                    text.Append("\n<size=85%>").Append(Escape(item.MeaningKo)).Append("</size>");
                text.Append("</indent>");
            }
            return new ReviewReferenceText { Text = text.ToString(), Markers = markers.ToArray() };
        }

        private static string Escape(string value) =>
            (value ?? "").Replace("<", "‹").Replace(">", "›");
    }

    public sealed class ReviewReferenceItem
    {
        public ReviewReferenceItem(string expression, string meaningKo, bool used)
        {
            Expression = expression;
            MeaningKo = meaningKo;
            Used = used;
        }

        public string Expression { get; }
        public string MeaningKo { get; }
        public bool Used { get; }
    }
}
