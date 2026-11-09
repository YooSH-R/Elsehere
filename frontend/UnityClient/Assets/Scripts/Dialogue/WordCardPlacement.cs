using UnityEngine;

namespace A305.Dialogue
{
    /// <summary>
    /// 대사에서 누른 단어의 호버 카드 자리. 단어 바로 위에 띄우고, 위에 자리가 없으면 아래에 띄운다.
    /// 카드는 대화창 안을 벗어나지 않아 대화창 위쪽의 NPC 얼굴을 가리지 않는다.
    /// </summary>
    public static class WordCardPlacement
    {
        // 카드와 단어 사이 간격
        public const float Gap = 8;

        /// <param name="word">누른 단어가 차지한 영역</param>
        /// <param name="card">카드 크기</param>
        /// <param name="area">카드가 들어갈 영역(대화창). 단어와 같은 좌표계다.</param>
        /// <returns>카드 왼쪽 아래 모서리 위치</returns>
        public static Vector2 Place(Rect word, Vector2 card, Rect area)
        {
            float x = Mathf.Clamp(word.center.x - card.x / 2, area.xMin, area.xMax - card.x);
            float above = word.yMax + Gap;
            if (above + card.y <= area.yMax)
            {
                return new Vector2(x, above);
            }

            float below = word.yMin - Gap - card.y;
            if (below >= area.yMin)
            {
                return new Vector2(x, below);
            }

            // 위·아래 어디에도 다 들어가지 않으면 단어를 조금 가리더라도 카드 위쪽을 대화창 안에 맞춘다.
            return new Vector2(x, Mathf.Max(area.yMin, area.yMax - card.y));
        }

        /// <summary>
        /// 카드 높이를 단어 위나 아래 중 넓은 쪽 남은 자리에 맞춰, 키운 카드가 단어를 가리지 않게 한다.
        /// </summary>
        /// <param name="wanted">자리가 있으면 쓸 높이</param>
        /// <param name="least">자리가 좁아도 줄이지 않을 높이</param>
        public static float FitHeight(Rect word, float wanted, float least, Rect area)
        {
            float room = Mathf.Max(area.yMax - (word.yMax + Gap), word.yMin - Gap - area.yMin);
            return Mathf.Max(least, Mathf.Min(wanted, room));
        }
    }
}
