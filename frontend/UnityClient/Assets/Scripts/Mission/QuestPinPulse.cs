using UnityEngine;
using UnityEngine.UI;

namespace A305.Mission
{
    /// <summary>
    /// 도장 지도에서 지금 갈 곳의 핀 아래 빛을 천천히 퍼지며 흐려지게 되풀이해 갈 곳이 눈에 띄게 한다.
    /// 퀘스트 창을 여는 동안 게임 시간이 멈춰도 움직이도록 실제 시간을 쓴다.
    /// </summary>
    public sealed class QuestPinPulse : MonoBehaviour
    {
        private const float Period = 1.6f;
        private const float StartScale = .7f;
        private const float EndScale = 1.4f;

        [SerializeField]
        private Graphic glow;

        private void Update()
        {
            float t = Time.unscaledTime % Period / Period;
            transform.localScale = Vector3.one * Mathf.Lerp(StartScale, EndScale, t);
            Color color = glow.color;
            color.a = 1 - t;
            glow.color = color;
        }
    }
}
