using System;
using System.Linq;
using A305.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Learning
{
    /// <summary>저장한 상황 한 칸. 글자 크기를 줄이지 않고 내용에 맞춰 높이를 늘린다.</summary>
    public sealed class ReviewWorkbookRow : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text title;

        [SerializeField]
        private TMP_Text situation;

        [SerializeField]
        private TMP_Text expressions;

        [SerializeField]
        private TMP_Text progress;

        [SerializeField]
        private Button practice;

        [SerializeField]
        private Button remove;

        public float Show(
            ReviewCard card,
            float width,
            bool busy,
            Action start,
            Action delete,
            Action openResult = null
        )
        {
            const float padding = 58;
            bool narrow = width < 670;
            title.text =
                "<color=#F2633F>"
                + PlaceScenes.NameOf(card.SceneCode)
                + "</color><color=#8B7C6B> · </color>"
                + Escape(card.Npc?.Name);
            situation.text = Escape(card.SituationSummary);
            expressions.text = string.Join(
                "\n\n",
                (card.Targets ?? new System.Collections.Generic.List<ReviewTarget>()).Select(
                    target => Escape(target.Expression)
                )
            );
            progress.text = card.PracticeCount > 0 ? card.PracticeCount + "회 연습" : "처음 연습";
            Button cardButton = GetComponent<Button>();
            if (cardButton != null)
            {
                cardButton.interactable = !busy && card.PracticeCount > 0 && openResult != null;
                cardButton.onClick.RemoveAllListeners();
                if (openResult != null)
                    cardButton.onClick.AddListener(() => openResult());
                cardButton.transition = Selectable.Transition.None;
            }
            float bodyWidth = Mathf.Max(160, width - padding * 2);
            float titleWidth = narrow ? bodyWidth - 56 : bodyWidth - 230;
            float titleHeight = Mathf.Max(
                44,
                title.GetPreferredValues(title.text, titleWidth, 0).y
            );
            Pin(title.rectTransform, padding, 36, titleWidth, titleHeight);
            float top = 36 + titleHeight + 20;
            if (!narrow)
                Pin(progress.rectTransform, width - padding - 216, 44, 160, 30);
            float situationHeight = Mathf.Max(
                40,
                situation.GetPreferredValues(situation.text, bodyWidth, 0).y
            );
            Pin(situation.rectTransform, padding, top, bodyWidth, situationHeight);
            top += situationHeight + 20;
            float expressionWidth = narrow ? bodyWidth : bodyWidth - 220;
            float expressionHeight = Mathf.Max(
                100,
                expressions.GetPreferredValues(expressions.text, expressionWidth, 0).y
            );
            Pin(expressions.rectTransform, padding, top, expressionWidth, expressionHeight);
            float bottom = top + expressionHeight;
            if (narrow)
            {
                bottom += 24;
                Pin(progress.rectTransform, padding, bottom + 12, bodyWidth - 190, 30);
                Pin((RectTransform)practice.transform, width - padding - 178, bottom, 178, 54);
                bottom += 54;
            }
            else
                Pin(
                    (RectTransform)practice.transform,
                    width - padding - 178,
                    Mathf.Max(top, bottom - 60),
                    178,
                    54
                );
            Pin((RectTransform)remove.transform, width - padding - 40, 38, 40, 40);
            practice.interactable = remove.interactable = !busy;
            practice.onClick.RemoveAllListeners();
            practice.onClick.AddListener(() => start());
            remove.onClick.RemoveAllListeners();
            remove.onClick.AddListener(() => delete());
            gameObject.SetActive(true);
            return Mathf.Max(300, bottom + 56);
        }

        internal static string Escape(string value) =>
            (value ?? "").Replace("<", "‹").Replace(">", "›");

        public static void Pin(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
