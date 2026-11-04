using System.Collections.Generic;

namespace A305.Learning
{
    /// <summary>피드백 화면 아래 쪽수. 카드마다 글이 여러 쪽일 수 있어 모든 카드의 쪽을 이어 세어 하나의 번호로 보인다.</summary>
    public static class FeedbackPageNumber
    {
        /// <param name="pagesPerCard">카드마다의 글 쪽수(1 이상)</param>
        /// <param name="card">지금 카드(0부터)</param>
        /// <param name="textPage">지금 카드 안의 글 쪽(1부터)</param>
        public static string Format(IReadOnlyList<int> pagesPerCard, int card, int textPage)
        {
            int before = 0;
            int total = 0;
            for (int i = 0; i < pagesPerCard.Count; i++)
            {
                if (i < card)
                    before += pagesPerCard[i];
                total += pagesPerCard[i];
            }

            return $"{before + textPage} / {total}";
        }
    }
}
