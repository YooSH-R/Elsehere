using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Mission
{
    /// <summary>
    /// 기다리는 카드의 한 줄. 동그라미 표시, 받을 것, 누가 어디서, 남은 시간을 보이고, 다 되면 한 번 반짝여 알린다.
    /// 글자는 <see cref="FollowUpCardText"/>가 만든다.
    /// </summary>
    public sealed class FollowUpRowView : MonoBehaviour
    {
        // 다 됐을 때 반짝임: 전체 시간, 두 번 깜박이는 바탕의 가장 진한 불투명도, 처음에 통 튀는 시간과 크기
        private const float FlashSeconds = 1.6f;
        private const float FlashPulses = 2f;
        private const float FlashMaxAlpha = .45f;
        private const float PunchSeconds = .35f;
        private const float PunchScale = .05f;

        [SerializeField]
        private Image mark;

        [SerializeField]
        private TMP_Text title;

        [SerializeField]
        private TMP_Text detail;

        [SerializeField]
        private TMP_Text countdown;

        [SerializeField]
        [Tooltip("다 되면 반짝이는 줄 바탕. 색은 Builder가 정하고 불투명도만 바꾼다.")]
        private Image flash;

        private Coroutine flashing;

        public void Show(FollowUpCardRow row, Sprite markSprite)
        {
            mark.sprite = markSprite;
            title.text = row.Title;
            detail.text = row.Detail;
            countdown.text = row.Countdown ?? "";
            countdown.gameObject.SetActive(row.Countdown != null);
        }

        /// <summary>다 됐다는 알림으로 줄을 한 번 반짝인다.</summary>
        public void Flash()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (flashing != null)
            {
                StopCoroutine(flashing);
            }

            flashing = StartCoroutine(PlayFlash());
        }

        private IEnumerator PlayFlash()
        {
            for (float t = 0f; t < FlashSeconds; t += Time.unscaledDeltaTime)
            {
                float wave = Mathf.Abs(Mathf.Sin(t / FlashSeconds * FlashPulses * Mathf.PI));
                SetFlashAlpha(FlashMaxAlpha * wave);
                float punch = Mathf.Sin(Mathf.Clamp01(t / PunchSeconds) * Mathf.PI) * PunchScale;
                transform.localScale = Vector3.one * (1f + punch);
                yield return null;
            }

            ResetFlash();
        }

        // 반짝이는 중에 카드가 숨으면 바탕이 진한 채로 남지 않게 되돌린다.
        private void OnDisable()
        {
            if (flashing != null)
            {
                StopCoroutine(flashing);
            }

            ResetFlash();
        }

        private void ResetFlash()
        {
            flashing = null;
            SetFlashAlpha(0f);
            transform.localScale = Vector3.one;
        }

        private void SetFlashAlpha(float alpha)
        {
            if (flash == null)
            {
                return;
            }

            Color color = flash.color;
            color.a = alpha;
            flash.color = color;
        }
    }
}
