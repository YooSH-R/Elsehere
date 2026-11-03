using UnityEngine;

namespace A305.Dialogue
{
    /// <summary>
    /// 오른쪽 위 단어·문장 카드 묶음을 대화창 이름 줄 위 남는 높이에 다 들어가게 줄이는 배율.
    /// 아래가 잘려 보이지 않게 하되, 읽기 어렵지 않게 0.7배까지만 줄이고 그보다 길면 칸 안에서 스크롤한다.
    /// </summary>
    public static class DialogueHelpFit
    {
        public const float MinimumScale = .7f;

        /// <param name="room">카드 묶음을 둘 수 있는 높이</param>
        /// <param name="needed">카드 묶음을 다 보이는 데 필요한 높이</param>
        public static float Scale(float room, float needed) =>
            needed <= room ? 1f : Mathf.Max(MinimumScale, room / needed);
    }
}
