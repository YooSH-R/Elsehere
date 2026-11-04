using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Mission
{
    /// <summary>
    /// 퀘스트 목록에서 연계 퀘스트의 잠금을 푸는 연출(시안 f2). 위에 "연계 퀘스트가 열렸습니다!" 띠가 내려오고, 다음 퀘스트 카드의 자물쇠가
    /// 흔들리다 깨지면서 흰 막이 조각나 흩어지고, 카드가 노랗게 반짝이며 [퀘스트 시작]이 나타난다.
    /// 카드 위에 겹쳐 두는 부품이라 목록을 다시 채워도 그대로 남는다. 부품과 [퀘스트 시작] 연결은 QuestBoardBuilder가 넣는다.
    /// </summary>
    public sealed class QuestUnlockEffect : MonoBehaviour
    {
        // 시안 f2 순서(초): 띠가 내려오는 시간, 자물쇠가 흔들리는 때와 깨지는 때, 조각이 흩어지는 시간, 열린 자물쇠·반짝이가 사라지는 때, 띠가 사라지는 때
        private const float ToastSeconds = .35f;
        private const float ToastDrop = 40f;
        private const float ShakeAt = .15f;
        private const float BreakAt = .75f;
        private const float ShardSeconds = .5f;
        private const float ShardReach = 1.5f;
        private const float VeilFadeSeconds = .3f;
        private const float GlowAt = .8f;
        private const float GlowSeconds = .3f;
        private const float ButtonAt = .95f;
        private const float ButtonSeconds = .3f;
        private const float OpenLockFadeAt = 1.9f;
        private const float SparkFadeAt = 1.6f;
        private const float FadeSeconds = .4f;
        private const float ToastFadeAt = 3.2f;
        private const float LockShakeDegrees = 14f;
        private const float OpenLockDegrees = -14f;

        // 흰 막(시안: 흰색 35%)
        private const float VeilAlpha = .35f;

        [SerializeField]
        private CanvasGroup toast;

        [SerializeField]
        private CanvasGroup glow;

        [SerializeField]
        private Image veil;

        [SerializeField]
        private RectTransform closedLock;

        [SerializeField]
        private RectTransform openLock;

        [SerializeField]
        private RectTransform[] shards;

        [SerializeField]
        private RectTransform[] sparks;

        [SerializeField]
        private RectTransform startButton;

        private Vector2 toastHome;
        private Vector2[] shardSpots;
        private Vector3[] sparkSizes;

        /// <summary>카드 자리(같은 부모 안의 anchoredPosition)에 겹쳐 잠금을 푼다. 실행 중이 아니면(Editor·테스트) 다 푼 모습을 바로 보인다.</summary>
        public void Play(Vector2 cardPosition)
        {
            Remember();
            StopAllCoroutines();
            ((RectTransform)transform).anchoredPosition = cardPosition;
            gameObject.SetActive(true);
            toast.gameObject.SetActive(true);
            if (Application.isPlaying && isActiveAndEnabled)
                StartCoroutine(Unlock());
            else
                ShowUnlocked();
        }

        public void Hide()
        {
            StopAllCoroutines();
            gameObject.SetActive(false);
            toast.gameObject.SetActive(false);
        }

        // 시안 자리를 처음 한 번 기억한다. 조각은 자물쇠에서 시안 자리를 지나 더 멀리 흩어진다.
        private void Remember()
        {
            if (shardSpots != null)
                return;
            toastHome = ((RectTransform)toast.transform).anchoredPosition;
            shardSpots = new Vector2[shards.Length];
            for (int i = 0; i < shards.Length; i++)
                shardSpots[i] = shards[i].anchoredPosition;
            sparkSizes = new Vector3[sparks.Length];
            for (int i = 0; i < sparks.Length; i++)
                sparkSizes[i] = sparks[i].localScale;
        }

        private IEnumerator Unlock()
        {
            Vector2 lockSpot = closedLock.anchoredPosition;
            var toastRect = (RectTransform)toast.transform;
            for (float t = 0f; ; t += Time.unscaledDeltaTime)
            {
                // 띠는 위에서 내려왔다가 잠시 뒤 사라진다.
                float drop = Mathf.Clamp01(t / ToastSeconds);
                toastRect.anchoredPosition =
                    toastHome + Vector2.up * ToastDrop * (1 - drop) * (1 - drop);
                toast.alpha = drop * (1 - Fade(t, ToastFadeAt));
                toast.gameObject.SetActive(t < ToastFadeAt + FadeSeconds);

                // 자물쇠가 점점 세게 흔들리다 깨진다.
                bool broken = t >= BreakAt;
                closedLock.gameObject.SetActive(!broken);
                float shake = Mathf.Clamp01((t - ShakeAt) / (BreakAt - ShakeAt));
                closedLock.localRotation = Quaternion.Euler(
                    0,
                    0,
                    Mathf.Sin(t * 40f) * LockShakeDegrees * shake
                );

                // 깨지면 흰 막이 조각나 흩어지고, 열린 자물쇠가 튀어나왔다 사라진다.
                float sinceBreak = t - BreakAt;
                float flight = Mathf.Clamp01(sinceBreak / ShardSeconds);
                for (int i = 0; i < shards.Length; i++)
                {
                    shards[i].gameObject.SetActive(broken && flight < 1);
                    shards[i].anchoredPosition = Vector2.LerpUnclamped(
                        lockSpot,
                        shardSpots[i],
                        ShardReach * (1 - (1 - flight) * (1 - flight))
                    );
                    shards[i].localRotation = Quaternion.Euler(
                        0,
                        0,
                        360f * flight * (i % 2 == 0 ? 1 : -1)
                    );
                }
                SetVeil(VeilAlpha * (1 - Mathf.Clamp01(sinceBreak / VeilFadeSeconds)));
                openLock.gameObject.SetActive(broken && t < OpenLockFadeAt + FadeSeconds);
                openLock.localScale =
                    Vector3.one
                    * 1.1f
                    * QuestMotion.Pop(sinceBreak / .25f)
                    * (1 - Fade(t, OpenLockFadeAt));
                openLock.localRotation = Quaternion.Euler(0, 0, OpenLockDegrees);

                // 카드가 노랗게 빛나고 반짝이와 [퀘스트 시작]이 나타난다.
                glow.alpha = Mathf.Clamp01((t - GlowAt) / GlowSeconds);
                for (int i = 0; i < sparks.Length; i++)
                {
                    float twinkle = 1 + .2f * Mathf.Sin(t * 9f + i);
                    sparks[i].gameObject.SetActive(broken && t < SparkFadeAt + FadeSeconds);
                    sparks[i].localScale =
                        sparkSizes[i]
                        * QuestMotion.Pop((t - GlowAt - i * .05f) / .25f)
                        * twinkle
                        * (1 - Fade(t, SparkFadeAt));
                }
                startButton.localScale =
                    Vector3.one * QuestMotion.Pop((t - ButtonAt) / ButtonSeconds);
                yield return null;
            }
        }

        private void ShowUnlocked()
        {
            ((RectTransform)toast.transform).anchoredPosition = toastHome;
            toast.alpha = 1;
            closedLock.gameObject.SetActive(false);
            openLock.gameObject.SetActive(false);
            foreach (RectTransform shard in shards)
                shard.gameObject.SetActive(false);
            foreach (RectTransform spark in sparks)
                spark.gameObject.SetActive(false);
            SetVeil(0);
            glow.alpha = 1;
            startButton.localScale = Vector3.one;
        }

        private void SetVeil(float alpha)
        {
            Color color = veil.color;
            color.a = alpha;
            veil.color = color;
        }

        // at부터 사라지는 정도(0~1)
        private static float Fade(float t, float at) => Mathf.Clamp01((t - at) / FadeSeconds);
    }
}
