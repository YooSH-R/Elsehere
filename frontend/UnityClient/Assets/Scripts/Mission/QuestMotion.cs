using UnityEngine;

namespace A305.Mission
{
    /// <summary>퀘스트 완료 팡파레와 연계 퀘스트 잠금 해제가 함께 쓰는 움직임 곡선</summary>
    internal static class QuestMotion
    {
        /// <summary>0에서 1을 조금 넘었다가 1로 돌아오며 튀어나오는 곡선. 시작 전(0 이하)은 0, 끝난 뒤(1 이상)는 1이다.</summary>
        public static float Pop(float t)
        {
            t = Mathf.Clamp01(t);
            const float back = 1.70158f;
            float u = t - 1;
            return 1 + (back + 1) * u * u * u + back * u * u;
        }
    }
}
