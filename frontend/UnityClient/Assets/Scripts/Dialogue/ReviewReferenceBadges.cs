using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Dialogue
{
    /// <summary>줄바꿈·화면 크기에 맞춰 실제 첫 글자 옆에 사용 배지를 놓는다.</summary>
    public sealed class ReviewReferenceBadges : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text source;

        [SerializeField]
        private Sprite usedSprite;

        [SerializeField]
        private Sprite pendingSprite;
        private readonly List<Image> badges = new List<Image>();
        private ReviewReferenceText formatted;

        public void Configure(TMP_Text source, Sprite usedSprite, Sprite pendingSprite)
        {
            this.source = source;
            this.usedSprite = usedSprite;
            this.pendingSprite = pendingSprite;
        }

        public void Show(ReviewReferenceText text)
        {
            formatted = text;
            while (badges.Count < text.Markers.Count)
            {
                var image = new GameObject(
                    "ExpressionUsage",
                    typeof(RectTransform),
                    typeof(Image)
                ).GetComponent<Image>();
                image.transform.SetParent(source.transform, false);
                image.raycastTarget = false;
                image.preserveAspect = true;
                image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0, 1);
                image.rectTransform.sizeDelta = new Vector2(44, 44);
                badges.Add(image);
            }
            LateUpdate();
        }

        public void Clear()
        {
            formatted = null;
            foreach (Image badge in badges)
                badge.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (formatted == null || source == null || !source.isActiveAndEnabled)
                return;
            source.ForceMeshUpdate();
            for (int i = 0; i < badges.Count; i++)
            {
                Image badge = badges[i];
                badge.gameObject.SetActive(false);
                if (i >= formatted.Markers.Count)
                    continue;
                ReviewReferenceMarker marker = formatted.Markers[i];
                for (int c = 0; c < source.textInfo.characterCount; c++)
                {
                    TMP_CharacterInfo character = source.textInfo.characterInfo[c];
                    if (character.index != marker.SourceIndex)
                        continue;
                    badge.sprite = marker.Used ? usedSprite : pendingSprite;
                    badge.rectTransform.anchoredPosition = new Vector2(
                        14,
                        character.baseLine
                            + character.pointSize * .32f
                            - source.rectTransform.rect.yMax
                    );
                    badge.gameObject.SetActive(true);
                    break;
                }
            }
        }
    }
}
