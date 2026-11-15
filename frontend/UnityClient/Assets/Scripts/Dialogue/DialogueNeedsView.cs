using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Dialogue
{
    /// <summary>
    /// 대화 창 이름 줄의 NPC 욕구 게이지(<see cref="NpcNeedGauges"/>). 욕구마다 위에 이름, 아래에 양 끝이 둥근 막대를 둔다.
    /// 바탕 없이 그리며, 자리는 대화 창(<see cref="DialoguePanelView"/>)이 NPC 이름표 바로 오른쪽(호감도·무례 경고 칸 앞)으로 정한다.
    /// 칸은 DialogueNeedsBuilder가 만든 세 개를 앞에서부터 쓰고 남는 칸은 숨긴다.
    /// </summary>
    public sealed class DialogueNeedsView : MonoBehaviour
    {
        /// <summary>게이지 하나의 폭</summary>
        public const float SlotWidth = 64;

        /// <summary>게이지 사이 간격</summary>
        public const float SlotGap = 16;

        private const float BarHeight = 8;

        // 바로 옆 호감도·무례 경고 칸이 먼저 눈에 들어오도록 채도를 낮춘 색만 쓴다. 평소는 무채색에 가까운 회갈색이고,
        // 조금 부족하면 옅은 모래색, 급할 때만 차분한 산호색이며 그때만 이름도 같은 색으로 바꾼다.
        private static readonly Color LabelColor = new Color32(128, 120, 112, 255);
        private static readonly Color FineColor = new Color32(160, 152, 142, 255);
        private static readonly Color LowColor = new Color32(212, 178, 124, 255);
        private static readonly Color UrgentColor = new Color32(206, 120, 104, 255);

        [SerializeField]
        [Tooltip("게이지 이름. 칸 순서대로")]
        private TMP_Text[] labels;

        [SerializeField]
        [Tooltip("막대를 채우는 부분. 칸 순서대로, 왼쪽 끝을 기준으로 폭만 바꾼다.")]
        private RectTransform[] fills;

        private int shownCount;

        /// <summary>보이는 게이지가 차지하는 폭. 게이지가 없으면 0</summary>
        public float Width =>
            shownCount == 0 ? 0 : shownCount * SlotWidth + (shownCount - 1) * SlotGap;

        public void Show(IReadOnlyList<NpcNeedGauge> gauges)
        {
            shownCount = Mathf.Min(gauges.Count, labels.Length);
            for (int i = 0; i < labels.Length; i++)
            {
                bool isShown = i < shownCount;
                labels[i].transform.parent.gameObject.SetActive(isShown);
                if (isShown)
                {
                    Paint(i, gauges[i]);
                }
            }
        }

        private void Paint(int slot, NpcNeedGauge gauge)
        {
            Color tone = ColorOf(gauge.Tone);
            TMP_Text label = labels[slot];
            label.text = gauge.Label;
            label.color = gauge.Tone == NpcNeedTone.Urgent ? UrgentColor : LabelColor;

            // 둥근 끝이 찌그러지지 않게 막대 높이만큼은 늘 남긴다.
            RectTransform fill = fills[slot];
            fill.sizeDelta = new Vector2(
                Mathf.Max(BarHeight, SlotWidth * Mathf.Clamp01(gauge.Fill)),
                fill.sizeDelta.y
            );
            fill.GetComponent<Image>().color = tone;
        }

        private static Color ColorOf(NpcNeedTone tone)
        {
            switch (tone)
            {
                case NpcNeedTone.Urgent:
                    return UrgentColor;
                case NpcNeedTone.Low:
                    return LowColor;
                default:
                    return FineColor;
            }
        }
    }
}
