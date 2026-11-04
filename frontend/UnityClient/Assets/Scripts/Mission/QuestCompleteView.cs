using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace A305.Mission
{
    /// <summary>
    /// 퀘스트를 끝내면 화면 가운데에 띄우는 '퀘스트 완료!' 팡파레(시안 f1). 화면이 살짝 어두워지며 팡파레 소리와 색종이가 터지고, 카드가 튀어 오르며
    /// 리본이 펼쳐진다. 이어서 큰 '완료' 도장이 쾅 찍히고(카드가 살짝 흔들림), 보상 코인이 오른쪽 위 코인 칩으로 날아간다.
    /// 자리는 시안 px × 1.2이고 부품과 [다음] 연결은 QuestCompleteBuilder가 넣는다.
    /// </summary>
    public sealed class QuestCompleteView : MonoBehaviour
    {
        private const float Px = 1.2f;

        // 시안 f1 순서(초): 바탕이 밝아지는 시간, 카드가 튀어 오르는 때, 리본이 펼쳐지는 때, 도장이 찍히는 때, 코인이 날아가는 때
        private const float FadeSeconds = .25f;
        private const float PopAt = .05f;
        private const float PopSeconds = .4f;
        private const float RibbonAt = .3f;
        private const float RibbonSeconds = .3f;
        private const float StampAt = .75f;
        private const float StampSeconds = .16f;
        private const float ShakeSeconds = .28f;
        private const float ShakeDistance = 7f;
        private const float ImpactSeconds = .6f;
        private const float CoinsAt = 1.15f;
        private const float CoinStagger = .07f;
        private const float CoinFlightSeconds = .55f;
        private const int FlyingCoins = 6;
        private const float RayDegreesPerSecond = 12f;

        // 색종이: 카드 가운데에서 터져 시안 자리까지 퍼진 뒤 천천히 떨어지며 사라진다.
        private const int ConfettiCount = 46;
        private const float BurstSeconds = .55f;
        private const float ConfettiFadeAt = 2.4f;
        private const float ConfettiFadeSeconds = .8f;
        private const float ConfettiGravity = 60f;

        // 시안 색종이 색
        private static readonly Color[] ConfettiColors =
        {
            new Color32(255, 119, 89, 255),
            new Color32(255, 201, 60, 255),
            new Color32(108, 188, 247, 255),
            new Color32(98, 217, 189, 255),
            new Color32(192, 166, 250, 255),
            new Color32(255, 159, 192, 255),
            new Color32(111, 209, 138, 255),
        };

        // 작은 도장은 시안처럼 번갈아 조금씩 기울인다.
        private static readonly float[] MiniStampAngles = { -4, 6, -8, 4 };

        [SerializeField]
        private CanvasGroup group;

        [SerializeField]
        private RectTransform rays;

        [SerializeField]
        private RectTransform card;

        [SerializeField]
        private RectTransform ribbon;

        [SerializeField]
        private Image icon;

        [SerializeField]
        private TMP_Text title;

        [SerializeField]
        private TMP_Text subtitle;

        [SerializeField]
        [Tooltip("들른 곳 작은 도장 줄. 꺼 둔 견본을 복사해 곳마다 하나씩 둔다.")]
        private Image miniStamp;

        [SerializeField]
        private TMP_Text reward;

        [SerializeField]
        [Tooltip("보상 알약의 코인. 날아가는 코인이 여기서 출발한다.")]
        private RectTransform rewardCoin;

        [SerializeField]
        private CanvasGroup bigStamp;

        [SerializeField]
        [Tooltip("도장이 찍힐 때 튀는 잉크 자국")]
        private CanvasGroup impacts;

        [SerializeField]
        [Tooltip("색종이 견본. 네모 그림이고, 동그라미는 confettiDot을 쓴다.")]
        private Image confetti;

        [SerializeField]
        private Sprite confettiDot;

        [SerializeField]
        [Tooltip("코인 칩으로 날아가는 코인 견본")]
        private RectTransform flyingCoin;

        [SerializeField]
        private AudioSource fanfare;

        private readonly List<GameObject> made = new List<GameObject>();
        private Vector2 cardHome;

        public bool IsOpen => gameObject.activeSelf;

        /// <summary>
        /// 끝낸 퀘스트를 보인다. 보상 코인이 코인 칩(coinTarget)에 닿으면 coinsArrived를 부른다.
        /// 실행 중이 아니면(Editor·테스트) 연출 없이 마지막 모습을 보이고 바로 부른다.
        /// </summary>
        public void Show(
            QuestEntry finished,
            Sprite questIcon,
            IReadOnlyList<Sprite> stamps,
            RectTransform coinTarget,
            Action coinsArrived
        )
        {
            ClearMade();
            gameObject.SetActive(true);
            icon.sprite = questIcon;
            title.text = finished.Title;
            subtitle.text = $"들른 곳 도장 {stamps.Count}개를 모두 모았어요";
            reward.text = $"+{finished.Reward}";
            for (int i = 0; i < stamps.Count; i++)
            {
                Image stamp = Make(miniStamp.gameObject, miniStamp.transform.parent)
                    .GetComponent<Image>();
                stamp.sprite = stamps[i];
                stamp.transform.localRotation = Quaternion.Euler(
                    0,
                    0,
                    MiniStampAngles[i % MiniStampAngles.Length]
                );
            }
            if (cardHome == default)
                cardHome = card.anchoredPosition;

            if (Application.isPlaying && isActiveAndEnabled)
                StartCoroutine(Play(coinTarget, coinsArrived));
            else
            {
                ShowFinished();
                coinsArrived?.Invoke();
            }
        }

        public void Close()
        {
            StopAllCoroutines();
            ClearMade();
            gameObject.SetActive(false);
        }

        private IEnumerator Play(RectTransform coinTarget, Action coinsArrived)
        {
            if (fanfare != null)
                fanfare.Play();
            StartCoroutine(Confetti());
            StartCoroutine(FlyCoins(coinTarget, coinsArrived));
            bool landed = false;
            for (float t = 0f; ; t += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Clamp01(t / FadeSeconds);
                rays.localRotation = Quaternion.Euler(0, 0, -t * RayDegreesPerSecond);
                card.localScale =
                    Vector3.one
                    * Mathf.LerpUnclamped(.6f, 1f, QuestMotion.Pop((t - PopAt) / PopSeconds));
                ribbon.localScale = new Vector3(
                    QuestMotion.Pop((t - RibbonAt) / RibbonSeconds),
                    1,
                    1
                );

                // 큰 도장이 위에서 쾅 찍히면 카드가 흔들리고 잉크 자국이 튀었다 사라진다.
                float slam = Mathf.Clamp01((t - StampAt) / StampSeconds);
                bigStamp.alpha = slam;
                bigStamp.transform.localScale = Vector3.one * Mathf.Lerp(2.2f, 1f, slam * slam);
                float sinceLanding = t - StampAt - StampSeconds;
                landed |= sinceLanding >= 0;
                float shake =
                    landed && sinceLanding < ShakeSeconds
                        ? Mathf.Sin(sinceLanding * 70f)
                            * ShakeDistance
                            * (1 - sinceLanding / ShakeSeconds)
                        : 0f;
                card.anchoredPosition = cardHome + new Vector2(shake, 0) * Px;
                impacts.alpha = landed ? 1 - Mathf.Clamp01(sinceLanding / ImpactSeconds) : 0;
                yield return null;
            }
        }

        // 색종이: 카드 가운데에서 시안 자리(카드 바깥)로 퍼진 뒤 돌며 떨어지다 사라진다.
        private IEnumerator Confetti()
        {
            var pieces =
                new List<(RectTransform rect, Image image, Vector2 to, float fall, float spin)>();
            for (int i = 0; i < ConfettiCount; i++)
            {
                GameObject piece = Make(confetti.gameObject, confetti.transform.parent);
                var image = piece.GetComponent<Image>();
                bool dot = Random.value >= .7f;
                if (dot)
                    image.sprite = confettiDot;
                image.rectTransform.sizeDelta =
                    (
                        dot
                            ? new Vector2(11, 11)
                            : new Vector2(Random.Range(9f, 16f), Random.Range(5f, 9f))
                    ) * Px;
                image.color = ConfettiColors[Random.Range(0, ConfettiColors.Length)];
                pieces.Add(
                    (
                        image.rectTransform,
                        image,
                        ConfettiSpot(),
                        Random.Range(40f, 90f),
                        Random.Range(-360f, 360f)
                    )
                );
            }
            Vector2 from = cardHome;
            for (
                float t = 0f;
                t < ConfettiFadeAt + ConfettiFadeSeconds;
                t += Time.unscaledDeltaTime
            )
            {
                float burst = 1 - Mathf.Pow(1 - Mathf.Clamp01(t / BurstSeconds), 3);
                float falling = Mathf.Max(0, t - BurstSeconds);
                float alpha = 1 - Mathf.Clamp01((t - ConfettiFadeAt) / ConfettiFadeSeconds);
                foreach (var piece in pieces)
                {
                    piece.rect.anchoredPosition =
                        Vector2.Lerp(from, piece.to, burst)
                        + Vector2.down
                            * (piece.fall * falling + .5f * ConfettiGravity * falling * falling)
                            * Px;
                    piece.rect.localRotation = Quaternion.Euler(0, 0, piece.spin * t);
                    Color color = piece.image.color;
                    color.a = alpha;
                    piece.image.color = color;
                }
                yield return null;
            }
            foreach (var piece in pieces)
                piece.rect.gameObject.SetActive(false);
        }

        // 시안 자리: 화면(1600×900) 가운데 기준으로 카드(740×530 둘레)를 비운 곳
        private static Vector2 ConfettiSpot()
        {
            while (true)
            {
                float x = Random.Range(300f, 1300f);
                float y = Random.Range(110f, 860f);
                if (!(x > 430 && x < 1170 && y > 210 && y < 740))
                    return new Vector2(x - 800, 450 - y) * Px;
            }
        }

        // 보상 알약의 코인에서 코인 칩까지 시안의 휜 길을 따라 코인이 차례로 날아간다. 첫 코인이 닿으면 코인 칩의 숫자를 올린다.
        private IEnumerator FlyCoins(RectTransform target, Action arrived)
        {
            yield return Wait(CoinsAt);
            var coins = new List<RectTransform>();
            for (int i = 0; i < FlyingCoins; i++)
                coins.Add((RectTransform)Make(flyingCoin.gameObject, transform).transform);
            Vector3 start = rewardCoin.position;
            Vector3 end = target != null ? target.position : start;
            bool told = false;
            for (float t = 0f; ; t += Time.unscaledDeltaTime)
            {
                bool flying = false;
                for (int i = 0; i < coins.Count; i++)
                {
                    float along = (t - i * CoinStagger) / CoinFlightSeconds;
                    coins[i].gameObject.SetActive(along > 0 && along < 1);
                    flying |= along < 1;
                    if (along <= 0 || along >= 1)
                        continue;
                    coins[i].position = CoinPath(start, end, along * along * (3 - 2 * along));
                    coins[i].localScale = Vector3.one * Mathf.Lerp(.8f, 1.1f, along);
                }
                if (!told && t >= CoinFlightSeconds)
                {
                    told = true;
                    arrived?.Invoke();
                }
                if (!flying)
                    yield break;
                yield return null;
            }
        }

        // 시안 f1의 길(M 900 600 C 1120 560, 1290 330, 1366 112)을 출발·도착 사이 비율로 옮긴 3차 곡선
        private static Vector3 CoinPath(Vector3 start, Vector3 end, float t)
        {
            Vector3 d = end - start;
            Vector3 c1 = start + new Vector3(d.x * .47f, d.y * .08f, 0);
            Vector3 c2 = start + new Vector3(d.x * .84f, d.y * .55f, 0);
            float u = 1 - t;
            return u * u * u * start + 3 * u * u * t * c1 + 3 * u * t * t * c2 + t * t * t * end;
        }

        private void ShowFinished()
        {
            group.alpha = 1;
            card.localScale = Vector3.one;
            ribbon.localScale = Vector3.one;
            bigStamp.alpha = 1;
            bigStamp.transform.localScale = Vector3.one;
            impacts.alpha = 0;
        }

        private static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
                yield return null;
        }

        // 꺼 둔 견본을 복사해 켠다. 이름은 견본과 같게 둔다.
        private GameObject Make(GameObject template, Transform parent)
        {
            GameObject item = Instantiate(template, parent);
            item.name = template.name;
            item.SetActive(true);
            made.Add(item);
            return item;
        }

        private void ClearMade()
        {
            foreach (GameObject item in made)
            {
                if (item == null)
                    continue;
                if (Application.isPlaying)
                    Destroy(item);
                else
                    DestroyImmediate(item);
            }
            made.Clear();
        }
    }
}
