using System;
using A305.Player;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace A305.Dialogue
{
    /// <summary>글자의 <c>&lt;link&gt;</c>를 누르면 그 링크 ID를 알린다. 링크가 아닌 곳을 누르면 알리지 않는다.</summary>
    [RequireComponent(typeof(TMP_Text))]
    public class TextLinkClicks : MonoBehaviour, IPointerClickHandler, IClickPointerTarget
    {
        private TMP_Text text;

        public event Action<string> Clicked;

        public bool CanClickAt(Vector2 position, Camera camera) => FindLink(position, camera) >= 0;

        public void OnPointerClick(PointerEventData eventData)
        {
            int link = FindLink(eventData.position, eventData.pressEventCamera);
            if (link >= 0)
            {
                A305.World.UiSoundEvents.Play(A305.World.UiSound.Click);
                Clicked?.Invoke(text.textInfo.linkInfo[link].GetLinkID());
            }
        }

        private int FindLink(Vector2 position, Camera camera)
        {
            if (text == null)
                text = GetComponent<TMP_Text>();
            return TMP_TextUtilities.FindIntersectingLink(text, position, camera);
        }
    }
}
