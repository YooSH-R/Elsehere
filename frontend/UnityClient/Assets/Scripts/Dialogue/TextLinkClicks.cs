using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace A305.Dialogue
{
    /// <summary>글자의 <c>&lt;link&gt;</c>를 누르면 그 링크 ID를 알린다. 링크가 아닌 곳을 누르면 알리지 않는다.</summary>
    [RequireComponent(typeof(TMP_Text))]
    public class TextLinkClicks : MonoBehaviour, IPointerClickHandler
    {
        private TMP_Text text;

        public event Action<string> Clicked;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (text == null)
            {
                text = GetComponent<TMP_Text>();
            }

            int link = TMP_TextUtilities.FindIntersectingLink(
                text,
                eventData.position,
                eventData.pressEventCamera
            );
            if (link >= 0)
            {
                Clicked?.Invoke(text.textInfo.linkInfo[link].GetLinkID());
            }
        }
    }
}
