using TMPro;
using UnityEngine;

namespace A305.Learning
{
    /// <summary>복습 버튼의 실제 글자 경계를 버튼 중심에 맞춘다. 테마·글꼴이 바뀌어도 같은 기준을 쓴다.</summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(200)]
    public sealed class ReviewButtonLabel : MonoBehaviour
    {
        private Vector2 arrangedSize;
        private Vector2 arrangedPosition;
        private string arrangedText;
        private TMP_FontAsset arrangedFont;
        private float arrangedFontSize;

        public void Arrange()
        {
            Transform child = transform.Find("Label");
            if (child == null || !child.TryGetComponent(out TMP_Text label))
                return;
            var button = (RectTransform)transform;
            RectTransform rect = label.rectTransform;
            if (
                !label.havePropertiesChanged
                && arrangedSize == button.rect.size
                && arrangedText == label.text
                && arrangedFont == label.font
                && arrangedFontSize == label.fontSize
                && rect.anchoredPosition == arrangedPosition
                && rect.anchorMin == new Vector2(.5f, .5f)
                && rect.anchorMax == rect.anchorMin
            )
                return;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.sizeDelta = new Vector2(Mathf.Max(0, button.rect.width - 36), button.rect.height);
            rect.anchoredPosition = Vector2.zero;
            label.margin = Vector4.zero;
            label.alignment = TextAlignmentOptions.Midline;
            label.ForceMeshUpdate(ignoreActiveState: true);
            // advance 폭·글꼴의 기준선 대신 화면에 그려지는 글자 경계의 중심을 쓴다.
            rect.anchoredPosition = -(Vector2)label.textBounds.center;
            arrangedSize = button.rect.size;
            arrangedPosition = rect.anchoredPosition;
            arrangedText = label.text;
            arrangedFont = label.font;
            arrangedFontSize = label.fontSize;
        }

        private void LateUpdate() => Arrange();
    }
}
